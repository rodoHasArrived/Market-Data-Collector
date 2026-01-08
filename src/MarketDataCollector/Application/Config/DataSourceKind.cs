namespace MarketDataCollector.Application.Config;

/// <summary>
/// Supported market data providers.
/// </summary>
public enum DataSourceKind
{
    /// <summary>Interactive Brokers via native TWS API.</summary>
    IB = 0,

    /// <summary>Alpaca Markets via WebSocket.</summary>
    Alpaca = 1,

    /// <summary>Polygon.io via WebSocket.</summary>
    Polygon = 2,

    /// <summary>
    /// StockSharp unified connector framework.
    /// Provides access to 90+ data sources including Rithmic, IQFeed, CQG, and more.
    /// </summary>
    StockSharp = 3
}
