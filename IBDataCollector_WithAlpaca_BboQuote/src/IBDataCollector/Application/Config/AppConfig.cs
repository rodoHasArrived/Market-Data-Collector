using System.Text.Json.Serialization;

namespace IBDataCollector.Application.Config;

/// <summary>
/// Root configuration model loaded from appsettings.json.
/// </summary>
/// <param name="DataRoot">Output directory root for storage sinks.</param>
/// <param name="Compress">Whether JSONL sinks should gzip.</param>
/// <param name="DataSource">
/// Market data provider selector:
/// - <see cref="DataSourceKind.IB"/> (default) uses Interactive Brokers via IIBMarketDataClient/IBMarketDataClient.
/// - <see cref="DataSourceKind.Alpaca"/> uses Alpaca market data via WebSocket (trades; quotes optional in future).
/// </param>
/// <param name="Alpaca">Alpaca provider options (required if DataSource == DataSourceKind.Alpaca).</param>
/// <param name="Symbols">Symbol subscriptions.</param>
public sealed record AppConfig(
    string DataRoot = "data",
    bool Compress = false,
    [property: JsonConverter(typeof(DataSourceKindConverter))] DataSourceKind DataSource = DataSourceKind.IB,
    AlpacaOptions? Alpaca = null,
    SymbolConfig[]? Symbols = null
);
