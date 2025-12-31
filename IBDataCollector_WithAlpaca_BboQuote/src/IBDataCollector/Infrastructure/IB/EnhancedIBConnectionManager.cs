using System.Collections.Concurrent;

namespace IBDataCollector.Infrastructure.IB;

/// <summary>
/// IB connection manager that owns the socket/EReader loop and forwards raw callbacks into <see cref="IBCallbackRouter"/>.
/// 
/// This file is buildable out-of-the-box:
/// - When compiled WITHOUT the official IB API reference, it exposes a small stub implementation.
/// - When compiled WITH the IB API (define the compilation constant IBAPI and reference IBApi),
///   it provides a full EWrapper implementation with depth routing.
/// </summary>
public sealed partial class EnhancedIBConnectionManager
{
#if !IBAPI
    private readonly IBCallbackRouter _router;

    public EnhancedIBConnectionManager(IBCallbackRouter router)
    {
        _router = router;
    }

    public bool IsConnected => false;

    public Task ConnectAsync() => throw new NotSupportedException("Build with IBAPI defined and reference the official IBApi package/dll.");
    public Task DisconnectAsync() => Task.CompletedTask;

    public int SubscribeMarketDepth(string symbol, int depthLevels = 10) => throw new NotSupportedException("Build with IBAPI defined.");

    public int SubscribeMarketDepth(IBDataCollector.Application.Config.SymbolConfig cfg, bool smartDepth = true)
        => throw new NotSupportedException("Build with IBAPI defined.");

    public int SubscribeTrades(IBDataCollector.Application.Config.SymbolConfig cfg)
        => throw new NotSupportedException("Build with IBAPI defined.");

    public void UnsubscribeTrades(int tickerId)
        => throw new NotSupportedException("Build with IBAPI defined.");
    public void UnsubscribeMarketDepth(int tickerId) => throw new NotSupportedException("Build with IBAPI defined.");
#endif
}
