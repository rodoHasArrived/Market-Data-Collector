#if STOCKSHARP
using StockSharp.Messages;
#endif
using MarketDataCollector.Infrastructure.Contracts;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Configuration interface for StockSharp adapters in the connector hub.
/// Each adapter type implements this interface to provide adapter-specific configuration
/// and factory methods for creating the underlying StockSharp message adapter.
/// </summary>
[ImplementsAdr("ADR-001", "StockSharp adapter configuration contract")]
public interface IStockSharpAdapterConfig
{
    /// <summary>
    /// Unique identifier for the adapter (e.g., "alpaca", "polygon", "ib").
    /// </summary>
    string AdapterId { get; }

    /// <summary>
    /// Human-readable display name for the adapter.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description of the adapter and its data coverage.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether the adapter is currently enabled.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Priority for adapter selection (lower = higher priority).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Supported markets/regions (e.g., "US", "EU", "APAC").
    /// </summary>
    IReadOnlyList<string> SupportedMarkets { get; }

    /// <summary>
    /// Supported asset classes (e.g., "equity", "futures", "options", "crypto").
    /// </summary>
    IReadOnlyList<string> SupportedAssetClasses { get; }

    /// <summary>
    /// Supported exchanges (e.g., "NYSE", "NASDAQ", "CME", "ICE").
    /// </summary>
    IReadOnlyList<string> SupportedExchanges { get; }

    /// <summary>
    /// Provider IDs that this adapter can handle (e.g., ["alpaca", "alpaca-streaming"]).
    /// </summary>
    IReadOnlyList<string> MappedProviderIds { get; }

    /// <summary>
    /// Whether this adapter supports real-time streaming data.
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Whether this adapter supports historical data backfill.
    /// </summary>
    bool SupportsBackfill { get; }

    /// <summary>
    /// Whether this adapter supports Level 2 market depth.
    /// </summary>
    bool SupportsMarketDepth { get; }

    /// <summary>
    /// Maximum number of depth levels supported (null = unlimited).
    /// </summary>
    int? MaxDepthLevels { get; }

#if STOCKSHARP
    /// <summary>
    /// Create the StockSharp message adapter instance.
    /// </summary>
    /// <param name="transactionIdGenerator">Transaction ID generator from the connector.</param>
    /// <returns>Configured StockSharp message adapter.</returns>
    IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator);
#endif

    /// <summary>
    /// Validate the adapter configuration.
    /// </summary>
    /// <returns>Validation result with any errors or warnings.</returns>
    AdapterValidationResult Validate();
}

/// <summary>
/// Result of adapter configuration validation.
/// </summary>
public sealed record AdapterValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static AdapterValidationResult Success() => new(true, [], []);

    /// <summary>
    /// Creates a validation result with errors.
    /// </summary>
    public static AdapterValidationResult WithErrors(params string[] errors) =>
        new(false, errors, []);

    /// <summary>
    /// Creates a validation result with warnings (still valid).
    /// </summary>
    public static AdapterValidationResult WithWarnings(params string[] warnings) =>
        new(true, [], warnings);
}

/// <summary>
/// Base class for StockSharp adapter configurations with common functionality.
/// </summary>
public abstract class StockSharpAdapterConfigBase : IStockSharpAdapterConfig
{
    /// <inheritdoc/>
    public abstract string AdapterId { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public int Priority { get; init; } = 50;

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> SupportedMarkets { get; init; } = ["US"];

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> SupportedAssetClasses { get; init; } = ["equity"];

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> SupportedExchanges { get; init; } = [];

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> MappedProviderIds { get; init; } = [];

    /// <inheritdoc/>
    public virtual bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public virtual bool SupportsBackfill { get; init; } = false;

    /// <inheritdoc/>
    public virtual bool SupportsMarketDepth { get; init; } = false;

    /// <inheritdoc/>
    public virtual int? MaxDepthLevels { get; init; }

#if STOCKSHARP
    /// <inheritdoc/>
    public abstract IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator);
#endif

    /// <inheritdoc/>
    public virtual AdapterValidationResult Validate() => AdapterValidationResult.Success();

    /// <summary>
    /// Helper to validate required string fields.
    /// </summary>
    protected static bool IsNullOrEmpty(string? value) => string.IsNullOrWhiteSpace(value);
}
