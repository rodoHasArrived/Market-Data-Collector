-- =============================================================================
-- Lending event store schema — Sharpino-pattern tables for the Loan Contract aggregate
-- =============================================================================
-- Run with any migration tool (Flyway, DbUp, psql \i, etc.).
-- Flyway naming: V1__loan_contract_events.sql
--
-- Design:
--   loan_contract_events   — append-only event log; one row per LoanEvent
--   loan_contract_snapshots — periodic state snapshots; accelerates replay
--
-- Concurrency model:
--   A UNIQUE constraint on (aggregate_id, sequence_number) provides the
--   optimistic-concurrency safety net.  The application layer reads the
--   current MAX(sequence_number) inside a FOR UPDATE transaction, validates
--   it against the expected value, then inserts with incremented sequence
--   numbers.  The unique constraint rejects duplicate inserts on concurrent
--   writers regardless of application-level checks.
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS lending;

-- ---------------------------------------------------------------------------
-- Event log
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.loan_contract_events (
    id               BIGSERIAL        PRIMARY KEY,
    aggregate_id     UUID             NOT NULL,
    sequence_number  BIGINT           NOT NULL,
    event_json       TEXT             NOT NULL,
    created_at       TIMESTAMPTZ      NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_loan_contract_events_stream
        UNIQUE (aggregate_id, sequence_number)
);

-- Index for fast stream reads (the primary query pattern)
CREATE INDEX IF NOT EXISTS ix_loan_contract_events_aggregate
    ON lending.loan_contract_events (aggregate_id, sequence_number ASC);

-- ---------------------------------------------------------------------------
-- Snapshot table
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.loan_contract_snapshots (
    id               BIGSERIAL        PRIMARY KEY,
    aggregate_id     UUID             NOT NULL,
    -- The sequence number of the last event folded into this snapshot.
    sequence_number  BIGINT           NOT NULL,
    state_json       TEXT             NOT NULL,
    created_at       TIMESTAMPTZ      NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_loan_contract_snapshots_version
        UNIQUE (aggregate_id, sequence_number)
);

CREATE INDEX IF NOT EXISTS ix_loan_contract_snapshots_aggregate
    ON lending.loan_contract_snapshots (aggregate_id, sequence_number DESC);

-- ---------------------------------------------------------------------------
-- Optional: partition loan_contract_events by aggregate_id range
-- (enable when the event table exceeds ~10M rows; requires pg_partman or
--  manual partition definitions — left as a future migration)
-- ---------------------------------------------------------------------------
