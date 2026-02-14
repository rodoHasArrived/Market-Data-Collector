namespace MarketDataCollector.Application.Exceptions;

/// <summary>
/// Exception thrown when there are issues with data providers
/// </summary>
public class DataProviderException : MarketDataCollectorException
{
    public string? Provider { get; }
    public string? Symbol { get; }

    public DataProviderException(string message) : base(message)
    {
    }

    public DataProviderException(string message, string? provider = null, string? symbol = null)
        : base(message)
    {
        Provider = provider;
        Symbol = symbol;
    }

    public DataProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DataProviderException(string message, Exception innerException, string? provider = null, string? symbol = null)
        : base(message, innerException)
    {
        Provider = provider;
        Symbol = symbol;
    }
}
