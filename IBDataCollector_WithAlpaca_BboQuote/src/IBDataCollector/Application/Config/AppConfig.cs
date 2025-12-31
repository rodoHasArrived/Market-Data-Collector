namespace IBDataCollector.Application.Config;

/// <summary>
/// Root configuration model loaded from appsettings.json.
/// </summary>
/// <param name="DataRoot">Output directory root for storage sinks.</param>
/// <param name="Compress">Whether JSONL sinks should gzip.</param>
/// <param name="DataSource">
/// Market data provider selector:
/// - "IB" (default) uses Interactive Brokers via IIBMarketDataClient/IBMarketDataClient.
/// - "Alpaca" uses Alpaca market data via WebSocket (trades; quotes optional in future).
/// </param>
/// <param name="Alpaca">Alpaca provider options (required if DataSource == "Alpaca").</param>
/// <param name="Symbols">Symbol subscriptions.</param>
public sealed record AppConfig(
    string DataRoot = "data",
    bool Compress = false,
    string DataSource = "IB",
    AlpacaOptions? Alpaca = null,
    SymbolConfig[]? Symbols = null
);
