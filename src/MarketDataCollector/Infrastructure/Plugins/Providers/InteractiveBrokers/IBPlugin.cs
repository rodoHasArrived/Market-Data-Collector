using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.InteractiveBrokers;

/// <summary>
/// Interactive Brokers plugin for real-time and historical market data.
///
/// This plugin wraps the existing IB infrastructure to provide a unified interface.
/// It supports both real-time streaming (via TWS/Gateway WebSocket) and historical
/// backfill (via IB API).
///
/// Configuration (via environment variables):
///   IB__HOST - TWS/Gateway host (default: 127.0.0.1)
///   IB__PORT - TWS/Gateway port (default: 7497 for paper, 7496 for live)
///   IB__CLIENT_ID - Client ID for connection (default: 1)
///   IB__ACCOUNT - Account ID (optional)
///
/// Note: Requires TWS or IB Gateway to be running with API access enabled.
/// The IB API DLL must be present for full functionality (IBAPI compile flag).
/// </summary>
[MarketDataPlugin(
    id: "ib",
    displayName: "Interactive Brokers",
    type: PluginType.Hybrid,
    Category = PluginCategory.Broker,
    Priority = 5,
    Description = "Real-time streaming and historical data via TWS/IB Gateway",
    Author = "Market Data Collector",
    ConfigPrefix = "IB",
    Version = "2.0.0")]
public sealed class IBPlugin : MarketDataPluginBase
{
    private string _host = "127.0.0.1";
    private int _port = 7497;
    private int _clientId = 1;

    private readonly Channel<MarketDataEvent> _eventChannel;
    private IBPluginConnectionManager? _connectionManager;
    private CancellationTokenSource? _connectionCts;

    private readonly HashSet<string> _subscribedSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _subscriptionLock = new();

    public IBPlugin()
    {
        _eventChannel = Channel.CreateBounded<MarketDataEvent>(
            new BoundedChannelOptions(50_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });
    }

    #region Identity

    public override string Id => "ib";
    public override string DisplayName => "Interactive Brokers";
    public override string Description => "Real-time streaming and historical data via TWS/IB Gateway";
    public override Version Version => new(2, 0, 0);

    #endregion

    #region Capabilities

    public override PluginCapabilities Capabilities => new()
    {
        SupportsRealtime = true,
        SupportsHistorical = true,
        SupportsTrades = true,
        SupportsQuotes = true,
        SupportsDepth = true, // L2 market depth supported
        SupportsBars = true,
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true,
        MaxHistoricalLookback = TimeSpan.FromDays(365 * 20), // ~20 years
        SupportedBarIntervals = ["1min", "5min", "15min", "30min", "1hour", "1day"],
        SupportedAssetClasses = new HashSet<AssetClass>
        {
            AssetClass.Equity,
            AssetClass.Option,
            AssetClass.Future,
            AssetClass.Forex,
            AssetClass.Index,
            AssetClass.ETF,
            AssetClass.Bond
        },
        SupportedMarkets = new HashSet<string> { "US", "EU", "UK", "DE", "FR", "APAC", "JP", "HK", "AU" },
        MaxSymbolsPerRequest = 100, // IB limit varies by subscription
        RateLimit = new RateLimitPolicy
        {
            // IB historical: 60 requests per 10 minutes
            MaxRequests = 60,
            Window = TimeSpan.FromMinutes(10),
            BurstAllowance = 0
        }
    };

    #endregion

    #region Lifecycle

    protected override void ValidateConfiguration(IPluginConfig config)
    {
        _host = config.Get("host", "127.0.0.1")!;
        _port = config.Get("port", 7497);
        _clientId = config.Get("client_id", 1);

        Logger.LogInformation(
            "IB plugin configured: Host={Host}, Port={Port}, ClientId={ClientId}",
            _host, _port, _clientId);
    }

    protected override async Task OnInitializeAsync(IPluginConfig config, CancellationToken ct)
    {
        _connectionCts = new CancellationTokenSource();

        // Create the connection manager (handles IBAPI conditional compilation)
        _connectionManager = new IBPluginConnectionManager(
            _host, _port, _clientId,
            OnTradeReceived,
            OnQuoteReceived,
            OnDepthReceived,
            OnConnectionStateChanged,
            Logger);

        await _connectionManager.InitializeAsync(ct).ConfigureAwait(false);
    }

    protected override ILogger CreateLogger()
    {
        // In production, inject ILoggerFactory via DI
        return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    #endregion

    #region Data Streaming

    public override async IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (_connectionManager == null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call InitializeAsync first.");
        }

        // Historical request
        if (request.IsHistorical)
        {
            await foreach (var evt in StreamHistoricalAsync(request, ct).ConfigureAwait(false))
            {
                yield return evt;
            }
            yield break;
        }

        // Real-time streaming
        await foreach (var evt in StreamRealtimeAsync(request, ct).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<MarketDataEvent> StreamRealtimeAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Ensure connected
        await _connectionManager!.ConnectAsync(ct).ConfigureAwait(false);

        // Subscribe to symbols
        lock (_subscriptionLock)
        {
            foreach (var symbol in request.Symbols)
            {
                if (_subscribedSymbols.Add(symbol))
                {
                    _connectionManager.Subscribe(symbol, request.DataTypes);
                }
            }
        }

        State = PluginState.Streaming;

        try
        {
            // Stream events from channel
            await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                // Filter to requested symbols and data types
                if (!request.Symbols.Contains(evt.Symbol))
                    continue;

                if (request.DataTypes.Count > 0 && !request.DataTypes.Contains(evt.EventType))
                    continue;

                yield return evt;
            }
        }
        finally
        {
            // Unsubscribe on completion
            lock (_subscriptionLock)
            {
                foreach (var symbol in request.Symbols)
                {
                    if (_subscribedSymbols.Remove(symbol))
                    {
                        _connectionManager.Unsubscribe(symbol);
                    }
                }
            }

            State = PluginState.Ready;
        }
    }

    private async IAsyncEnumerable<MarketDataEvent> StreamHistoricalAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var from = request.From!.Value;
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var interval = request.BarInterval ?? "1day";

        State = PluginState.Streaming;

        foreach (var symbol in request.Symbols)
        {
            ct.ThrowIfCancellationRequested();

            Logger.LogDebug("Fetching IB historical data for {Symbol} from {From} to {To}",
                symbol, from, to);

            IReadOnlyList<BarEvent>? bars = null;

            try
            {
                bars = await _connectionManager!.GetHistoricalBarsAsync(
                    symbol, from, to, interval, request.AdjustedPrices, ct).ConfigureAwait(false);

                RecordSuccess();
            }
            catch (Exception ex)
            {
                RecordFailure($"Historical request failed: {ex.Message}");
                Logger.LogWarning(ex, "Failed to fetch historical data for {Symbol}", symbol);

                // Yield error event
                yield return new ErrorEvent
                {
                    Symbol = symbol,
                    Timestamp = DateTimeOffset.UtcNow,
                    Source = Id,
                    ErrorMessage = ex.Message,
                    IsRecoverable = true
                };
                continue;
            }

            if (bars != null)
            {
                foreach (var bar in bars)
                {
                    yield return bar;
                }
            }
        }

        State = PluginState.Ready;
    }

    #endregion

    #region Event Callbacks

    private void OnTradeReceived(string symbol, decimal price, decimal size, DateTimeOffset timestamp, string? exchange)
    {
        var trade = new TradeEvent
        {
            Symbol = symbol,
            Timestamp = timestamp,
            Source = Id,
            Price = price,
            Size = size,
            Exchange = exchange
        };

        _eventChannel.Writer.TryWrite(trade);
    }

    private void OnQuoteReceived(string symbol, decimal bidPrice, decimal bidSize, decimal askPrice, decimal askSize, DateTimeOffset timestamp)
    {
        var quote = new QuoteEvent
        {
            Symbol = symbol,
            Timestamp = timestamp,
            Source = Id,
            BidPrice = bidPrice,
            BidSize = bidSize,
            AskPrice = askPrice,
            AskSize = askSize
        };

        _eventChannel.Writer.TryWrite(quote);
    }

    private void OnDepthReceived(string symbol, IReadOnlyList<DepthLevel> bids, IReadOnlyList<DepthLevel> asks, DateTimeOffset timestamp, bool isSnapshot)
    {
        var depth = new DepthEvent
        {
            Symbol = symbol,
            Timestamp = timestamp,
            Source = Id,
            Bids = bids,
            Asks = asks,
            IsSnapshot = isSnapshot
        };

        _eventChannel.Writer.TryWrite(depth);
    }

    private void OnConnectionStateChanged(bool isConnected, string? message)
    {
        if (isConnected)
        {
            RecordSuccess();
            Logger.LogInformation("IB connection established");
        }
        else
        {
            RecordFailure(message ?? "Connection lost");
            Logger.LogWarning("IB connection lost: {Message}", message);
        }
    }

    #endregion

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _connectionCts?.Cancel();

        if (_connectionManager != null)
        {
            await _connectionManager.DisconnectAsync().ConfigureAwait(false);
            _connectionManager.Dispose();
        }

        _eventChannel.Writer.Complete();
        _connectionCts?.Dispose();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}
