using MarketDataCollector.Infrastructure.Plugins.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.InteractiveBrokers;

/// <summary>
/// Connection manager for the IB plugin.
/// Provides a simplified interface for the plugin system without requiring
/// direct dependency on the IBAPI.
/// </summary>
/// <remarks>
/// This class provides a clean boundary between the plugin system and the
/// legacy IB implementation. When IBAPI is available, it creates lightweight
/// collectors that emit plugin events. When IBAPI is not available, it
/// provides a stub implementation.
/// </remarks>
public sealed class IBPluginConnectionManager : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _clientId;
    private readonly ILogger _logger;

    // Event callbacks
    private readonly Action<string, decimal, decimal, DateTimeOffset, string?> _onTrade;
    private readonly Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> _onQuote;
    private readonly Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> _onDepth;
    private readonly Action<bool, string?> _onConnectionStateChanged;

    // Connection state
    private bool _isConnected;
    private bool _disposed;

    // Subscription tracking
    private readonly Dictionary<string, List<int>> _symbolSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _subscriptionLock = new();

#if IBAPI
    private IBPluginBridge? _bridge;
#endif

    public IBPluginConnectionManager(
        string host,
        int port,
        int clientId,
        Action<string, decimal, decimal, DateTimeOffset, string?> onTrade,
        Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> onQuote,
        Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> onDepth,
        Action<bool, string?> onConnectionStateChanged,
        ILogger? logger = null)
    {
        _host = host;
        _port = port;
        _clientId = clientId;
        _onTrade = onTrade;
        _onQuote = onQuote;
        _onDepth = onDepth;
        _onConnectionStateChanged = onConnectionStateChanged;
        _logger = logger ?? NullLogger.Instance;
    }

    public bool IsConnected => _isConnected;

    public Task InitializeAsync(CancellationToken ct = default)
    {
#if IBAPI
        _logger.LogDebug("Initializing IB connection manager with IBAPI support");
        _bridge = new IBPluginBridge(_host, _port, _clientId, _onTrade, _onQuote, _onDepth, _logger);
        _bridge.ConnectionLost += () =>
        {
            _isConnected = false;
            _onConnectionStateChanged(false, "Connection lost");
        };
        _bridge.ConnectionRestored += () =>
        {
            _isConnected = true;
            _onConnectionStateChanged(true, "Connection restored");
        };
        return Task.CompletedTask;
#else
        _logger.LogWarning("IB plugin initialized without IBAPI support. Real IB connectivity not available.");
        return Task.CompletedTask;
#endif
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
#if IBAPI
        if (_bridge == null)
            throw new InvalidOperationException("Connection manager not initialized");

        if (_isConnected)
            return;

        _logger.LogInformation("Connecting to IB Gateway at {Host}:{Port}", _host, _port);

        await _bridge.ConnectAsync(ct).ConfigureAwait(false);
        _isConnected = true;
        _onConnectionStateChanged(true, null);

        _logger.LogInformation("Connected to IB Gateway");
#else
        await Task.CompletedTask;
        _logger.LogWarning("ConnectAsync called but IBAPI not available. Using stub mode.");
        _isConnected = true;
        _onConnectionStateChanged(true, "Stub mode - no real IB connection");
#endif
    }

    public async Task DisconnectAsync()
    {
#if IBAPI
        if (_bridge != null && _isConnected)
        {
            _logger.LogInformation("Disconnecting from IB Gateway");
            await _bridge.DisconnectAsync().ConfigureAwait(false);
            _isConnected = false;
            _onConnectionStateChanged(false, "Disconnected");
        }
#else
        await Task.CompletedTask;
        _isConnected = false;
#endif
    }

    public void Subscribe(string symbol, IReadOnlyList<DataType> dataTypes)
    {
#if IBAPI
        if (_bridge == null || !_isConnected)
        {
            _logger.LogWarning("Cannot subscribe to {Symbol}: not connected", symbol);
            return;
        }

        lock (_subscriptionLock)
        {
            if (!_symbolSubscriptions.ContainsKey(symbol))
            {
                _symbolSubscriptions[symbol] = new List<int>();
            }

            foreach (var dataType in dataTypes)
            {
                var subId = _bridge.Subscribe(symbol, dataType);
                if (subId >= 0)
                {
                    _symbolSubscriptions[symbol].Add(subId);
                    _logger.LogDebug("Subscribed to {DataType} for {Symbol} (id={SubId})", dataType, symbol, subId);
                }
            }
        }
#else
        _logger.LogDebug("Subscribe called for {Symbol} in stub mode", symbol);
#endif
    }

    public void Unsubscribe(string symbol)
    {
#if IBAPI
        if (_bridge == null)
            return;

        lock (_subscriptionLock)
        {
            if (_symbolSubscriptions.TryGetValue(symbol, out var subIds))
            {
                foreach (var subId in subIds)
                {
                    _bridge.Unsubscribe(subId);
                }
                _symbolSubscriptions.Remove(symbol);
                _logger.LogDebug("Unsubscribed from {Symbol}", symbol);
            }
        }
#else
        _logger.LogDebug("Unsubscribe called for {Symbol} in stub mode", symbol);
#endif
    }

    public async Task<IReadOnlyList<BarEvent>> GetHistoricalBarsAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        string interval,
        bool adjusted,
        CancellationToken ct = default)
    {
#if IBAPI
        if (_bridge == null)
            throw new InvalidOperationException("Connection manager not initialized");

        return await _bridge.GetHistoricalBarsAsync(symbol, from, to, interval, adjusted, ct).ConfigureAwait(false);
#else
        await Task.CompletedTask;
        _logger.LogWarning("GetHistoricalBarsAsync called but IBAPI not available. Returning empty.");
        return Array.Empty<BarEvent>();
#endif
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

#if IBAPI
        _bridge?.Dispose();
#endif
    }
}

#if IBAPI
using System.Collections.Concurrent;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;

/// <summary>
/// Bridge between the plugin system and the existing IB infrastructure.
/// Creates lightweight collectors that emit plugin events.
/// </summary>
internal sealed class IBPluginBridge : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _clientId;
    private readonly ILogger _logger;

    // Plugin event callbacks
    private readonly Action<string, decimal, decimal, DateTimeOffset, string?> _onTrade;
    private readonly Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> _onQuote;
    private readonly Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> _onDepth;

    // IB infrastructure
    private EnhancedIBConnectionManager? _connection;
    private PluginEventCollector? _collector;

    // Subscription tracking
    private readonly ConcurrentDictionary<int, (string Symbol, DataType Type)> _subscriptions = new();
    private int _nextSubId = 200000;

    public event Action? ConnectionLost;
    public event Action? ConnectionRestored;

    public IBPluginBridge(
        string host,
        int port,
        int clientId,
        Action<string, decimal, decimal, DateTimeOffset, string?> onTrade,
        Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> onQuote,
        Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> onDepth,
        ILogger logger)
    {
        _host = host;
        _port = port;
        _clientId = clientId;
        _onTrade = onTrade;
        _onQuote = onQuote;
        _onDepth = onDepth;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        // Create the collector that forwards events to the plugin
        _collector = new PluginEventCollector(_onTrade, _onQuote, _onDepth);

        // Create the IB callback router that uses our collector
        var router = new IBCallbackRouter(_collector.DepthCollector, _collector.TradeCollector, _collector.QuoteCollector);

        // Create the connection
        _connection = new EnhancedIBConnectionManager(router, _host, _port, _clientId);

        _connection.ConnectionLost += (s, e) => ConnectionLost?.Invoke();
        _connection.ConnectionRestored += (s, e) => ConnectionRestored?.Invoke();

        await _connection.ConnectAsync().ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.DisconnectAsync().ConfigureAwait(false);
        }
    }

    public int Subscribe(string symbol, DataType dataType)
    {
        if (_connection == null)
            return -1;

        var config = CreateSymbolConfig(symbol);
        var subId = Interlocked.Increment(ref _nextSubId);

        switch (dataType)
        {
            case DataType.Trade:
                var tradeId = _connection.SubscribeTrades(config);
                _subscriptions[tradeId] = (symbol, dataType);
                return tradeId;

            case DataType.Quote:
                var quoteId = _connection.SubscribeQuotes(config);
                _subscriptions[quoteId] = (symbol, dataType);
                return quoteId;

            case DataType.Depth:
                var depthId = _connection.SubscribeMarketDepth(config);
                _subscriptions[depthId] = (symbol, dataType);
                return depthId;

            default:
                return -1;
        }
    }

    public void Unsubscribe(int subscriptionId)
    {
        if (_connection == null)
            return;

        if (_subscriptions.TryRemove(subscriptionId, out var sub))
        {
            switch (sub.Type)
            {
                case DataType.Trade:
                    _connection.UnsubscribeTrades(subscriptionId);
                    break;
                case DataType.Quote:
                    _connection.UnsubscribeQuotes(subscriptionId);
                    break;
                case DataType.Depth:
                    _connection.UnsubscribeMarketDepth(subscriptionId);
                    break;
            }
        }
    }

    public async Task<IReadOnlyList<BarEvent>> GetHistoricalBarsAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        string interval,
        bool adjusted,
        CancellationToken ct)
    {
        if (_connection == null)
            return Array.Empty<BarEvent>();

        var barSize = interval switch
        {
            "1min" => IBBarSizes.Min1,
            "5min" => IBBarSizes.Min5,
            "15min" => IBBarSizes.Min15,
            "30min" => IBBarSizes.Min30,
            "1hour" => IBBarSizes.Hour1,
            "1day" => IBBarSizes.Day1,
            _ => IBBarSizes.Day1
        };

        var whatToShow = adjusted ? IBWhatToShow.AdjustedLast : IBWhatToShow.Trades;
        var config = CreateSymbolConfig(symbol);
        var endDateTime = to.ToDateTime(new TimeOnly(23, 59, 59)).ToString("yyyyMMdd-HH:mm:ss");
        var duration = $"{Math.Max(1, to.DayNumber - from.DayNumber + 1)} D";

        var ibBars = await _connection.RequestHistoricalDataAsync(
            config, endDateTime, duration, barSize, whatToShow, useRTH: true, ct).ConfigureAwait(false);

        return ibBars.Select(bar => new BarEvent
        {
            Symbol = symbol,
            Timestamp = bar.Time,
            Source = "ib",
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
            Interval = interval,
            IsAdjusted = adjusted
        }).ToList();
    }

    private static SymbolConfig CreateSymbolConfig(string symbol)
    {
        var secType = "STK";
        var exchange = "SMART";
        var currency = "USD";

        // Handle futures
        if (symbol.Length >= 3 && char.IsDigit(symbol[^1]) &&
            "FGHJKMNQUVXZ".Contains(symbol[^2], StringComparison.OrdinalIgnoreCase))
        {
            secType = "FUT";
            exchange = "CME";
        }

        // Handle forex
        if (symbol.Contains('.') && symbol.Length == 7)
        {
            secType = "CASH";
            exchange = "IDEALPRO";
            var parts = symbol.Split('.');
            currency = parts[1];
        }

        return new SymbolConfig
        {
            Symbol = symbol.Split('-')[0].Split('.')[0],
            SecurityType = secType,
            Exchange = exchange,
            Currency = currency
        };
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

/// <summary>
/// Lightweight collectors that forward domain events to plugin event callbacks.
/// These replace the standard collectors when running in plugin mode.
/// </summary>
internal sealed class PluginEventCollector
{
    private readonly Action<string, decimal, decimal, DateTimeOffset, string?> _onTrade;
    private readonly Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> _onQuote;
    private readonly Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> _onDepth;

    public TradeDataCollector TradeCollector { get; }
    public MarketDepthCollector DepthCollector { get; }
    public QuoteCollector QuoteCollector { get; }

    public PluginEventCollector(
        Action<string, decimal, decimal, DateTimeOffset, string?> onTrade,
        Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> onQuote,
        Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> onDepth)
    {
        _onTrade = onTrade;
        _onQuote = onQuote;
        _onDepth = onDepth;

        // Create a dummy publisher that we'll intercept
        var publisher = new PluginEventPublisher(onTrade, onQuote, onDepth);

        // Create collectors with our intercepting publisher
        TradeCollector = new TradeDataCollector(publisher);
        DepthCollector = new MarketDepthCollector(publisher, depthLevels: 10);
        QuoteCollector = new QuoteCollector(publisher);
    }
}

/// <summary>
/// Publisher that converts domain events to plugin events.
/// </summary>
internal sealed class PluginEventPublisher : IMarketEventPublisher
{
    private readonly Action<string, decimal, decimal, DateTimeOffset, string?> _onTrade;
    private readonly Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> _onQuote;
    private readonly Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> _onDepth;

    public PluginEventPublisher(
        Action<string, decimal, decimal, DateTimeOffset, string?> onTrade,
        Action<string, decimal, decimal, decimal, decimal, DateTimeOffset> onQuote,
        Action<string, IReadOnlyList<DepthLevel>, IReadOnlyList<DepthLevel>, DateTimeOffset, bool> onDepth)
    {
        _onTrade = onTrade;
        _onQuote = onQuote;
        _onDepth = onDepth;
    }

    public ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
    {
        switch (evt.Type)
        {
            case MarketEventType.Trade when evt.Payload is Trade trade:
                _onTrade(evt.Symbol, trade.Price, trade.Size, evt.Timestamp, trade.Venue);
                break;

            case MarketEventType.BboQuote when evt.Payload is BboQuote quote:
                _onQuote(evt.Symbol, quote.BidPrice, (decimal)quote.BidSize, quote.AskPrice, (decimal)quote.AskSize, evt.Timestamp);
                break;

            case MarketEventType.L2Snapshot when evt.Payload is LOBSnapshot lob:
                var bids = lob.Bids.Select(l => new DepthLevel(l.Price, l.Size, l.OrderCount)).ToList();
                var asks = lob.Asks.Select(l => new DepthLevel(l.Price, l.Size, l.OrderCount)).ToList();
                _onDepth(evt.Symbol, bids, asks, evt.Timestamp, true);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
#endif
