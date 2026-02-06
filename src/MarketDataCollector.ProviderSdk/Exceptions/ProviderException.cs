namespace MarketDataCollector.ProviderSdk.Exceptions;

/// <summary>
/// Base exception for provider-related errors in the plugin system.
/// </summary>
public class ProviderException : Exception
{
    /// <summary>
    /// The provider that raised the error.
    /// </summary>
    public string? ProviderId { get; }

    /// <summary>
    /// The symbol being processed when the error occurred.
    /// </summary>
    public string? Symbol { get; }

    public ProviderException(string message) : base(message) { }

    public ProviderException(string message, string? providerId = null, string? symbol = null)
        : base(message)
    {
        ProviderId = providerId;
        Symbol = symbol;
    }

    public ProviderException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a provider's rate limit is exceeded.
/// </summary>
public sealed class ProviderRateLimitException : ProviderException
{
    /// <summary>
    /// Suggested time to wait before retrying.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public ProviderRateLimitException(string message) : base(message) { }

    public ProviderRateLimitException(
        string message,
        string? providerId = null,
        string? symbol = null,
        TimeSpan? retryAfter = null)
        : base(message, providerId, symbol)
    {
        RetryAfter = retryAfter;
    }

    public ProviderRateLimitException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a provider cannot establish or maintain a connection.
/// </summary>
public sealed class ProviderConnectionException : ProviderException
{
    /// <summary>
    /// The host that could not be reached.
    /// </summary>
    public string? Host { get; }

    public ProviderConnectionException(string message) : base(message) { }

    public ProviderConnectionException(
        string message,
        string? providerId = null,
        string? host = null)
        : base(message, providerId)
    {
        Host = host;
    }

    public ProviderConnectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
