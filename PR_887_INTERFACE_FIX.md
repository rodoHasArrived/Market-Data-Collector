# PR #887 Interface Alignment Fix

## Problem

The PR #887 introduced `ILoggingService` interface for dependency injection but there was a mismatch between the interface and platform implementations:

### Interface (Ui.Services/Contracts/ILoggingService.cs)
```csharp
public interface ILoggingService
{
    void Log(string message);
    void LogError(string message, Exception? exception = null);
    void LogWarning(string message);
    void LogDebug(string message);
}
```

### Platform Implementations
Both WPF and UWP had methods with `params` arguments:
- `LogInfo(string message, params (string key, string value)[] properties)`
- `LogWarning(string message, params (string key, string value)[] properties)`
- `LogDebug(string message, params (string key, string value)[] properties)`
- `LogError(string message, Exception? ex = null)`

The interface required `Log(string)` but implementations had `LogInfo(string, params...)`.

## Solution

Added explicit interface implementations to both WPF and UWP `LoggingService` classes:

### WPF (MarketDataCollector.Wpf/Services/LoggingService.cs)
```csharp
// Public method for Log
public void Log(string message)
{
    LogInfo(message);
}

// Explicit interface implementations
void ILoggingService.LogWarning(string message)
{
    LogWarning(message);
}

void ILoggingService.LogError(string message, Exception? exception)
{
    LogError(message, exception);
}

void ILoggingService.LogDebug(string message)
{
    LogDebug(message);
}
```

### UWP (MarketDataCollector.Uwp/Services/LoggingService.cs)
```csharp
void ILoggingService.Log(string message)
{
    LogInfo(message);
}

void ILoggingService.LogDebug(string message)
{
    LogDebug(message);
}

void ILoggingService.LogWarning(string message)
{
    LogWarning(message);
}

void ILoggingService.LogError(string message, Exception? exception)
{
    if (exception != null)
    {
        LogError(message, exception);
    }
    else
    {
        LogError(message);
    }
}
```

## Benefits

1. **Interface Compliance**: Both implementations now properly implement `ILoggingService`
2. **Backward Compatibility**: Existing platform code can continue using the `params` overloads
3. **DI Support**: Shared services can inject `ILoggingService` and call simple interface methods
4. **Clean API**: Explicit interface implementation keeps the public API surface clean

## Build Verification

✅ WPF build succeeds  
✅ UWP build succeeds  
✅ Full solution build succeeds

## Files Changed

- `src/MarketDataCollector.Wpf/Services/LoggingService.cs` (+40 lines)
- `src/MarketDataCollector.Uwp/Services/LoggingService.cs` (+41 lines)
