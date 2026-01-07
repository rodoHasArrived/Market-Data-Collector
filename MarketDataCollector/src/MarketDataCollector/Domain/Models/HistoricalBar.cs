using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Daily historical bar (OHLCV) used for backfilling from free data sources.
/// </summary>
public sealed record HistoricalBar : MarketEventPayload
{
    public string Symbol { get; }
    public DateOnly SessionDate { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public long Volume { get; }
    public string Source { get; }
    public long SequenceNumber { get; }

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

    public DateTimeOffset ToTimestampUtc()
        => new DateTimeOffset(SessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
