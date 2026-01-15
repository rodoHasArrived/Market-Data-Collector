using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Daily historical bar (OHLCV) used for backfilling from free data sources.
/// </summary>
public sealed record HistoricalBar : MarketEventPayload
{
    /// <summary>
    /// Gets the ticker symbol for the security.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the trading session date for this bar.
    /// </summary>
    public DateOnly SessionDate { get; }

    /// <summary>
    /// Gets the opening price for the session.
    /// </summary>
    public decimal Open { get; }

    /// <summary>
    /// Gets the highest price during the session.
    /// </summary>
    public decimal High { get; }

    /// <summary>
    /// Gets the lowest price during the session.
    /// </summary>
    public decimal Low { get; }

    /// <summary>
    /// Gets the closing price for the session.
    /// </summary>
    public decimal Close { get; }

    /// <summary>
    /// Gets the total trading volume for the session.
    /// </summary>
    public long Volume { get; }

    /// <summary>
    /// Gets the data source identifier (e.g., "stooq", "alpaca").
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the sequence number for ordering events.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoricalBar"/> record.
    /// </summary>
    /// <param name="Symbol">The ticker symbol.</param>
    /// <param name="SessionDate">The trading session date.</param>
    /// <param name="Open">The opening price.</param>
    /// <param name="High">The highest price.</param>
    /// <param name="Low">The lowest price.</param>
    /// <param name="Close">The closing price.</param>
    /// <param name="Volume">The total volume.</param>
    /// <param name="Source">The data source identifier.</param>
    /// <param name="SequenceNumber">The sequence number for ordering.</param>
    public HistoricalBar(
        string Symbol,
        DateOnly SessionDate,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume,
        string Source = "stooq",
        long SequenceNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            throw new ArgumentException("Symbol is required", nameof(Symbol));

        if (Open <= 0 || High <= 0 || Low <= 0 || Close <= 0)
            throw new ArgumentOutOfRangeException(nameof(Open), "OHLC values must be greater than zero.");

        if (Low > High)
            throw new ArgumentOutOfRangeException(nameof(Low), "Low cannot exceed high.");

        if (Open > High || Close > High)
            throw new ArgumentOutOfRangeException(nameof(High), "Open/Close cannot exceed high.");

        if (Open < Low || Close < Low)
            throw new ArgumentOutOfRangeException(nameof(Low), "Open/Close cannot be below low.");

        if (Volume < 0)
            throw new ArgumentOutOfRangeException(nameof(Volume), "Volume cannot be negative.");

        this.Symbol = Symbol;
        this.SessionDate = SessionDate;
        this.Open = Open;
        this.High = High;
        this.Low = Low;
        this.Close = Close;
        this.Volume = Volume;
        this.Source = Source;
        this.SequenceNumber = SequenceNumber;
    }

    /// <summary>
    /// Converts the session date to a UTC timestamp.
    /// </summary>
    public DateTimeOffset ToTimestampUtc()
        => new DateTimeOffset(SessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
