namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Request for batch symbol deletion.
/// </summary>
public sealed record BatchDeleteRequest(
    /// <summary>Symbols to delete.</summary>
    string[] Symbols,

    /// <summary>If true, also remove from all watchlists.</summary>
    bool RemoveFromWatchlists = true
);

/// <summary>
/// Request for batch subscription toggle.
/// </summary>
public sealed record BatchToggleRequest(
    /// <summary>Symbols to modify.</summary>
    string[] Symbols,

    /// <summary>Enable or disable trade subscriptions (null = don't change).</summary>
    bool? SubscribeTrades = null,

    /// <summary>Enable or disable depth subscriptions (null = don't change).</summary>
    bool? SubscribeDepth = null,

    /// <summary>Set depth levels (null = don't change).</summary>
    int? DepthLevels = null
);

/// <summary>
/// Request for batch symbol update.
/// </summary>
public sealed record BatchUpdateRequest(
    /// <summary>Symbols to update.</summary>
    string[] Symbols,

    /// <summary>New security type (null = don't change).</summary>
    string? SecurityType = null,

    /// <summary>New exchange (null = don't change).</summary>
    string? Exchange = null,

    /// <summary>New currency (null = don't change).</summary>
    string? Currency = null,

    /// <summary>New primary exchange (null = don't change).</summary>
    string? PrimaryExchange = null
);

/// <summary>
/// Request to add multiple symbols at once.
/// </summary>
public sealed record BatchAddRequest(
    /// <summary>Symbols to add.</summary>
    string[] Symbols,

    /// <summary>Default subscription settings.</summary>
    BatchAddDefaults? Defaults = null,

    /// <summary>Skip symbols that already exist.</summary>
    bool SkipExisting = true
);

/// <summary>
/// Default settings for batch add operation.
/// </summary>
public sealed record BatchAddDefaults(
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    int DepthLevels = 10,
    string SecurityType = "STK",
    string Exchange = "SMART",
    string Currency = "USD"
);

/// <summary>
/// Result of a batch operation.
/// </summary>
public sealed record BatchOperationResult(
    /// <summary>Whether the operation succeeded.</summary>
    bool Success,

    /// <summary>Type of operation performed.</summary>
    string Operation,

    /// <summary>Number of symbols affected.</summary>
    int AffectedCount,

    /// <summary>Number of symbols skipped.</summary>
    int SkippedCount,

    /// <summary>Number of failures.</summary>
    int FailedCount,

    /// <summary>Symbols that were affected.</summary>
    string[] AffectedSymbols,

    /// <summary>Symbols that were skipped.</summary>
    string[] SkippedSymbols,

    /// <summary>Any error messages.</summary>
    string[] Errors,

    /// <summary>Processing time in milliseconds.</summary>
    long ProcessingTimeMs
);

/// <summary>
/// Request to move symbols between watchlists.
/// </summary>
public sealed record BatchMoveToWatchlistRequest(
    /// <summary>Symbols to move.</summary>
    string[] Symbols,

    /// <summary>Target watchlist ID.</summary>
    string TargetWatchlistId,

    /// <summary>Source watchlist ID (null = don't remove from any).</summary>
    string? SourceWatchlistId = null,

    /// <summary>Remove from source watchlist.</summary>
    bool RemoveFromSource = false
);

/// <summary>
/// Request to copy subscription settings from one symbol to others.
/// </summary>
public sealed record BatchCopySettingsRequest(
    /// <summary>Symbol to copy settings from.</summary>
    string SourceSymbol,

    /// <summary>Symbols to copy settings to.</summary>
    string[] TargetSymbols
);

/// <summary>
/// Filter criteria for batch operations.
/// </summary>
public sealed record BatchFilter(
    /// <summary>Only affect symbols matching this pattern (supports * wildcard).</summary>
    string? SymbolPattern = null,

    /// <summary>Only affect symbols subscribed to trades.</summary>
    bool? HasTradeSubscription = null,

    /// <summary>Only affect symbols subscribed to depth.</summary>
    bool? HasDepthSubscription = null,

    /// <summary>Only affect symbols in these watchlists.</summary>
    string[]? InWatchlists = null,

    /// <summary>Only affect symbols with this security type.</summary>
    string? SecurityType = null,

    /// <summary>Only affect symbols on this exchange.</summary>
    string? Exchange = null
);

/// <summary>
/// Request to perform batch operations with a filter.
/// </summary>
public sealed record BatchFilteredOperationRequest(
    /// <summary>Filter criteria to select symbols.</summary>
    BatchFilter Filter,

    /// <summary>Operation to perform (toggle, delete, update).</summary>
    string Operation,

    /// <summary>Parameters for the operation.</summary>
    Dictionary<string, object?>? Parameters = null
);
