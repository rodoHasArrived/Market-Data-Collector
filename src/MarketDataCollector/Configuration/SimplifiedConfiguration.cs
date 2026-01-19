namespace MarketDataCollector.Configuration;

/// <summary>
/// Simplified application configuration model.
/// Replaces complex nested JSON with flat, environment-variable-friendly structure.
/// </summary>
public sealed record SimplifiedAppConfiguration(
    ApplicationSettings Application,
    string DataPath,
    List<ProviderConfiguration> Providers,
    StorageConfiguration Storage
)
{
    /// <summary>
    /// Gets enabled providers only.
    /// </summary>
    public IEnumerable<ProviderConfiguration> EnabledProviders =>
        Providers.Where(p => p.Enabled);

    /// <summary>
    /// Gets a provider by name.
    /// </summary>
    public ProviderConfiguration? GetProvider(string name) =>
        Providers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Application-level settings.
/// </summary>
public sealed record ApplicationSettings(
    int HttpPort = 8080,
    string LogLevel = "Information"
);

/// <summary>
/// Provider configuration for market data sources.
/// </summary>
public sealed record ProviderConfiguration
{
    /// <summary>
    /// Unique provider name (e.g., "alpaca", "ib", "polygon").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Provider type: "PythonSubprocess", "InteractiveBrokers", "Native".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether this provider is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Path to Python script for subprocess providers.
    /// </summary>
    public string? ScriptPath { get; init; }

    /// <summary>
    /// Simple symbol list (for providers that don't need detailed config).
    /// </summary>
    public List<string>? Symbols { get; init; }

    /// <summary>
    /// Detailed symbol configuration (for IB, futures, options, etc.).
    /// </summary>
    public List<SymbolConfiguration>? SymbolDetails { get; init; }

    /// <summary>
    /// Gets all symbols (either from Symbols or SymbolDetails).
    /// </summary>
    public IEnumerable<string> GetAllSymbols()
    {
        if (Symbols != null)
            foreach (var s in Symbols)
                yield return s;

        if (SymbolDetails != null)
            foreach (var s in SymbolDetails)
                yield return s.Symbol;
    }
}

/// <summary>
/// Detailed symbol configuration for complex providers (IB, futures, options).
/// </summary>
public sealed record SymbolConfiguration(
    string Symbol,
    string SecurityType = "STK",
    string Exchange = "SMART",
    string Currency = "USD",
    string? Expiry = null,
    decimal? Strike = null,
    string? Right = null
);

/// <summary>
/// Storage configuration.
/// </summary>
public sealed record StorageConfiguration(
    string Type = "SQLite",
    string Path = "./data/market_data.db"
)
{
    /// <summary>
    /// Whether storage is SQLite (the simplified default).
    /// </summary>
    public bool IsSqlite => Type.Equals("SQLite", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Market data point - simplified from complex event types.
/// </summary>
public sealed record SimplifiedMarketData(
    string Symbol,
    decimal Price,
    long Volume,
    DateTime Timestamp,
    string Source
)
{
    /// <summary>
    /// Creates from various price types.
    /// </summary>
    public static SimplifiedMarketData FromTrade(
        string symbol, decimal price, long volume, DateTime timestamp, string source) =>
        new(symbol, price, volume, timestamp, source);

    /// <summary>
    /// Creates from quote midpoint.
    /// </summary>
    public static SimplifiedMarketData FromQuote(
        string symbol, decimal bidPrice, decimal askPrice, DateTime timestamp, string source) =>
        new(symbol, (bidPrice + askPrice) / 2, 0, timestamp, source);

    /// <summary>
    /// Creates from OHLCV bar (uses close price).
    /// </summary>
    public static SimplifiedMarketData FromBar(
        string symbol, decimal close, long volume, DateTime timestamp, string source) =>
        new(symbol, close, volume, timestamp, source);
}
