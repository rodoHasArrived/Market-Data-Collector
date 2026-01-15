namespace MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;

/// <summary>
/// No-op connection manager for builds without Interactive Brokers API reference.
/// </summary>
/// <remarks>
/// <para>
/// This is a stub implementation that allows the project to compile without the IB API
/// NuGet package. All connection methods are no-ops that immediately return success.
/// </para>
/// <para>
/// For actual IB connectivity, compile with the IBAPI symbol defined and use
/// <see cref="EnhancedIBConnectionManager"/> which implements the full EWrapper interface.
/// </para>
/// </remarks>
/// <seealso cref="EnhancedIBConnectionManager"/>
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
