using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.ViewModels;

/// <summary>
/// ViewModel for the Data Quality Monitoring page.
/// </summary>
public sealed partial class DataQualityViewModel : ObservableObject, IDisposable
{
    private readonly UwpDataQualityService _qualityService;
    private readonly System.Timers.Timer _refreshTimer;
    private bool _disposed;

    // Overall quality state
    [ObservableProperty]
    private double _overallScore;

    [ObservableProperty]
    private string _overallGrade = "A";

    [ObservableProperty]
    private string _overallStatus = "Healthy";

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _healthyFiles;

    [ObservableProperty]
    private int _warningFiles;

    [ObservableProperty]
    private int _criticalFiles;

    [ObservableProperty]
    private int _activeAlerts;

    [ObservableProperty]
    private int _unacknowledgedAlerts;

    [ObservableProperty]
    private DateTime _lastChecked;

    // Trend data
    [ObservableProperty]
    private string _selectedTimeWindow = "7d";

    [ObservableProperty]
    private double _averageTrendScore;

    [ObservableProperty]
    private string _trendDirection = "stable";

    [ObservableProperty]
    private double _trendChangePercent;

    // Loading states
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _errorMessage;

    // Selected items
    [ObservableProperty]
    private QualityScoreEntry? _selectedScore;

    [ObservableProperty]
    private string? _selectedSymbol;

    [ObservableProperty]
    private string _selectedSeverityFilter = "All";

    // Collections
    public ObservableCollection<SymbolQualityItem> SymbolQualities { get; } = new();
    public ObservableCollection<QualityAlert> Alerts { get; } = new();
    public ObservableCollection<QualityScoreEntry> QualityScores { get; } = new();
    public ObservableCollection<AnomalyEvent> Anomalies { get; } = new();
    public ObservableCollection<TrendDataPoint> TrendPoints { get; } = new();
    public ObservableCollection<string> TimeWindowOptions { get; } = new() { "1d", "7d", "30d", "90d" };
    public ObservableCollection<string> SeverityOptions { get; } = new() { "All", "Critical", "Warning", "Info" };

    public DataQualityViewModel()
    {
        _qualityService = UwpDataQualityService.Instance;

        _refreshTimer = new System.Timers.Timer(30000); // Refresh every 30 seconds
        _refreshTimer.Elapsed += async (s, e) => await RefreshAsync();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await Task.WhenAll(
                LoadSummaryAsync(),
                LoadAlertsAsync(),
                LoadQualityScoresAsync(),
                LoadTrendsAsync(),
                LoadAnomaliesAsync()
            );
            _refreshTimer.Start();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        ErrorMessage = null;

        try
        {
            await Task.WhenAll(
                LoadSummaryAsync(),
                LoadAlertsAsync(),
                LoadQualityScoresAsync(),
                LoadTrendsAsync()
            );
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadSummaryAsync()
    {
        var summary = await _qualityService.GetQualitySummaryAsync();
        if (summary != null)
        {
            OverallScore = summary.OverallScore;
            OverallGrade = GetGrade(summary.OverallScore);
            OverallStatus = GetStatus(summary.OverallScore);
            TotalFiles = summary.TotalFiles;
            HealthyFiles = summary.HealthyFiles;
            WarningFiles = summary.WarningFiles;
            CriticalFiles = summary.CriticalFiles;
            ActiveAlerts = summary.ActiveAlerts;
            UnacknowledgedAlerts = summary.UnacknowledgedAlerts;
            LastChecked = summary.LastChecked;

            SymbolQualities.Clear();
            foreach (var symbolSummary in summary.SymbolSummaries.OrderByDescending(s => s.QualityScore))
            {
                SymbolQualities.Add(new SymbolQualityItem(symbolSummary));
            }
        }
    }

    private async Task LoadAlertsAsync()
    {
        string? severityFilter = SelectedSeverityFilter == "All" ? null : SelectedSeverityFilter;
        var alerts = await _qualityService.GetQualityAlertsAsync(severityFilter);

        if (alerts != null)
        {
            Alerts.Clear();
            foreach (var alert in alerts.OrderByDescending(a => a.CreatedAt))
            {
                Alerts.Add(alert);
            }
        }
    }

    private async Task LoadQualityScoresAsync()
    {
        var scores = await _qualityService.GetQualityScoresAsync();

        if (scores != null)
        {
            QualityScores.Clear();
            foreach (var score in scores.OrderByDescending(s => s.Score))
            {
                QualityScores.Add(score);
            }
        }
    }

    private async Task LoadTrendsAsync()
    {
        var trends = await _qualityService.GetQualityTrendsAsync(SelectedTimeWindow);

        if (trends != null)
        {
            TrendPoints.Clear();
            foreach (var point in trends.OverallTrend)
            {
                TrendPoints.Add(point);
            }

            AverageTrendScore = trends.AverageScore;
            TrendChangePercent = trends.TrendDirection;
            TrendDirection = trends.TrendDirection > 0.5 ? "improving"
                           : trends.TrendDirection < -0.5 ? "declining"
                           : "stable";
        }
    }

    private async Task LoadAnomaliesAsync()
    {
        var anomalies = await _qualityService.GetAnomaliesAsync();

        if (anomalies != null)
        {
            Anomalies.Clear();
            foreach (var anomaly in anomalies.OrderByDescending(a => a.DetectedAt))
            {
                Anomalies.Add(anomaly);
            }
        }
    }

    [RelayCommand]
    private async Task AcknowledgeAlertAsync(string alertId)
    {
        var success = await _qualityService.AcknowledgeAlertAsync(alertId);
        if (success)
        {
            await LoadAlertsAsync();
            UnacknowledgedAlerts = Math.Max(0, UnacknowledgedAlerts - 1);
        }
    }

    [RelayCommand]
    private async Task FilterBySeverityAsync(string severity)
    {
        SelectedSeverityFilter = severity;
        await LoadAlertsAsync();
    }

    [RelayCommand]
    private async Task ChangeTimeWindowAsync(string window)
    {
        SelectedTimeWindow = window;
        await LoadTrendsAsync();
    }

    [RelayCommand]
    private async Task RunQualityCheckAsync(string path)
    {
        IsLoading = true;
        try
        {
            var result = await _qualityService.RunQualityCheckAsync(path);
            if (result != null)
            {
                await RefreshAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ViewSymbolQualityAsync(string symbol)
    {
        SelectedSymbol = symbol;
        var report = await _qualityService.GetSymbolQualityAsync(symbol);
        // Update UI with symbol-specific report
    }

    private static string GetGrade(double score) => score switch
    {
        >= 95 => "A+",
        >= 90 => "A",
        >= 85 => "A-",
        >= 80 => "B+",
        >= 75 => "B",
        >= 70 => "B-",
        >= 65 => "C+",
        >= 60 => "C",
        >= 55 => "C-",
        >= 50 => "D",
        _ => "F"
    };

    private static string GetStatus(double score) => score switch
    {
        >= 90 => "Excellent",
        >= 75 => "Healthy",
        >= 50 => "Warning",
        _ => "Critical"
    };

    public void Dispose()
    {
        if (_disposed) return;

        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// View model item for symbol quality display.
/// </summary>
public class SymbolQualityItem
{
    public string Symbol { get; }
    public double QualityScore { get; }
    public string Grade { get; }
    public string Status { get; }
    public int EventCount { get; }
    public int GapCount { get; }
    public DateTime LastUpdate { get; }
    public string StatusColor { get; }
    public string ScoreFormatted { get; }
    public string LastUpdateFormatted { get; }

    public SymbolQualityItem(SymbolQualitySummary summary)
    {
        Symbol = summary.Symbol;
        QualityScore = summary.QualityScore;
        Grade = GetGrade(summary.QualityScore);
        Status = summary.Status;
        EventCount = summary.EventCount;
        GapCount = summary.GapCount;
        LastUpdate = summary.LastUpdate;
        StatusColor = GetStatusColor(summary.QualityScore);
        ScoreFormatted = $"{summary.QualityScore:F1}%";
        LastUpdateFormatted = FormatRelativeTime(summary.LastUpdate);
    }

    private static string GetGrade(double score) => score switch
    {
        >= 90 => "A",
        >= 75 => "B",
        >= 60 => "C",
        >= 50 => "D",
        _ => "F"
    };

    private static string GetStatusColor(double score) => score switch
    {
        >= 90 => "#48BB78",
        >= 75 => "#68D391",
        >= 60 => "#ECC94B",
        >= 50 => "#ED8936",
        _ => "#F56565"
    };

    private static string FormatRelativeTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        return span.TotalMinutes < 1 ? "Just now"
             : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes}m ago"
             : span.TotalHours < 24 ? $"{(int)span.TotalHours}h ago"
             : $"{(int)span.TotalDays}d ago";
    }
}
