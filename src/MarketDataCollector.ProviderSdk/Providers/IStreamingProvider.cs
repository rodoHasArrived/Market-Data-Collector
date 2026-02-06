namespace MarketDataCollector.ProviderSdk.Providers;

/// <summary>
/// Contract for real-time streaming data providers in the plugin system.
/// Implementations connect to market data feeds and emit events via callbacks.
/// </summary>
/// <remarks>
/// This interface parallels the internal IMarketDataClient but is designed
/// for the plugin boundary. Instead of depending on internal SymbolConfig and
/// IMarketEventPublisher, it uses SDK-defined types and event callbacks.
/// The core application bridges these events into its internal event pipeline.
/// </remarks>
public interface IStreamingProvider : IProviderIdentity, IAsyncDisposable
{
    /// <summary>
    /// Whether the provider is enabled and configured with valid credentials.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Connect to the data feed. Must be called before subscribing.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnect from the data feed. Unsubscribes all active subscriptions.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Subscribe to market depth (L2 order book) for a symbol.
    /// </summary>
    /// <param name="subscription">Symbol subscription parameters.</param>
    /// <returns>Subscription ID for later unsubscription, or -1 if failed.</returns>
    int SubscribeMarketDepth(SymbolSubscription subscription);

    /// <summary>
    /// Unsubscribe from a previously created depth subscription.
    /// </summary>
    void UnsubscribeMarketDepth(int subscriptionId);

    /// <summary>
    /// Subscribe to tick-by-tick trade prints for a symbol.
    /// </summary>
    /// <param name="subscription">Symbol subscription parameters.</param>
    /// <returns>Subscription ID for later unsubscription, or -1 if failed.</returns>
    int SubscribeTrades(SymbolSubscription subscription);

    /// <summary>
    /// Unsubscribe from a previously created trade subscription.
    /// </summary>
    void UnsubscribeTrades(int subscriptionId);

    /// <summary>
    /// Event raised when a trade is received from the data feed.
    /// </summary>
    event Action<TradeReceived>? OnTradeReceived;

    /// <summary>
    /// Event raised when a quote (BBO) update is received.
    /// </summary>
    event Action<QuoteReceived>? OnQuoteReceived;

    /// <summary>
    /// Event raised when a depth/order book update is received.
    /// </summary>
    event Action<DepthUpdateReceived>? OnDepthUpdateReceived;
}

/// <summary>
/// Parameters for subscribing to market data for a symbol.
/// </summary>
public sealed record SymbolSubscription(
    string Symbol,
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    int DepthLevels = 10,
    string SecurityType = "STK",
    string Exchange = "SMART",
    string Currency = "USD",
    string? PrimaryExchange = null);

/// <summary>
/// Trade event emitted by a streaming provider.
/// </summary>
public sealed record TradeReceived(
    string Symbol,
    DateTimeOffset Timestamp,
    decimal Price,
    long Size,
    string? Exchange = null,
    string? Condition = null,
    long SequenceNumber = 0);

/// <summary>
/// Quote (BBO) event emitted by a streaming provider.
/// </summary>
public sealed record QuoteReceived(
    string Symbol,
    DateTimeOffset Timestamp,
    decimal BidPrice,
    decimal AskPrice,
    long BidSize,
    long AskSize,
    string? BidExchange = null,
    string? AskExchange = null,
    long SequenceNumber = 0);

/// <summary>
/// Depth/order book update event emitted by a streaming provider.
/// </summary>
public sealed record DepthUpdateReceived(
    string Symbol,
    DateTimeOffset Timestamp,
    IReadOnlyList<DepthLevel> Bids,
    IReadOnlyList<DepthLevel> Asks,
    long SequenceNumber = 0);

/// <summary>
/// A single price level in the order book.
/// </summary>
public sealed record DepthLevel(
    decimal Price,
    long Size,
    int OrderCount = 0);
