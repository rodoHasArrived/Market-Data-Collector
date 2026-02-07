namespace MarketDataCollector.Application.Exceptions;

/// <summary>
/// Base exception for all MarketDataCollector-specific exceptions
/// </summary>
public class MarketDataCollectorException : Exception
{
    public MarketDataCollectorException()
    {
    }

    public MarketDataCollectorException(string message) : base(message)
    {
    }

    public MarketDataCollectorException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
