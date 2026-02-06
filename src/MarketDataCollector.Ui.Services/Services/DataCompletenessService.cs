using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Service for analyzing data completeness across time and symbols.
/// Implements Feature #29: Data Completeness Calendar
/// </summary>
public sealed class DataCompletenessService
{
    private readonly ManifestService _manifestService;
    private readonly TradingCalendarService _tradingCalendar;

    public DataCompletenessService(ManifestService manifestService, TradingCalendarService tradingCalendar)
    {
        _manifestService = manifestService;
        _tradingCalendar = tradingCalendar;
    }

    /// <summary>
    /// Generates a completeness report for a date range.
    /// </summary>
    public async Task<CompletenessReport> GetCompletenessReportAsync(
        string dataPath,
        DateOnly startDate,
        DateOnly endDate,
        string[]? symbols = null,
        CancellationToken ct = default)
    {
        var report = new CompletenessReport
        {
            StartDate = startDate,
            EndDate = endDate,
            GeneratedAt = DateTime.UtcNow
        };

        // Get all trading days in the range
        var tradingDays = _tradingCalendar.GetTradingDays(startDate, endDate);
        report.ExpectedTradingDays = tradingDays.Count;

        // Get all available symbols if not specified
        var availableSymbols = symbols ?? GetAvailableSymbols(dataPath);

        // Analyze each symbol
        var symbolReports = new List<SymbolCompleteness>();
        foreach (var symbol in availableSymbols)
        {
            ct.ThrowIfCancellationRequested();
            var symbolReport = await AnalyzeSymbolCompletenessAsync(dataPath, symbol, tradingDays, ct);
            symbolReports.Add(symbolReport);
        }

        report.Symbols = symbolReports;
        report.OverallScore = CalculateOverallScore(symbolReports);

        // Generate calendar heatmap data
        report.CalendarData = GenerateCalendarData(symbolReports, tradingDays, startDate, endDate);

        // Identify gaps
        report.Gaps = IdentifyGaps(symbolReports, tradingDays);

        return report;
    }

    /// <summary>
    /// Gets completeness data for a single date (for drill-down).
    /// </summary>
    public async Task<DailyCompleteness> GetDailyCompletenessAsync(
        string dataPath,
        DateOnly date,
        CancellationToken ct = default)
    {
        var symbols = GetAvailableSymbols(dataPath);
        var result = new DailyCompleteness
        {
            Date = date,
            IsHoliday = _tradingCalendar.IsHoliday(date),
            IsWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
        };

        if (result.IsHoliday || result.IsWeekend)
        {
            result.Status = CompletenessStatus.NonTradingDay;
            return result;
        }

        var symbolDetails = new List<DailySymbolDetail>();
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            var detail = await GetDailySymbolDetailAsync(dataPath, symbol, date, ct);
            symbolDetails.Add(detail);
        }

        result.Symbols = symbolDetails;
        result.TotalEvents = symbolDetails.Sum(s => s.EventCount);
        result.SymbolsWithData = symbolDetails.Count(s => s.HasData);
        result.SymbolsMissingData = symbolDetails.Count(s => !s.HasData);
        result.Status = DetermineStatus(result);

        return result;
    }

    /// <summary>
    /// Identifies and returns all gaps that can be backfilled.
    /// </summary>
    public async Task<List<BackfillableGap>> GetBackfillableGapsAsync(
        string dataPath,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var report = await GetCompletenessReportAsync(dataPath, startDate, endDate, null, ct);
        return report.Gaps
            .Where(g => g.CanBackfill)
            .OrderBy(g => g.Symbol)
            .ThenBy(g => g.Date)
            .ToList();
    }

    private async Task<SymbolCompleteness> AnalyzeSymbolCompletenessAsync(
        string dataPath,
        string symbol,
        List<DateOnly> tradingDays,
        CancellationToken ct)
    {
        var symbolPath = Path.Combine(dataPath, symbol);
        var report = new SymbolCompleteness
        {
            Symbol = symbol,
            ExpectedDays = tradingDays.Count
        };

        if (!Directory.Exists(symbolPath))
        {
            report.DaysWithData = 0;
            report.MissingDays = tradingDays.ToList();
            report.Score = 0;
            return report;
        }

        var daysWithData = new HashSet<DateOnly>();
        var dayEventCounts = new Dictionary<DateOnly, DayEventCount>();

        // Scan all data files
        foreach (var file in Directory.GetFiles(symbolPath, "*.jsonl*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.EndsWith(".jsonl")) fileName = Path.GetFileNameWithoutExtension(fileName);

            if (DateOnly.TryParse(fileName, out var fileDate) && tradingDays.Contains(fileDate))
            {
                daysWithData.Add(fileDate);

                if (!dayEventCounts.TryGetValue(fileDate, out var counts))
                {
                    counts = new DayEventCount { Date = fileDate };
                    dayEventCounts[fileDate] = counts;
                }

                var eventCount = await CountEventsInFileAsync(file, ct);
                var eventType = DetermineEventType(file);
                counts.AddEvents(eventType, eventCount);
            }
        }

        report.DaysWithData = daysWithData.Count;
        report.MissingDays = tradingDays.Except(daysWithData).ToList();
        report.DayDetails = dayEventCounts.Values.OrderBy(d => d.Date).ToList();
        report.Score = tradingDays.Count > 0 ? (double)daysWithData.Count / tradingDays.Count * 100 : 100;
        report.TotalEvents = dayEventCounts.Values.Sum(d => d.TotalEvents);

        return report;
    }

    private async Task<DailySymbolDetail> GetDailySymbolDetailAsync(
        string dataPath,
        string symbol,
        DateOnly date,
        CancellationToken ct)
    {
        var symbolPath = Path.Combine(dataPath, symbol);
        var detail = new DailySymbolDetail
        {
            Symbol = symbol,
            Date = date
        };

        if (!Directory.Exists(symbolPath))
        {
            return detail;
        }

        // Look for files matching this date
        foreach (var file in Directory.GetFiles(symbolPath, $"*{date:yyyy-MM-dd}*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var eventType = DetermineEventType(file);
            var eventCount = await CountEventsInFileAsync(file, ct);

            detail.EventCount += eventCount;
            detail.HasData = true;
            detail.EventTypes.Add(eventType);
            detail.FileSize += new FileInfo(file).Length;

            if (detail.FirstTimestamp == null || detail.LastTimestamp == null)
            {
                var (first, last) = await GetFirstLastTimestampsAsync(file, ct);
                if (first < detail.FirstTimestamp || detail.FirstTimestamp == null)
                    detail.FirstTimestamp = first;
                if (last > detail.LastTimestamp || detail.LastTimestamp == null)
                    detail.LastTimestamp = last;
            }
        }

        return detail;
    }

    private static string DetermineEventType(string filePath)
    {
        var path = filePath.ToLowerInvariant();
        if (path.Contains("trade")) return "Trade";
        if (path.Contains("quote") || path.Contains("bbo")) return "Quote";
        if (path.Contains("depth") || path.Contains("lob")) return "Depth";
        if (path.Contains("bar") || path.Contains("ohlc")) return "Bar";
        return "Unknown";
    }

    private static async Task<int> CountEventsInFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Stream readStream = filePath.EndsWith(".gz")
                ? new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress)
                : stream;

            using var reader = new StreamReader(readStream);
            var count = 0;
            while (await reader.ReadLineAsync(ct) != null)
            {
                count++;
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<(DateTime?, DateTime?)> GetFirstLastTimestampsAsync(string filePath, CancellationToken ct)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Stream readStream = filePath.EndsWith(".gz")
                ? new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress)
                : stream;

            using var reader = new StreamReader(readStream);

            DateTime? first = null;
            DateTime? last = null;
            string? line;

            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (TryExtractTimestamp(line, out var timestamp))
                {
                    first ??= timestamp;
                    last = timestamp;
                }
            }

            return (first, last);
        }
        catch
        {
            return (null, null);
        }
    }

    private static bool TryExtractTimestamp(string jsonLine, out DateTime timestamp)
    {
        timestamp = default;
        try
        {
            // Quick extraction without full JSON parsing
            var idx = jsonLine.IndexOf("\"Timestamp\":");
            if (idx < 0) idx = jsonLine.IndexOf("\"timestamp\":");
            if (idx < 0) return false;

            var startQuote = jsonLine.IndexOf('"', idx + 12);
            var endQuote = jsonLine.IndexOf('"', startQuote + 1);
            if (startQuote < 0 || endQuote < 0) return false;

            var dateStr = jsonLine.Substring(startQuote + 1, endQuote - startQuote - 1);
            return DateTime.TryParse(dateStr, out timestamp);
        }
        catch
        {
            return false;
        }
    }

    private string[] GetAvailableSymbols(string dataPath)
    {
        if (!Directory.Exists(dataPath))
            return Array.Empty<string>();

        return Directory.GetDirectories(dataPath)
            .Select(Path.GetFileName)
            .Where(d => !string.IsNullOrEmpty(d) && !d.StartsWith("_"))
            .Cast<string>()
            .OrderBy(s => s)
            .ToArray();
    }

    private static double CalculateOverallScore(List<SymbolCompleteness> symbols)
    {
        if (symbols.Count == 0) return 100;
        return symbols.Average(s => s.Score);
    }

    private List<CalendarDay> GenerateCalendarData(
        List<SymbolCompleteness> symbols,
        List<DateOnly> tradingDays,
        DateOnly start,
        DateOnly end)
    {
        var calendar = new List<CalendarDay>();
        var currentDate = start;

        while (currentDate <= end)
        {
            var day = new CalendarDay
            {
                Date = currentDate,
                IsWeekend = currentDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                IsHoliday = _tradingCalendar.IsHoliday(currentDate),
                IsTradingDay = tradingDays.Contains(currentDate)
            };

            if (day.IsTradingDay)
            {
                var symbolsWithData = symbols.Count(s => !s.MissingDays.Contains(currentDate));
                day.SymbolsWithData = symbolsWithData;
                day.TotalSymbols = symbols.Count;
                day.CompletenessPercent = symbols.Count > 0 ? (double)symbolsWithData / symbols.Count * 100 : 100;
                day.Status = GetDayStatus(day.CompletenessPercent);
            }
            else
            {
                day.Status = CompletenessStatus.NonTradingDay;
            }

            calendar.Add(day);
            currentDate = currentDate.AddDays(1);
        }

        return calendar;
    }

    private List<BackfillableGap> IdentifyGaps(List<SymbolCompleteness> symbols, List<DateOnly> tradingDays)
    {
        var gaps = new List<BackfillableGap>();

        foreach (var symbol in symbols)
        {
            foreach (var missingDay in symbol.MissingDays)
            {
                gaps.Add(new BackfillableGap
                {
                    Symbol = symbol.Symbol,
                    Date = missingDay,
                    GapType = DetermineGapType(missingDay, tradingDays),
                    CanBackfill = true,
                    EstimatedEvents = symbol.DayDetails.Count > 0
                        ? (int)symbol.DayDetails.Average(d => d.TotalEvents)
                        : 10000
                });
            }
        }

        return gaps;
    }

    private static GapType DetermineGapType(DateOnly date, List<DateOnly> tradingDays)
    {
        var idx = tradingDays.IndexOf(date);
        if (idx == 0) return GapType.StartOfRange;
        if (idx == tradingDays.Count - 1) return GapType.EndOfRange;

        // Check if it's part of a consecutive gap
        var prevMissing = idx > 0 && !tradingDays.Contains(tradingDays[idx - 1]);
        var nextMissing = idx < tradingDays.Count - 1 && !tradingDays.Contains(tradingDays[idx + 1]);

        if (prevMissing || nextMissing) return GapType.Consecutive;
        return GapType.Single;
    }

    private static CompletenessStatus GetDayStatus(double percent) => percent switch
    {
        >= 99 => CompletenessStatus.Complete,
        >= 95 => CompletenessStatus.MinorGaps,
        >= 80 => CompletenessStatus.SignificantGaps,
        _ => CompletenessStatus.MajorIssues
    };

    private static CompletenessStatus DetermineStatus(DailyCompleteness day)
    {
        if (day.IsHoliday || day.IsWeekend) return CompletenessStatus.NonTradingDay;
        if (day.SymbolsMissingData == 0) return CompletenessStatus.Complete;
        var pct = day.Symbols.Count > 0 ? (double)day.SymbolsWithData / day.Symbols.Count * 100 : 100;
        return GetDayStatus(pct);
    }
}

/// <summary>
/// Service for trading calendar information.
/// </summary>
public sealed class TradingCalendarService
{
    private readonly HashSet<DateOnly> _holidays = new();

    public TradingCalendarService()
    {
        // Add common US market holidays (simplified - would load from config in production)
        AddHolidays2026();
    }

    private void AddHolidays2026()
    {
        // 2026 US Market Holidays
        _holidays.Add(new DateOnly(2026, 1, 1));   // New Year's Day
        _holidays.Add(new DateOnly(2026, 1, 19));  // MLK Day
        _holidays.Add(new DateOnly(2026, 2, 16));  // Presidents Day
        _holidays.Add(new DateOnly(2026, 4, 3));   // Good Friday
        _holidays.Add(new DateOnly(2026, 5, 25));  // Memorial Day
        _holidays.Add(new DateOnly(2026, 7, 3));   // Independence Day (observed)
        _holidays.Add(new DateOnly(2026, 9, 7));   // Labor Day
        _holidays.Add(new DateOnly(2026, 11, 26)); // Thanksgiving
        _holidays.Add(new DateOnly(2026, 12, 25)); // Christmas
    }

    public bool IsHoliday(DateOnly date) => _holidays.Contains(date);

    public bool IsTradingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(date);

    public List<DateOnly> GetTradingDays(DateOnly start, DateOnly end)
    {
        var days = new List<DateOnly>();
        var current = start;
        while (current <= end)
        {
            if (IsTradingDay(current))
                days.Add(current);
            current = current.AddDays(1);
        }
        return days;
    }
}

#region Models

public sealed record CompletenessReport
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateTime GeneratedAt { get; init; }
    public int ExpectedTradingDays { get; init; }
    public double OverallScore { get; init; }
    public List<SymbolCompleteness> Symbols { get; init; } = new();
    public List<CalendarDay> CalendarData { get; init; } = new();
    public List<BackfillableGap> Gaps { get; init; } = new();
}

public sealed record SymbolCompleteness
{
    public string Symbol { get; init; } = "";
    public int ExpectedDays { get; init; }
    public int DaysWithData { get; init; }
    public double Score { get; init; }
    public long TotalEvents { get; init; }
    public List<DateOnly> MissingDays { get; init; } = new();
    public List<DayEventCount> DayDetails { get; init; } = new();
}

public sealed class DayEventCount
{
    public DateOnly Date { get; init; }
    public int TradeEvents { get; set; }
    public int QuoteEvents { get; set; }
    public int DepthEvents { get; set; }
    public int BarEvents { get; set; }
    public int OtherEvents { get; set; }
    public int TotalEvents => TradeEvents + QuoteEvents + DepthEvents + BarEvents + OtherEvents;

    public void AddEvents(string type, int count)
    {
        switch (type)
        {
            case "Trade": TradeEvents += count; break;
            case "Quote": QuoteEvents += count; break;
            case "Depth": DepthEvents += count; break;
            case "Bar": BarEvents += count; break;
            default: OtherEvents += count; break;
        }
    }
}

public sealed record CalendarDay
{
    public DateOnly Date { get; init; }
    public bool IsWeekend { get; init; }
    public bool IsHoliday { get; init; }
    public bool IsTradingDay { get; init; }
    public int SymbolsWithData { get; init; }
    public int TotalSymbols { get; init; }
    public double CompletenessPercent { get; init; }
    public CompletenessStatus Status { get; init; }
}

public sealed record DailyCompleteness
{
    public DateOnly Date { get; init; }
    public bool IsWeekend { get; init; }
    public bool IsHoliday { get; init; }
    public CompletenessStatus Status { get; init; }
    public int SymbolsWithData { get; init; }
    public int SymbolsMissingData { get; init; }
    public long TotalEvents { get; init; }
    public List<DailySymbolDetail> Symbols { get; init; } = new();
}

public sealed record DailySymbolDetail
{
    public string Symbol { get; init; } = "";
    public DateOnly Date { get; init; }
    public bool HasData { get; init; }
    public int EventCount { get; init; }
    public long FileSize { get; init; }
    public DateTime? FirstTimestamp { get; init; }
    public DateTime? LastTimestamp { get; init; }
    public List<string> EventTypes { get; init; } = new();
}

public sealed record BackfillableGap
{
    public string Symbol { get; init; } = "";
    public DateOnly Date { get; init; }
    public GapType GapType { get; init; }
    public bool CanBackfill { get; init; }
    public int EstimatedEvents { get; init; }
}

public enum CompletenessStatus
{
    Complete,          // >= 99%
    MinorGaps,         // 95-99%
    SignificantGaps,   // 80-95%
    MajorIssues,       // < 80%
    NonTradingDay      // Weekend or holiday
}

public enum GapType
{
    Single,
    Consecutive,
    StartOfRange,
    EndOfRange
}

#endregion
