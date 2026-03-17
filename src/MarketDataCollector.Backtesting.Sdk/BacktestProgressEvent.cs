namespace MarketDataCollector.Backtesting.Sdk;

/// <summary>Progress notification emitted by <see cref="BacktestEngine"/> during replay.</summary>
public sealed record BacktestProgressEvent(
    double ProgressFraction,        // 0.0 – 1.0
    DateOnly CurrentDate,
    decimal PortfolioValue,
    long EventsProcessed,
    string? Message = null);
