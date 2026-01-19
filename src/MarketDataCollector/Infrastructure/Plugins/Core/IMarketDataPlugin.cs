using System.Runtime.CompilerServices;

namespace MarketDataCollector.Infrastructure.Plugins.Core;

/// <summary>
/// Unified interface for all market data plugins (real-time and historical).
/// This is the single abstraction that all data source plugins implement.
///
/// Design philosophy: "Simplest thing that works, designed to scale"
/// - ONE interface for all data sources (no IMarketDataClient vs IHistoricalDataProvider split)
/// - Capability-driven: plugins declare what they support
/// - Streaming-first: both real-time and historical data flow through IAsyncEnumerable
/// - Health and metrics built-in, not bolted on
/// </summary>
/// <remarks>
/// Migration note: This interface replaces the following legacy interfaces:
/// - IMarketDataClient (real-time streaming)
/// - IHistoricalDataProvider (historical backfill)
/// - IDataSource (unified but complex)
///
/// All providers should implement this single interface. Use the base classes
/// (RealtimePluginBase, HistoricalPluginBase) for common functionality.
/// </remarks>
public interface IMarketDataPlugin : IAsyncDisposable
{
    #region Identity

    /// <summary>
    /// Unique plugin identifier (e.g., "alpaca", "yahoo", "ib").
    /// Used for configuration and routing.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Brief description of the plugin and what it provides.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Plugin version following semantic versioning.
    /// </summary>
    Version Version { get; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the plugin with the provided configuration.
    /// Called once before any data operations.
    /// </summary>
    /// <param name="config">Plugin configuration from environment/settings.</param>
    /// <param name="ct">Cancellation token for graceful shutdown.</param>
    Task InitializeAsync(IPluginConfig config, CancellationToken ct = default);

    /// <summary>
    /// Current operational state of the plugin.
    /// </summary>
    PluginState State { get; }

    #endregion

    #region Capabilities

    /// <summary>
    /// Capabilities this plugin supports.
    /// Used by orchestrators for routing and fallback decisions.
    /// </summary>
    PluginCapabilities Capabilities { get; }

    #endregion

    #region Data Streaming

    /// <summary>
    /// Streams market data for the requested symbols.
    /// This is the unified data pipeline for BOTH real-time and historical data.
    ///
    /// For real-time plugins: streams indefinitely until cancelled.
    /// For historical plugins: streams requested range then completes.
    /// For hybrid plugins: behavior depends on request options.
    /// </summary>
    /// <param name="request">Symbols and options for the data stream.</param>
    /// <param name="ct">Cancellation token to stop streaming.</param>
    /// <returns>Async stream of market data events.</returns>
    IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default);

    #endregion

    #region Health & Diagnostics

    /// <summary>
    /// Current health status of the plugin.
    /// </summary>
    PluginHealth Health { get; }

    /// <summary>
    /// Event raised when plugin health changes significantly.
    /// </summary>
    event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

    /// <summary>
    /// Emits plugin metrics for observability.
    /// </summary>
    event EventHandler<PluginMetricEventArgs>? MetricEmitted;

    #endregion
}

/// <summary>
/// Plugin operational state.
/// </summary>
public enum PluginState
{
    /// <summary>Plugin created but not initialized.</summary>
    Created,

    /// <summary>Plugin is initializing.</summary>
    Initializing,

    /// <summary>Plugin is ready to stream data.</summary>
    Ready,

    /// <summary>Plugin is actively streaming data.</summary>
    Streaming,

    /// <summary>Plugin is reconnecting after a connection loss.</summary>
    Reconnecting,

    /// <summary>Plugin encountered a rate limit, waiting to resume.</summary>
    RateLimited,

    /// <summary>Plugin has an error and cannot operate.</summary>
    Error,

    /// <summary>Plugin is disposed.</summary>
    Disposed
}

/// <summary>
/// Request to stream market data.
/// </summary>
public sealed record DataStreamRequest
{
    /// <summary>
    /// Symbols to stream data for.
    /// </summary>
    public required IReadOnlyList<string> Symbols { get; init; }

    /// <summary>
    /// Types of data to stream (trades, quotes, bars, etc.).
    /// If empty, streams all supported types.
    /// </summary>
    public IReadOnlyList<DataType> DataTypes { get; init; } = [];

    /// <summary>
    /// For historical requests: start date (inclusive).
    /// Null for real-time streaming.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// For historical requests: end date (inclusive).
    /// Null for real-time streaming (stream until cancelled).
    /// </summary>
    public DateOnly? To { get; init; }

    /// <summary>
    /// Whether to request adjusted prices (for historical data).
    /// </summary>
    public bool AdjustedPrices { get; init; } = true;

    /// <summary>
    /// Bar interval for aggregate data (e.g., "1min", "1hour", "1day").
    /// </summary>
    public string? BarInterval { get; init; }

    /// <summary>
    /// Creates a real-time streaming request for the given symbols.
    /// </summary>
    public static DataStreamRequest Realtime(params string[] symbols) => new()
    {
        Symbols = symbols,
        DataTypes = [DataType.Trade, DataType.Quote]
    };

    /// <summary>
    /// Creates a historical data request for the given symbols and date range.
    /// </summary>
    public static DataStreamRequest Historical(
        IEnumerable<string> symbols,
        DateOnly from,
        DateOnly? to = null,
        string interval = "1day") => new()
    {
        Symbols = symbols.ToList(),
        DataTypes = [DataType.Bar],
        From = from,
        To = to ?? DateOnly.FromDateTime(DateTime.UtcNow),
        BarInterval = interval
    };

    /// <summary>
    /// Whether this is a historical (bounded) request.
    /// </summary>
    public bool IsHistorical => From.HasValue;

    /// <summary>
    /// Whether this is a real-time (unbounded) request.
    /// </summary>
    public bool IsRealtime => !From.HasValue;
}

/// <summary>
/// Types of market data.
/// </summary>
public enum DataType
{
    /// <summary>Trade execution (tick-by-tick).</summary>
    Trade,

    /// <summary>Best bid/offer quote.</summary>
    Quote,

    /// <summary>Level 2 order book depth.</summary>
    Depth,

    /// <summary>OHLCV bar (aggregate).</summary>
    Bar,

    /// <summary>Dividend event.</summary>
    Dividend,

    /// <summary>Stock split event.</summary>
    Split,

    /// <summary>Earnings announcement.</summary>
    Earnings
}

/// <summary>
/// Health changed event arguments.
/// </summary>
public sealed class PluginHealthChangedEventArgs : EventArgs
{
    public required PluginHealth Previous { get; init; }
    public required PluginHealth Current { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Metric event arguments for observability.
/// </summary>
public sealed class PluginMetricEventArgs : EventArgs
{
    public required string Name { get; init; }
    public required double Value { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
