using System.Diagnostics;
using Prometheus;

namespace DataIngestion.QuoteService.Services;

public sealed class QuoteMetrics
{
    private readonly Counter _quotesSubmitted = Metrics.CreateCounter("quote_service_submitted_total", "Total quotes submitted");
    private readonly Counter _quotesProcessed = Metrics.CreateCounter("quote_service_processed_total", "Total quotes processed");
    private readonly Counter _quotesDropped = Metrics.CreateCounter("quote_service_dropped_total", "Total quotes dropped");
    private readonly Counter _crossedQuotes = Metrics.CreateCounter("quote_service_crossed_total", "Total crossed quotes");
    private readonly Counter _lockedQuotes = Metrics.CreateCounter("quote_service_locked_total", "Total locked quotes");

    private long _totalProcessed;
    private long _crossed;
    private long _locked;
    private long _startTime = Stopwatch.GetTimestamp();

    public long QuotesProcessed => _totalProcessed;
    public long CrossedQuotes => _crossed;
    public long LockedQuotes => _locked;
    public double QuotesPerSecond => Stopwatch.GetElapsedTime(_startTime).TotalSeconds > 0
        ? _totalProcessed / Stopwatch.GetElapsedTime(_startTime).TotalSeconds : 0;

    public void RecordSubmission() => _quotesSubmitted.Inc();
    public void RecordDropped() => _quotesDropped.Inc();
    public void RecordProcessed(int count)
    {
        Interlocked.Add(ref _totalProcessed, count);
        _quotesProcessed.Inc(count);
    }
    public void RecordCrossedQuote()
    {
        Interlocked.Increment(ref _crossed);
        _crossedQuotes.Inc();
    }
    public void RecordLockedQuote()
    {
        Interlocked.Increment(ref _locked);
        _lockedQuotes.Inc();
    }
}
