using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Serilog;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.StockSharp;

/// <summary>
/// StockSharp (S#) market data plugin supporting multiple connector types.
/// Wraps the StockSharp connector framework for unified market data access.
/// Supports Rithmic, IQFeed, CQG, and Interactive Brokers adapters.
/// </summary>
[MarketDataPlugin(
    id: "stocksharp",
    displayName: "StockSharp",
    type: PluginType.Realtime,
    Category = PluginCategory.Framework,
    Priority = 8)]
public sealed class StockSharpPlugin : MarketDataPluginBase
{
    private readonly Channel<MarketDataEvent> _eventChannel;
    private readonly StockSharpPluginBridge _bridge;

    private string _connectorType = "Rithmic";
    private bool _isConnected;
    private CancellationTokenSource? _connectionCts;

    public override string Id => "stocksharp";
    public override string DisplayName => "StockSharp";
    public override string Description => "Multi-connector market data via StockSharp framework";
    public override string Version => "1.0.0";

    public override PluginCapabilities Capabilities { get; protected set; } = new()
    {
        SupportsRealtime = true,
        SupportsHistorical = false, // Historical via separate plugins
        SupportsTrades = true,
        SupportsQuotes = true,
        SupportsDepth = true,
        SupportsBars = false,
        SupportedMarkets = new[] { "US", "EU", "ASIA" },
        SupportedAssetClasses = new[] { "Equity", "Future", "Option", "Forex" },
        MaxSymbolsPerSubscription = 500,
        RequiresAuthentication = true
    };

    public StockSharpPlugin() : base(Log.ForContext<StockSharpPlugin>())
    {
        _eventChannel = Channel.CreateBounded<MarketDataEvent>(
            new BoundedChannelOptions(50_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        _bridge = new StockSharpPluginBridge(Logger, OnEventReceived);
    }

    protected override async Task OnInitializeAsync(IPluginConfig config, CancellationToken ct)
    {
        _connectorType = config.Get("STOCKSHARP__CONNECTOR_TYPE", "Rithmic");

        var bridgeConfig = new StockSharpBridgeConfig
        {
            ConnectorType = _connectorType,
            // Rithmic settings
            RithmicServer = config.Get("STOCKSHARP__RITHMIC_SERVER", "Rithmic Test"),
            RithmicUserName = config.Get("STOCKSHARP__RITHMIC_USERNAME", string.Empty),
            RithmicPassword = config.Get("STOCKSHARP__RITHMIC_PASSWORD", string.Empty),
            RithmicCertFile = config.Get("STOCKSHARP__RITHMIC_CERT_FILE", string.Empty),
            // IQFeed settings
            IQFeedHost = config.Get("STOCKSHARP__IQFEED_HOST", "127.0.0.1"),
            IQFeedLevel1Port = config.Get("STOCKSHARP__IQFEED_L1_PORT", 9100),
            IQFeedLevel2Port = config.Get("STOCKSHARP__IQFEED_L2_PORT", 9200),
            IQFeedProductId = config.Get("STOCKSHARP__IQFEED_PRODUCT_ID", string.Empty),
            // CQG settings
            CQGUserName = config.Get("STOCKSHARP__CQG_USERNAME", string.Empty),
            CQGPassword = config.Get("STOCKSHARP__CQG_PASSWORD", string.Empty),
            CQGUseDemoServer = config.Get("STOCKSHARP__CQG_USE_DEMO", true),
            // IB settings
            IBHost = config.Get("STOCKSHARP__IB_HOST", "127.0.0.1"),
            IBPort = config.Get("STOCKSHARP__IB_PORT", 7497),
            IBClientId = config.Get("STOCKSHARP__IB_CLIENT_ID", 1)
        };

        await _bridge.InitializeAsync(bridgeConfig, ct);

        Logger.Information("StockSharp plugin initialized with connector type: {ConnectorType}", _connectorType);
    }

    public override async IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_isConnected)
        {
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await ConnectAsync(_connectionCts.Token);
        }

        // Subscribe to requested symbols
        foreach (var symbol in request.Symbols)
        {
            switch (request.Type)
            {
                case DataType.Trade:
                    _bridge.SubscribeTrades(symbol);
                    break;
                case DataType.Quote:
                    _bridge.SubscribeLevel1(symbol);
                    break;
                case DataType.Depth:
                    _bridge.SubscribeDepth(symbol, request.DepthLevels);
                    break;
                default:
                    // Subscribe to all data types
                    _bridge.SubscribeTrades(symbol);
                    _bridge.SubscribeLevel1(symbol);
                    _bridge.SubscribeDepth(symbol, request.DepthLevels);
                    break;
            }
        }

        Logger.Information("Subscribed to {Count} symbols via StockSharp", request.Symbols.Length);

        // Stream events from channel
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        try
        {
            await _bridge.ConnectAsync(ct);
            _isConnected = true;
            RecordHealth(true);
            Logger.Information("StockSharp connector connected successfully");
        }
        catch (Exception ex)
        {
            RecordHealth(false, ex.Message);
            Logger.Error(ex, "Failed to connect StockSharp connector");
            throw;
        }
    }

    private void OnEventReceived(MarketDataEvent evt)
    {
        // Write to bounded channel (drops oldest if full)
        _eventChannel.Writer.TryWrite(evt);
    }

    protected override async ValueTask OnDisposeAsync()
    {
        _connectionCts?.Cancel();
        _connectionCts?.Dispose();

        _eventChannel.Writer.Complete();
        await _bridge.DisposeAsync();
    }
}

/// <summary>
/// Configuration for the StockSharp bridge.
/// </summary>
public sealed class StockSharpBridgeConfig
{
    public string ConnectorType { get; set; } = "Rithmic";

    // Rithmic
    public string RithmicServer { get; set; } = string.Empty;
    public string RithmicUserName { get; set; } = string.Empty;
    public string RithmicPassword { get; set; } = string.Empty;
    public string RithmicCertFile { get; set; } = string.Empty;

    // IQFeed
    public string IQFeedHost { get; set; } = "127.0.0.1";
    public int IQFeedLevel1Port { get; set; } = 9100;
    public int IQFeedLevel2Port { get; set; } = 9200;
    public string IQFeedProductId { get; set; } = string.Empty;

    // CQG
    public string CQGUserName { get; set; } = string.Empty;
    public string CQGPassword { get; set; } = string.Empty;
    public bool CQGUseDemoServer { get; set; } = true;

    // Interactive Brokers
    public string IBHost { get; set; } = "127.0.0.1";
    public int IBPort { get; set; } = 7497;
    public int IBClientId { get; set; } = 1;
}

/// <summary>
/// Bridge to the StockSharp connector infrastructure.
/// Handles conditional compilation for optional StockSharp dependency.
/// </summary>
public sealed class StockSharpPluginBridge : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Action<MarketDataEvent> _onEvent;

#if STOCKSHARP
    private StockSharp.Algo.Connector? _connector;
    private readonly Dictionary<string, StockSharp.BusinessEntities.Security> _securities = new();
#endif

    private StockSharpBridgeConfig? _config;
    private bool _disposed;

    public StockSharpPluginBridge(ILogger logger, Action<MarketDataEvent> onEvent)
    {
        _logger = logger;
        _onEvent = onEvent;
    }

    public Task InitializeAsync(StockSharpBridgeConfig config, CancellationToken ct)
    {
        _config = config;

#if STOCKSHARP
        _logger.Information("StockSharp bridge initializing with {ConnectorType}", config.ConnectorType);
        _connector = CreateConnector(config);
        SetupEventHandlers();
#else
        _logger.Warning("StockSharp not available - running in stub mode. Define STOCKSHARP compilation symbol to enable.");
#endif

        return Task.CompletedTask;
    }

    public Task ConnectAsync(CancellationToken ct)
    {
#if STOCKSHARP
        if (_connector == null)
            throw new InvalidOperationException("Connector not initialized");

        var tcs = new TaskCompletionSource();

        _connector.Connected += () => tcs.TrySetResult();
        _connector.ConnectionError += ex => tcs.TrySetException(ex);

        using var registration = ct.Register(() => tcs.TrySetCanceled());

        _connector.Connect();
        return tcs.Task;
#else
        _logger.Information("StockSharp stub mode: simulating connection");
        return Task.CompletedTask;
#endif
    }

    public void SubscribeTrades(string symbol)
    {
#if STOCKSHARP
        var security = GetOrCreateSecurity(symbol);
        _connector?.RegisterTrades(security);
        _logger.Debug("Subscribed to trades for {Symbol}", symbol);
#else
        _logger.Debug("Stub mode: would subscribe to trades for {Symbol}", symbol);
        // Emit synthetic event for testing
        _onEvent(MarketDataEvent.Status(symbol, "subscribed", "trades"));
#endif
    }

    public void SubscribeLevel1(string symbol)
    {
#if STOCKSHARP
        var security = GetOrCreateSecurity(symbol);
        _connector?.RegisterSecurity(security);
        _logger.Debug("Subscribed to level1 for {Symbol}", symbol);
#else
        _logger.Debug("Stub mode: would subscribe to level1 for {Symbol}", symbol);
        _onEvent(MarketDataEvent.Status(symbol, "subscribed", "level1"));
#endif
    }

    public void SubscribeDepth(string symbol, int levels)
    {
#if STOCKSHARP
        var security = GetOrCreateSecurity(symbol);
        _connector?.RegisterMarketDepth(security);
        _logger.Debug("Subscribed to depth for {Symbol} ({Levels} levels)", symbol, levels);
#else
        _logger.Debug("Stub mode: would subscribe to depth for {Symbol} ({Levels} levels)", symbol, levels);
        _onEvent(MarketDataEvent.Status(symbol, "subscribed", $"depth:{levels}"));
#endif
    }

    public void Unsubscribe(string symbol)
    {
#if STOCKSHARP
        if (_securities.TryGetValue(symbol, out var security))
        {
            _connector?.UnRegisterTrades(security);
            _connector?.UnRegisterSecurity(security);
            _connector?.UnRegisterMarketDepth(security);
        }
#endif
    }

#if STOCKSHARP
    private StockSharp.Algo.Connector CreateConnector(StockSharpBridgeConfig config)
    {
        var connector = new StockSharp.Algo.Connector();

        switch (config.ConnectorType.ToLowerInvariant())
        {
            case "rithmic":
                CreateRithmicAdapter(connector, config);
                break;
            case "iqfeed":
                CreateIQFeedAdapter(connector, config);
                break;
            case "cqg":
                CreateCQGAdapter(connector, config);
                break;
            case "interactivebrokers":
            case "ib":
                CreateIBAdapter(connector, config);
                break;
            default:
                throw new ArgumentException($"Unknown connector type: {config.ConnectorType}");
        }

        return connector;
    }

    private void CreateRithmicAdapter(StockSharp.Algo.Connector connector, StockSharpBridgeConfig config)
    {
#if STOCKSHARP_RITHMIC
        var adapter = new StockSharp.Rithmic.RithmicMessageAdapter(connector.TransactionIdGenerator)
        {
            Server = config.RithmicServer,
            UserName = config.RithmicUserName,
            Password = config.RithmicPassword.ToSecureString(),
            CertFile = config.RithmicCertFile
        };
        connector.Adapter.InnerAdapters.Add(adapter);
#else
        throw new InvalidOperationException("Rithmic adapter not available. Define STOCKSHARP_RITHMIC compilation symbol.");
#endif
    }

    private void CreateIQFeedAdapter(StockSharp.Algo.Connector connector, StockSharpBridgeConfig config)
    {
#if STOCKSHARP_IQFEED
        var adapter = new StockSharp.IQFeed.IQFeedMessageAdapter(connector.TransactionIdGenerator)
        {
            Address = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(config.IQFeedHost), config.IQFeedLevel1Port),
            Level2Address = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(config.IQFeedHost), config.IQFeedLevel2Port)
        };
        connector.Adapter.InnerAdapters.Add(adapter);
#else
        throw new InvalidOperationException("IQFeed adapter not available. Define STOCKSHARP_IQFEED compilation symbol.");
#endif
    }

    private void CreateCQGAdapter(StockSharp.Algo.Connector connector, StockSharpBridgeConfig config)
    {
#if STOCKSHARP_CQG
        var adapter = new StockSharp.Cqg.Com.CqgComMessageAdapter(connector.TransactionIdGenerator)
        {
            UserName = config.CQGUserName,
            Password = config.CQGPassword.ToSecureString()
        };
        connector.Adapter.InnerAdapters.Add(adapter);
#else
        throw new InvalidOperationException("CQG adapter not available. Define STOCKSHARP_CQG compilation symbol.");
#endif
    }

    private void CreateIBAdapter(StockSharp.Algo.Connector connector, StockSharpBridgeConfig config)
    {
#if STOCKSHARP_IB
        var adapter = new StockSharp.InteractiveBrokers.InteractiveBrokersMessageAdapter(connector.TransactionIdGenerator)
        {
            Address = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(config.IBHost), config.IBPort),
            ClientId = config.IBClientId
        };
        connector.Adapter.InnerAdapters.Add(adapter);
#else
        throw new InvalidOperationException("IB adapter not available. Define STOCKSHARP_IB compilation symbol.");
#endif
    }

    private void SetupEventHandlers()
    {
        if (_connector == null) return;

        _connector.NewTrade += trade =>
        {
            var evt = MarketDataEvent.Trade(
                symbol: trade.Security.Id,
                timestamp: trade.Time,
                price: trade.Price,
                size: trade.Volume,
                side: trade.OrderDirection switch
                {
                    StockSharp.Messages.Sides.Buy => TradeSide.Buy,
                    StockSharp.Messages.Sides.Sell => TradeSide.Sell,
                    _ => TradeSide.Unknown
                },
                exchange: trade.Security.Board?.Code
            );
            _onEvent(evt);
        };

        _connector.MarketDepthChanged += depth =>
        {
            foreach (var quote in depth.Bids.Concat(depth.Asks))
            {
                var evt = MarketDataEvent.DepthLevel(
                    symbol: depth.Security.Id,
                    timestamp: depth.LastChangeTime,
                    side: quote.OrderDirection == StockSharp.Messages.Sides.Buy ? OrderSide.Bid : OrderSide.Ask,
                    price: quote.Price,
                    size: quote.Volume,
                    position: 0 // StockSharp doesn't provide position directly
                );
                _onEvent(evt);
            }
        };

        _connector.ValuesChanged += (security, changes, serverTime, localTime) =>
        {
            decimal? bidPrice = null, bidSize = null, askPrice = null, askSize = null;

            foreach (var change in changes)
            {
                switch (change.Key)
                {
                    case StockSharp.Messages.Level1Fields.BestBidPrice:
                        bidPrice = (decimal)change.Value;
                        break;
                    case StockSharp.Messages.Level1Fields.BestBidVolume:
                        bidSize = (decimal)change.Value;
                        break;
                    case StockSharp.Messages.Level1Fields.BestAskPrice:
                        askPrice = (decimal)change.Value;
                        break;
                    case StockSharp.Messages.Level1Fields.BestAskVolume:
                        askSize = (decimal)change.Value;
                        break;
                }
            }

            if (bidPrice.HasValue && askPrice.HasValue)
            {
                var evt = MarketDataEvent.Quote(
                    symbol: security.Id,
                    timestamp: serverTime,
                    bidPrice: bidPrice.Value,
                    bidSize: bidSize ?? 0,
                    askPrice: askPrice.Value,
                    askSize: askSize ?? 0
                );
                _onEvent(evt);
            }
        };

        _connector.Error += ex =>
        {
            _logger.Error(ex, "StockSharp connector error");
            _onEvent(MarketDataEvent.Error("stocksharp", ex.Message, ex.GetType().Name));
        };
    }

    private StockSharp.BusinessEntities.Security GetOrCreateSecurity(string symbol)
    {
        if (!_securities.TryGetValue(symbol, out var security))
        {
            security = new StockSharp.BusinessEntities.Security { Id = symbol };
            _securities[symbol] = security;
        }
        return security;
    }
#endif

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

#if STOCKSHARP
        if (_connector != null)
        {
            _connector.Disconnect();
            _connector.Dispose();
            _connector = null;
        }
#endif

        await Task.CompletedTask;
    }
}
