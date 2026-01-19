using MarketDataCollector.Configuration;
using Microsoft.Data.Sqlite;

namespace MarketDataCollector.Storage;

/// <summary>
/// Simplified market data storage interface.
/// Replaces complex tiered storage with simple append/query operations.
/// </summary>
public interface ISimplifiedMarketDataStore : IAsyncDisposable
{
    /// <summary>
    /// Initializes the storage (creates tables, indices, etc.).
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Appends a market data point to storage.
    /// </summary>
    Task AppendAsync(SimplifiedMarketData data, CancellationToken ct = default);

    /// <summary>
    /// Appends multiple market data points in a batch.
    /// </summary>
    Task AppendBatchAsync(IEnumerable<SimplifiedMarketData> data, CancellationToken ct = default);

    /// <summary>
    /// Queries data for a symbol within a date range.
    /// </summary>
    Task<IReadOnlyList<SimplifiedMarketData>> QueryAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the latest data point for a symbol.
    /// </summary>
    Task<SimplifiedMarketData?> GetLatestAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    Task<StorageStats> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Storage statistics.
/// </summary>
public sealed record StorageStats(
    long TotalRecords,
    long DatabaseSizeBytes,
    DateTime? OldestRecord,
    DateTime? NewestRecord
);

/// <summary>
/// SQLite implementation of simplified market data storage.
/// Single table, simple schema, easy to query and backup.
/// </summary>
public sealed class SqliteSimplifiedStore : ISimplifiedMarketDataStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private SqliteConnection? _writeConnection;
    private bool _initialized;
    private bool _disposed;

    public SqliteSimplifiedStore(string databasePath)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        // Create main table with indices
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS market_data (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                symbol TEXT NOT NULL,
                price REAL NOT NULL,
                volume INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                source TEXT NOT NULL,
                received_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Index for symbol + time queries (most common)
            CREATE INDEX IF NOT EXISTS idx_market_data_symbol_timestamp
                ON market_data(symbol, timestamp DESC);

            -- Index for source filtering
            CREATE INDEX IF NOT EXISTS idx_market_data_source
                ON market_data(source);

            -- Index for latest queries
            CREATE INDEX IF NOT EXISTS idx_market_data_symbol_received
                ON market_data(symbol, received_at DESC);

            -- Prevent exact duplicates
            CREATE UNIQUE INDEX IF NOT EXISTS idx_market_data_unique
                ON market_data(symbol, timestamp, source);
        ";

        await using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync(ct);

        // Enable WAL mode for better concurrent access
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(ct);

        // Performance tuning
        command.CommandText = "PRAGMA synchronous=NORMAL;";
        await command.ExecuteNonQueryAsync(ct);

        _initialized = true;
    }

    public async Task AppendAsync(SimplifiedMarketData data, CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        await _writeLock.WaitAsync(ct);
        try
        {
            await EnsureWriteConnectionAsync(ct);

            await using var command = _writeConnection!.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO market_data
                    (symbol, price, volume, timestamp, source)
                VALUES
                    ($symbol, $price, $volume, $timestamp, $source)
            ";

            command.Parameters.AddWithValue("$symbol", data.Symbol);
            command.Parameters.AddWithValue("$price", (double)data.Price);
            command.Parameters.AddWithValue("$volume", data.Volume);
            command.Parameters.AddWithValue("$timestamp", data.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$source", data.Source);

            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AppendBatchAsync(IEnumerable<SimplifiedMarketData> data, CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        var dataList = data.ToList();
        if (dataList.Count == 0) return;

        await _writeLock.WaitAsync(ct);
        try
        {
            await EnsureWriteConnectionAsync(ct);

            await using var transaction = await _writeConnection!.BeginTransactionAsync(ct);

            await using var command = _writeConnection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO market_data
                    (symbol, price, volume, timestamp, source)
                VALUES
                    ($symbol, $price, $volume, $timestamp, $source)
            ";

            var symbolParam = command.Parameters.Add("$symbol", SqliteType.Text);
            var priceParam = command.Parameters.Add("$price", SqliteType.Real);
            var volumeParam = command.Parameters.Add("$volume", SqliteType.Integer);
            var timestampParam = command.Parameters.Add("$timestamp", SqliteType.Text);
            var sourceParam = command.Parameters.Add("$source", SqliteType.Text);

            foreach (var item in dataList)
            {
                symbolParam.Value = item.Symbol;
                priceParam.Value = (double)item.Price;
                volumeParam.Value = item.Volume;
                timestampParam.Value = item.Timestamp.ToString("O");
                sourceParam.Value = item.Source;

                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<SimplifiedMarketData>> QueryAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT symbol, price, volume, timestamp, source
            FROM market_data
            WHERE symbol = $symbol
                AND datetime(timestamp) BETWEEN datetime($from) AND datetime($to)
            ORDER BY timestamp DESC
            LIMIT 10000
        ";

        command.Parameters.AddWithValue("$symbol", symbol);
        command.Parameters.AddWithValue("$from", from.ToString("O"));
        command.Parameters.AddWithValue("$to", to.ToString("O"));

        var results = new List<SimplifiedMarketData>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SimplifiedMarketData(
                Symbol: reader.GetString(0),
                Price: (decimal)reader.GetDouble(1),
                Volume: reader.GetInt64(2),
                Timestamp: DateTime.Parse(reader.GetString(3)),
                Source: reader.GetString(4)
            ));
        }

        return results;
    }

    public async Task<SimplifiedMarketData?> GetLatestAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT symbol, price, volume, timestamp, source
            FROM market_data
            WHERE symbol = $symbol
            ORDER BY received_at DESC
            LIMIT 1
        ";

        command.Parameters.AddWithValue("$symbol", symbol);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new SimplifiedMarketData(
                Symbol: reader.GetString(0),
                Price: (decimal)reader.GetDouble(1),
                Volume: reader.GetInt64(2),
                Timestamp: DateTime.Parse(reader.GetString(3)),
                Source: reader.GetString(4)
            );
        }

        return null;
    }

    public async Task<StorageStats> GetStatsAsync(CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                COUNT(*) as total_records,
                MIN(timestamp) as oldest,
                MAX(timestamp) as newest
            FROM market_data
        ";

        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var totalRecords = reader.GetInt64(0);
        var oldest = reader.IsDBNull(1) ? null : (DateTime?)DateTime.Parse(reader.GetString(1));
        var newest = reader.IsDBNull(2) ? null : (DateTime?)DateTime.Parse(reader.GetString(2));

        // Get database file size
        command.CommandText = "SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size()";
        var sizeResult = await command.ExecuteScalarAsync(ct);
        var sizeBytes = sizeResult is long size ? size : 0;

        return new StorageStats(
            TotalRecords: totalRecords,
            DatabaseSizeBytes: sizeBytes,
            OldestRecord: oldest,
            NewestRecord: newest
        );
    }

    private async Task EnsureWriteConnectionAsync(CancellationToken ct)
    {
        if (_writeConnection == null || _writeConnection.State != System.Data.ConnectionState.Open)
        {
            _writeConnection?.Dispose();
            _writeConnection = new SqliteConnection(_connectionString);
            await _writeConnection.OpenAsync(ct);
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Storage not initialized. Call InitializeAsync first.");
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteSimplifiedStore));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _writeConnection?.Dispose();
        _writeLock.Dispose();

        await Task.CompletedTask;
    }
}
