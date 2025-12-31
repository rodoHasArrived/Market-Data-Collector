#if IBAPI
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using IBApi;
using MarketDataCollector.Application.Config;

namespace MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;

public sealed partial class EnhancedIBConnectionManager : EWrapper, IDisposable
{
    private readonly IBCallbackRouter _router;

    private readonly EReaderSignal _signal;
    private readonly EClientSocket _clientSocket;
    private EReader? _reader;

    private readonly CancellationTokenSource _cts = new();
    private Task? _readerLoop;

    private int _nextDepthTickerId = 10_000;
    private readonly ConcurrentDictionary<int, string> _depthTickerMap = new();

    private int _nextTradeTickerId = 20_000;
    private readonly ConcurrentDictionary<int, string> _tradeTickerMap = new();

    public EnhancedIBConnectionManager(IBCallbackRouter router, string host = "127.0.0.1", int port = 7497, int clientId = 1)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _signal = new EReaderMonitorSignal();
        _clientSocket = new EClientSocket(this, _signal);

        Host = host;
        Port = port;
        ClientId = clientId;
    }

    public string Host { get; }
    public int Port { get; }
    public int ClientId { get; }

    public bool IsConnected => _clientSocket.IsConnected();

    public Task ConnectAsync()
    {
        if (IsConnected) return Task.CompletedTask;

        _clientSocket.eConnect(Host, Port, ClientId);

        _reader = new EReader(_clientSocket, _signal);
        _reader.Start();

        _readerLoop = Task.Run(() => ReaderLoop(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _cts.Cancel();
            if (_readerLoop is not null) await _readerLoop.ConfigureAwait(false);
        }
        catch { /* ignore */ }

        if (IsConnected)
            _clientSocket.eDisconnect();
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsConnected)
            _clientSocket.eDisconnect();
        _cts.Dispose();
    }

    private void ReaderLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            _signal.waitForSignal();
            _reader?.processMsgs();
        }
    }

    // -----------------------
    // Depth subscriptions
    // -----------------------
    public int SubscribeMarketDepth(string symbol, Contract contract, int depthLevels = 10, bool smartDepth = true)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol required", nameof(symbol));
        if (contract is null) throw new ArgumentNullException(nameof(contract));

        var id = Interlocked.Increment(ref _nextDepthTickerId);
        _depthTickerMap[id] = symbol;

        // Router needs this mapping for callbacks
        _router.RegisterDepthTicker(id, symbol);

        _clientSocket.reqMktDepth(id, contract, depthLevels, smartDepth, null);
        return id;
    }

    /// <summary>
    /// Subscribe to L2 depth using a SymbolConfig (contract built via ContractFactory).
    /// </summary>
    public int SubscribeMarketDepth(SymbolConfig cfg, bool smartDepth = true)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var contract = ContractFactory.Create(cfg);
        var levels = cfg.DepthLevels <= 0 ? 10 : cfg.DepthLevels;
        return SubscribeMarketDepth(cfg.Symbol, contract, levels, smartDepth);
    }

    public void UnsubscribeMarketDepth(int tickerId)
    {
        _clientSocket.cancelMktDepth(tickerId);
        _depthTickerMap.TryRemove(tickerId, out _);
    }


    // -----------------------
    // Trade (tick-by-tick) subscriptions
    // -----------------------
    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        var contract = ContractFactory.Create(cfg);

        var id = Interlocked.Increment(ref _nextTradeTickerId);
        _tradeTickerMap[id] = cfg.Symbol;
        _router.RegisterTradeTicker(id, cfg.Symbol);

        // tickType can be "AllLast" (prints + special conditions) or "Last".
        _clientSocket.reqTickByTickData(id, contract, "AllLast", 0, ignoreSize: false);
        return id;
    }

    public void UnsubscribeTrades(int tickerId)
    {
        _clientSocket.cancelTickByTickData(tickerId);
        _tradeTickerMap.TryRemove(tickerId, out _);
    }

    // -----------------------
    // EWrapper depth callbacks
    // -----------------------
    public void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size)
    {
        _router.UpdateMktDepth(tickerId, position, operation, side, price, (double)size);
    }

    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth)
    {
        _router.UpdateMktDepthL2(tickerId, position, marketMaker, operation, side, price, (double)size, isSmartDepth);
    }


    public void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size, TickAttribLast tickAttribLast, string exchange, string specialConditions)
    {
        _router.OnTickByTickAllLast(reqId, tickType, time, price, (double)size, exchange, specialConditions);
    }

    // -----------------------
    // EWrapper required members (minimal passthrough stubs)
    // Expand as needed.
    // -----------------------
    public void error(Exception e) { }
    public void error(string str) { }
    public void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson) { }
    public void connectionClosed() { }

    // Many EWrapper methods exist; leave as no-ops until you wire the rest of your callbacks.
    public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { }
    public void tickSize(int tickerId, int field, decimal size) { }
    public void tickString(int tickerId, int field, string value) { }
    public void tickGeneric(int tickerId, int field, double value) { }
    public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { }
    public void tickSnapshotEnd(int tickerId) { }
    public void nextValidId(int orderId) { }
    public void managedAccounts(string accountsList) { }
    public void currentTime(long time) { }
    public void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public void accountSummaryEnd(int reqId) { }
    public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { }
    public void accountUpdateMultiEnd(int reqId) { }
    public void marketDataType(int reqId, int marketDataType) { }
    public void contractDetails(int reqId, ContractDetails contractDetails) { }
    public void contractDetailsEnd(int reqId) { }
    public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public void reqMktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { }
    public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public void newsProviders(NewsProvider[] newsProviders) { }
    public void newsArticle(int requestId, int articleType, string articleText) { }
    public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public void historicalNewsEnd(int requestId, bool hasMore) { }
    public void headTimestamp(int reqId, string headTimestamp) { }
    public void histoicalData(int reqId, Bar bar) { }
    public void historicalDataEnd(int reqId, string start, string end) { }

    // NOTE: The full EWrapper interface is extensive. Add methods as you need them for trades/ticks/orders.
}
#endif
