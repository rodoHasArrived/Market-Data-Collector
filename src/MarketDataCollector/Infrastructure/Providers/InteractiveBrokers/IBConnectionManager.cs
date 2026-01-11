namespace MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;

/// <summary>
/// Minimal connection manager placeholder (buildable without IBApi reference).
/// Use <see cref="EnhancedIBConnectionManager"/> when compiling with the official IB API.
/// </summary>
// TODO: Implement full IB connection management with TWS/Gateway API integration
// This stub allows the project to compile without the IB API reference
public sealed class IBConnectionManager
{
    public bool IsConnected { get; private set; }

    public Task ConnectAsync()
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }
}
