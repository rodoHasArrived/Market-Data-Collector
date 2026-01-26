using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Plugins.Core;

namespace MarketDataCollector.Infrastructure.Plugins.Integration;

/// <summary>
/// Adapter that connects the new plugin event stream to legacy collectors.
///
/// This enables gradual migration by allowing existing collectors and storage
/// to work with data from the new plugin system.
///
/// Usage:
/// <code>
/// var adapter = new LegacyCollectorAdapter(publisher, tradeCollector, depthCollector, quoteCollector);
///
/// await foreach (var evt in plugin.StreamAsync(request, ct))
/// {
///     adapter.ProcessEvent(evt);
/// }
/// </code>
/// </summary>
public sealed class LegacyCollectorAdapter
{
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector? _tradeCollector;
    private readonly MarketDepthCollector? _depthCollector;
    private readonly QuoteCollector? _quoteCollector;

    public LegacyCollectorAdapter(
        IMarketEventPublisher publisher,
        TradeDataCollector? tradeCollector = null,
        MarketDepthCollector? depthCollector = null,
        QuoteCollector? quoteCollector = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _tradeCollector = tradeCollector;
        _depthCollector = depthCollector;
        _quoteCollector = quoteCollector;
    }

    /// <summary>
    /// Processes a plugin event and routes it to the appropriate legacy collector.
    /// </summary>
    public void ProcessEvent(MarketDataEvent evt)
    {
        switch (evt)
        {
            case TradeEvent trade:
                ProcessTrade(trade);
                break;

            case QuoteEvent quote:
                ProcessQuote(quote);
                break;

            case DepthEvent depth:
                ProcessDepth(depth);
                break;

            case BarEvent bar:
                ProcessBar(bar);
                break;

            // Heartbeat and Error events don't need collector processing
        }
    }

    /// <summary>
    /// Processes a stream of plugin events asynchronously.
    /// </summary>
    public async Task ProcessStreamAsync(
        IAsyncEnumerable<MarketDataEvent> events,
        CancellationToken ct = default)
    {
        await foreach (var evt in events.WithCancellation(ct))
        {
            ProcessEvent(evt);
        }
    }

    private void ProcessTrade(TradeEvent trade)
    {
        if (_tradeCollector == null)
            return;

        var update = new MarketTradeUpdate(
            Timestamp: trade.Timestamp,
            Symbol: trade.Symbol,
            Price: trade.Price,
            Size: (long)trade.Size,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: trade.SequenceNumber ?? 0,
            StreamId: trade.Source,
            Venue: trade.Exchange ?? trade.Source
        );

        _tradeCollector.OnTrade(update);
    }

    private void ProcessQuote(QuoteEvent quote)
    {
        if (_quoteCollector == null)
            return;

        var update = new MarketQuoteUpdate(
            Timestamp: quote.Timestamp,
            Symbol: quote.Symbol,
            BidPrice: quote.BidPrice,
            BidSize: (long)quote.BidSize,
            AskPrice: quote.AskPrice,
            AskSize: (long)quote.AskSize,
            SequenceNumber: quote.SequenceNumber,
            StreamId: quote.Source,
            Venue: quote.BidExchange ?? quote.Source
        );

        _quoteCollector.OnQuote(update);
    }

    private void ProcessDepth(DepthEvent depth)
    {
        if (_depthCollector == null)
            return;

        // Convert depth levels to individual updates
        // The legacy collector maintains its own order book state
        var position = 0;

        foreach (var bid in depth.Bids)
        {
            _depthCollector.OnDepth(
                new MarketDepthUpdate(
                    Timestamp: depth.Timestamp,
                    Symbol: depth.Symbol,
                    Position: position++,
                    Operation: depth.IsSnapshot ? DepthOperation.Insert : DepthOperation.Update,
                    Side: OrderBookSide.Bid,
                    Price: bid.Price,
                    Size: bid.Size,
                    MarketMaker: bid.Exchange,
                    SequenceNumber: depth.SequenceNumber ?? 0,
                    StreamId: depth.Source,
                    Venue: bid.Exchange
                ));
        }

        position = 0;
        foreach (var ask in depth.Asks)
        {
            _depthCollector.OnDepth(
                new MarketDepthUpdate(
                    Timestamp: depth.Timestamp,
                    Symbol: depth.Symbol,
                    Position: position++,
                    Operation: depth.IsSnapshot ? DepthOperation.Insert : DepthOperation.Update,
                    Side: OrderBookSide.Ask,
                    Price: ask.Price,
                    Size: ask.Size,
                    MarketMaker: ask.Exchange,
                    SequenceNumber: depth.SequenceNumber ?? 0,
                    StreamId: depth.Source,
                    Venue: ask.Exchange
                ));
        }
    }

    private void ProcessBar(BarEvent bar)
    {
        // Bars are published directly to the event pipeline
        var historicalBar = new HistoricalBar
        {
            Date = DateOnly.FromDateTime(bar.Timestamp.DateTime),
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = (long)bar.Volume,
            AdjustedClose = bar.IsAdjusted ? bar.Close : null
        };

        var evt = new MarketEvent(
            bar.Timestamp,
            bar.Symbol,
            MarketEventType.HistoricalBar,
            historicalBar);

        _ = _publisher.PublishAsync(evt);
    }
}

/// <summary>
/// Wraps new plugin system to present the legacy IMarketDataClient interface.
/// This allows gradual migration of code that depends on IMarketDataClient.
/// </summary>
#pragma warning disable CS0618 // IMarketDataClient is obsolete - this adapter bridges old and new systems
public sealed class PluginToClientAdapter : IMarketDataClient
#pragma warning restore CS0618
{
    private readonly IMarketDataPlugin _plugin;
    private readonly IPluginConfig _config;
    private readonly Dictionary<int, (string Symbol, DataType Type)> _subscriptions = new();
    private int _nextSubId = 1;
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;

    /// <summary>
    /// Event raised when a trade is received.
    /// </summary>
    public event Action<TradeEvent>? TradeReceived;

    /// <summary>
    /// Event raised when a quote is received.
    /// </summary>
    public event Action<QuoteEvent>? QuoteReceived;

    /// <summary>
    /// Event raised when depth data is received.
    /// </summary>
    public event Action<DepthEvent>? DepthReceived;

    public PluginToClientAdapter(IMarketDataPlugin plugin, IPluginConfig config)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool IsEnabled => _plugin.State == PluginState.Ready || _plugin.State == PluginState.Streaming;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _plugin.InitializeAsync(_config, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _streamCts?.Cancel();

        if (_streamTask != null)
        {
            try { await _streamTask; }
            catch (OperationCanceledException) { }
        }
    }

    public int SubscribeMarketDepth(Application.Config.SymbolConfig cfg)
    {
        var id = _nextSubId++;
        _subscriptions[id] = (cfg.Symbol, DataType.Depth);
        StartStreamingIfNeeded();
        return id;
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        _subscriptions.Remove(subscriptionId);
    }

    public int SubscribeTrades(Application.Config.SymbolConfig cfg)
    {
        var id = _nextSubId++;
        _subscriptions[id] = (cfg.Symbol, DataType.Trade);
        StartStreamingIfNeeded();
        return id;
    }

    public void UnsubscribeTrades(int subscriptionId)
    {
        _subscriptions.Remove(subscriptionId);
    }

    private void StartStreamingIfNeeded()
    {
        if (_streamTask != null && !_streamTask.IsCompleted)
            return;

        _streamCts = new CancellationTokenSource();

        _streamTask = Task.Run(async () =>
        {
            var symbols = _subscriptions.Values.Select(s => s.Symbol).Distinct().ToList();
            var dataTypes = _subscriptions.Values.Select(s => s.Type).Distinct().ToList();

            if (symbols.Count == 0)
                return;

            var request = new DataStreamRequest
            {
                Symbols = symbols,
                DataTypes = dataTypes
            };

            await foreach (var evt in _plugin.StreamAsync(request, _streamCts.Token))
            {
                switch (evt)
                {
                    case TradeEvent trade:
                        TradeReceived?.Invoke(trade);
                        break;
                    case QuoteEvent quote:
                        QuoteReceived?.Invoke(quote);
                        break;
                    case DepthEvent depth:
                        DepthReceived?.Invoke(depth);
                        break;
                }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _streamCts?.Dispose();
        await _plugin.DisposeAsync();
    }
}
