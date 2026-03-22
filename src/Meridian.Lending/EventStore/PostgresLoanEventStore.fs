/// PostgreSQL-backed implementation of ILoanEventStore.
/// Uses Npgsql directly with JSON event serialization.
/// Table schema is created by deploy/sql/lending/V1__loan_contract_events.sql.
///
/// Follows the same structural guarantees as Sharpino's PostgresEventStore:
///   - Optimistic concurrency via a unique constraint on (aggregate_id, sequence_number)
///   - Snapshot table for fast state retrieval without full replay
///   - All operations are safe for concurrent multi-writer workloads
module Meridian.Lending.EventStore.PostgresLoanEventStore

open System
open Npgsql
open ILoanEventStore

/// PostgreSQL implementation of ILoanEventStore.
/// Requires the tables created by deploy/sql/lending/V1__loan_contract_events.sql.
type PostgresLoanEventStore(connectionString: string) =
    let openConnection () =
        let conn = new NpgsqlConnection(connectionString)
        conn.Open()
        conn

    interface ILoanEventStore with

        member _.GetEvents(aggregateId) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                SELECT aggregate_id, sequence_number, event_json, created_at
                FROM lending.loan_contract_events
                WHERE aggregate_id = @aggregateId
                ORDER BY sequence_number ASC
            """
            cmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
            use reader = cmd.ExecuteReader()
            let results = ResizeArray()
            while reader.Read() do
                results.Add({
                    AggregateId = reader.GetGuid(0)
                    SequenceNumber = reader.GetInt64(1)
                    EventJson = reader.GetString(2)
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
                })
            results |> Seq.toList

        member _.GetEventsAfter(aggregateId, fromSequenceExclusive) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                SELECT aggregate_id, sequence_number, event_json, created_at
                FROM lending.loan_contract_events
                WHERE aggregate_id = @aggregateId
                  AND sequence_number > @fromSeq
                ORDER BY sequence_number ASC
            """
            cmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
            cmd.Parameters.AddWithValue("fromSeq", fromSequenceExclusive) |> ignore
            use reader = cmd.ExecuteReader()
            let results = ResizeArray()
            while reader.Read() do
                results.Add({
                    AggregateId = reader.GetGuid(0)
                    SequenceNumber = reader.GetInt64(1)
                    EventJson = reader.GetString(2)
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
                })
            results |> Seq.toList

        member _.GetSnapshot(aggregateId) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                SELECT aggregate_id, sequence_number, state_json, created_at
                FROM lending.loan_contract_snapshots
                WHERE aggregate_id = @aggregateId
                ORDER BY sequence_number DESC
                LIMIT 1
            """
            cmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
            use reader = cmd.ExecuteReader()
            if reader.Read() then
                Some {
                    AggregateId = reader.GetGuid(0)
                    SequenceNumber = reader.GetInt64(1)
                    StateJson = reader.GetString(2)
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
                }
            else
                None

        member _.AppendEvents(aggregateId, expectedSequence, events) =
            use conn = openConnection()
            use tx = conn.BeginTransaction()
            try
                // Read current max sequence inside the transaction (serializable or repeatable-read
                // is recommended at the connection level for strict consistency; the unique constraint
                // on (aggregate_id, sequence_number) provides the final safety net regardless).
                use seqCmd = conn.CreateCommand()
                seqCmd.Transaction <- tx
                seqCmd.CommandText <- """
                    SELECT COALESCE(MAX(sequence_number), 0)
                    FROM lending.loan_contract_events
                    WHERE aggregate_id = @aggregateId
                    FOR UPDATE
                """
                seqCmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
                let currentSeq = seqCmd.ExecuteScalar() :?> int64

                if currentSeq <> expectedSequence then
                    tx.Rollback()
                    ConcurrencyConflict(ConcurrencyError(expectedSequence, currentSeq))
                else
                    let mutable seq = currentSeq
                    for json in events do
                        seq <- seq + 1L
                        use insertCmd = conn.CreateCommand()
                        insertCmd.Transaction <- tx
                        insertCmd.CommandText <- """
                            INSERT INTO lending.loan_contract_events
                                (aggregate_id, sequence_number, event_json, created_at)
                            VALUES (@aggregateId, @seq, @eventJson, NOW())
                        """
                        insertCmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
                        insertCmd.Parameters.AddWithValue("seq", seq) |> ignore
                        insertCmd.Parameters.AddWithValue("eventJson", json) |> ignore
                        insertCmd.ExecuteNonQuery() |> ignore
                    tx.Commit()
                    Appended seq
            with ex ->
                tx.Rollback()
                raise ex

        member _.SaveSnapshot(aggregateId, sequenceNumber, stateJson) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                INSERT INTO lending.loan_contract_snapshots
                    (aggregate_id, sequence_number, state_json, created_at)
                VALUES (@aggregateId, @seq, @stateJson, NOW())
                ON CONFLICT (aggregate_id, sequence_number) DO UPDATE
                    SET state_json = EXCLUDED.state_json,
                        created_at = EXCLUDED.created_at
            """
            cmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
            cmd.Parameters.AddWithValue("seq", sequenceNumber) |> ignore
            cmd.Parameters.AddWithValue("stateJson", stateJson) |> ignore
            cmd.ExecuteNonQuery() |> ignore

        member _.GetCurrentSequence(aggregateId) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                SELECT COALESCE(MAX(sequence_number), 0)
                FROM lending.loan_contract_events
                WHERE aggregate_id = @aggregateId
            """
            cmd.Parameters.AddWithValue("aggregateId", aggregateId) |> ignore
            cmd.ExecuteScalar() :?> int64
