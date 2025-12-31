namespace IBDataCollector.Infrastructure.IB;

/// <summary>
/// Minimal connection manager placeholder (buildable without IBApi reference).
/// Use <see cref="EnhancedIBConnectionManager"/> when compiling with the official IB API.
/// </summary>
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
