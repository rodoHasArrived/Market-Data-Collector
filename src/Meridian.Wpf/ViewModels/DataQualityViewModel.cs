using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using Meridian.Ui.Services;
using Meridian.Wpf.Models;
using WpfServices = Meridian.Wpf.Services;

namespace Meridian.Wpf.ViewModels;

/// <summary>Event args fired when the overall quality score changes, carrying all data
/// needed for the code-behind to update <c>ScoreRing</c> and related brushes.</summary>
public sealed class ScoreUpdatedEventArgs : EventArgs
{
    public double Score { get; init; }
    public string Label { get; init; } = string.Empty;
    public double[] StrokeSegments { get; init; } = Array.Empty<double>();
    public Color ScoreColor { get; init; }
}

/// <summary>Snapshot of trend statistics used by the code-behind to render the chart.</summary>
public readonly struct TrendStatistics
{
    public string AvgText { get; init; }
    public string MinText { get; init; }
    public string MaxText { get; init; }
    public string StdDevText { get; init; }
    public string TrendText { get; init; }
    public bool HasData { get; init; }
    public bool IsTrendPositive { get; init; }
    public double ScoreChange { get; init; }
}

/// <summary>
/// ViewModel for the Data Quality monitoring page.
/// All HTTP loading, filtering, metric computation, and alert management live here.
/// The code-behind retains only canvas/chart rendering and dialog creation.
/// </summary>
public sealed class DataQualityViewModel : BindableBase, IDisposable
{
    private readonly WpfServices.StatusService _statusService;
    private readonly WpfServices.LoggingService _loggingService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly HttpClient _httpClient = new();

    private DispatcherTimer? _refreshTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _timeRange = "7d";
    private double _lastOverallScore = 98.5;

    private readonly List<AlertModel> _allAlerts = new();
    private readonly List<AnomalyModel> _allAnomalies = new();

    // ── ScoreUpdated event ──────────────────────────────────────────────────
    /// <summary>
    /// Raised whenever the overall quality score is refreshed so the code-behind
    /// can update <c>ScoreRing.StrokeDashArray</c> and score-related brushes.
    /// </summary>
    public event EventHandler<ScoreUpdatedEventArgs>? ScoreUpdated;

    // ── Public collections ──────────────────────────────────────────────────
    public ObservableCollection<SymbolQualityModel> SymbolQuality { get; } = new();
    public ObservableCollection<SymbolQualityModel> FilteredSymbols { get; } = new();
    public ObservableCollection<GapModel> Gaps { get; } = new();
    public ObservableCollection<AlertModel> Alerts { get; } = new();
    public ObservableCollection<AnomalyModel> Anomalies { get; } = new();

    // ── Bindable text properties ────────────────────────────────────────────
    private string _lastUpdateText = "Last updated: --";
    public string LastUpdateText
    {
        get => _lastUpdateText;
        private set => SetProperty(ref _lastUpdateText, value);
    }

    private string _latencyText = "--";
    public string LatencyText
    {
        get => _latencyText;
        private set => SetProperty(ref _latencyText, value);
    }

    private string _completenessText = "--";
    public string CompletenessText
    {
        get => _completenessText;
        private set => SetProperty(ref _completenessText, value);
    }

    private string _healthyFilesText = "--";
    public string HealthyFilesText
    {
        get => _healthyFilesText;
        private set => SetProperty(ref _healthyFilesText, value);
    }

    private string _warningFilesText = "--";
    public string WarningFilesText
    {
        get => _warningFilesText;
        private set => SetProperty(ref _warningFilesText, value);
    }

    private string _criticalFilesText = "--";
    public string CriticalFilesText
    {
        get => _criticalFilesText;
        private set => SetProperty(ref _criticalFilesText, value);
    }

    private string _gapsCountText = "--";
    public string GapsCountText
    {
        get => _gapsCountText;
        private set => SetProperty(ref _gapsCountText, value);
    }

    private Color _gapsCountColor = Color.FromRgb(63, 185, 80);
    public Color GapsCountColor
    {
        get => _gapsCountColor;
        private set => SetProperty(ref _gapsCountColor, value);
    }

    private string _errorsCountText = "--";
    public string ErrorsCountText
    {
        get => _errorsCountText;
        private set => SetProperty(ref _errorsCountText, value);
    }

    private Color _errorsCountColor = Color.FromRgb(63, 185, 80);
    public Color ErrorsCountColor
    {
        get => _errorsCountColor;
        private set => SetProperty(ref _errorsCountColor, value);
    }

    private string _unacknowledgedText = "--";
    public string UnacknowledgedText
    {
        get => _unacknowledgedText;
        private set => SetProperty(ref _unacknowledgedText, value);
    }

    private string _totalActiveAlertsText = "--";
    public string TotalActiveAlertsText
    {
        get => _totalActiveAlertsText;
        private set => SetProperty(ref _totalActiveAlertsText, value);
    }

    private string _alertCountBadgeText = "0";
    public string AlertCountBadgeText
    {
        get => _alertCountBadgeText;
        private set => SetProperty(ref _alertCountBadgeText, value);
    }

    private bool _isAlertCountBadgeVisible;
    public bool IsAlertCountBadgeVisible
    {
        get => _isAlertCountBadgeVisible;
        private set => SetProperty(ref _isAlertCountBadgeVisible, value);
    }

    private string _crossedMarketCount = "--";
    public string CrossedMarketCount
    {
        get => _crossedMarketCount;
        private set => SetProperty(ref _crossedMarketCount, value);
    }

    private string _staleDataCount = "--";
    public string StaleDataCount
    {
        get => _staleDataCount;
        private set => SetProperty(ref _staleDataCount, value);
    }

    private string _invalidPriceCount = "--";
    public string InvalidPriceCount
    {
        get => _invalidPriceCount;
        private set => SetProperty(ref _invalidPriceCount, value);
    }

    private string _invalidVolumeCount = "--";
    public string InvalidVolumeCount
    {
        get => _invalidVolumeCount;
        private set => SetProperty(ref _invalidVolumeCount, value);
    }

    private string _missingDataCount = "--";
    public string MissingDataCount
    {
        get => _missingDataCount;
        private set => SetProperty(ref _missingDataCount, value);
    }

    private string _lastCheckTimeText = "--";
    public string LastCheckTimeText
    {
        get => _lastCheckTimeText;
        private set => SetProperty(ref _lastCheckTimeText, value);
    }

    private string _nextCheckText = "--";
    public string NextCheckText
    {
        get => _nextCheckText;
        private set => SetProperty(ref _nextCheckText, value);
    }

    private double _checkProgressValue;
    public double CheckProgressValue
    {
        get => _checkProgressValue;
        private set => SetProperty(ref _checkProgressValue, value);
    }

    private string _p50Text = "--";
    public string P50Text { get => _p50Text; private set => SetProperty(ref _p50Text, value); }

    private string _p75Text = "--";
    public string P75Text { get => _p75Text; private set => SetProperty(ref _p75Text, value); }

    private string _p90Text = "--";
    public string P90Text { get => _p90Text; private set => SetProperty(ref _p90Text, value); }

    private string _p95Text = "--";
    public string P95Text { get => _p95Text; private set => SetProperty(ref _p95Text, value); }

    private string _p99Text = "--";
    public string P99Text { get => _p99Text; private set => SetProperty(ref _p99Text, value); }

    // ── Visibility booleans ─────────────────────────────────────────────────
    private bool _hasNoGaps = true;
    public bool HasNoGaps { get => _hasNoGaps; private set => SetProperty(ref _hasNoGaps, value); }

    private bool _hasNoAlerts = true;
    public bool HasNoAlerts { get => _hasNoAlerts; private set => SetProperty(ref _hasNoAlerts, value); }

    private bool _hasNoAnomalies = true;
    public bool HasNoAnomalies { get => _hasNoAnomalies; private set => SetProperty(ref _hasNoAnomalies, value); }

    private bool _hasNoSymbols = true;
    public bool HasNoSymbols { get => _hasNoSymbols; private set => SetProperty(ref _hasNoSymbols, value); }

    private bool _isAnomalyCountBadgeVisible;
    public bool IsAnomalyCountBadgeVisible
    {
        get => _isAnomalyCountBadgeVisible;
        private set => SetProperty(ref _isAnomalyCountBadgeVisible, value);
    }

    private string _anomalyCountText = "0";
    public string AnomalyCountText
    {
        get => _anomalyCountText;
        private set => SetProperty(ref _anomalyCountText, value);
    }

    public DataQualityViewModel(
        WpfServices.StatusService statusService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        _statusService = statusService;
        _loggingService = loggingService;
        _notificationService = notificationService;
        _baseUrl = _statusService.BaseUrl;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────
    public async Task StartAsync()
    {
        await RefreshDataAsync();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += async (_, _) => await RefreshDataAsync();
        _refreshTimer.Start();
    }

    public void Stop()
    {
        _refreshTimer?.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient.Dispose();
    }

    // ── Refresh ─────────────────────────────────────────────────────────────
    public async Task RefreshAsync()
    {
        await RefreshDataAsync();
        _notificationService.ShowNotification(
            "Refreshed",
            "Data quality metrics have been refreshed.",
            NotificationType.Info);
    }

    private async Task RefreshDataAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await LoadDashboardAsync(_cts.Token);
            await LoadGapsAsync(_cts.Token);
            await LoadAnomaliesAsync(_cts.Token);
            await LoadLatencyDistributionAsync(_cts.Token);
            LastUpdateText = $"Last updated: {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to refresh data quality", ex);
        }
    }

    // ── Dashboard loading ───────────────────────────────────────────────────
    private async Task LoadDashboardAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/quality/dashboard", ct);
            if (!response.IsSuccessStatusCode)
            {
                LoadDemoDashboard();
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (data.TryGetProperty("realTimeMetrics", out var metrics))
            {
                var score = metrics.TryGetProperty("overallHealthScore", out var overall)
                    ? Math.Clamp(overall.GetDouble() * 100, 0, 100)
                    : 0;
                _lastOverallScore = score;

                var scoreColor = ScoreToColor(score);
                ScoreUpdated?.Invoke(this, new ScoreUpdatedEventArgs
                {
                    Score = score,
                    Label = GetGrade(score),
                    StrokeSegments = new[] { score, Math.Max(0, 100 - score) },
                    ScoreColor = scoreColor
                });

                if (metrics.TryGetProperty("averageLatencyMs", out var avgLatency))
                    LatencyText = $"{avgLatency.GetDouble():F0}ms";

                SymbolQuality.Clear();
                if (metrics.TryGetProperty("symbolHealth", out var symbolHealth) &&
                    symbolHealth.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sym in symbolHealth.EnumerateArray())
                    {
                        var symbol = sym.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var scoreValue = sym.TryGetProperty("score", out var q) ? q.GetDouble() * 100 : 0;
                        var state = sym.TryGetProperty("state", out var st)
                            ? ReadEnumString(st, HealthStateNames) : "Unknown";
                        var lastEvent = sym.TryGetProperty("lastEvent", out var le)
                            ? le.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                        var issues = sym.TryGetProperty("activeIssues", out var ai) &&
                            ai.ValueKind == JsonValueKind.Array
                            ? string.Join(", ", ai.EnumerateArray().Select(i => i.GetString())
                                .Where(i => !string.IsNullOrWhiteSpace(i)))
                            : "";

                        SymbolQuality.Add(new SymbolQualityModel
                        {
                            Symbol = symbol,
                            Score = scoreValue,
                            ScoreFormatted = $"{scoreValue:F1}%",
                            Grade = GetGrade(scoreValue),
                            Status = state,
                            Issues = string.IsNullOrWhiteSpace(issues) ? "—" : issues,
                            LastUpdate = lastEvent,
                            LastUpdateFormatted = FormatRelativeTime(lastEvent.UtcDateTime)
                        });
                    }
                }

                ApplySymbolFilter(string.Empty);
            }

            if (data.TryGetProperty("completenessStats", out var completeness))
            {
                if (completeness.TryGetProperty("averageScore", out var avgScore))
                {
                    var value = avgScore.GetDouble() * 100;
                    CompletenessText = value > 0 ? $"{value:F1}%" : "--";
                }

                var gradeDistribution = completeness.TryGetProperty("gradeDistribution", out var dist)
                    ? dist : default;

                var healthy = GetGradeCount(gradeDistribution, "A") + GetGradeCount(gradeDistribution, "B");
                var warning = GetGradeCount(gradeDistribution, "C");
                var critical = GetGradeCount(gradeDistribution, "D") + GetGradeCount(gradeDistribution, "F");

                HealthyFilesText = healthy.ToString("N0");
                WarningFilesText = warning.ToString("N0");
                CriticalFilesText = critical.ToString("N0");

                if (completeness.TryGetProperty("calculatedAt", out var calculatedAt))
                {
                    var timestamp = calculatedAt.GetDateTimeOffset();
                    LastCheckTimeText = FormatRelativeTime(timestamp.UtcDateTime);
                    NextCheckText = "In 30 minutes";
                    CheckProgressValue = Math.Min(100, (DateTimeOffset.UtcNow - timestamp).TotalMinutes / 30 * 100);
                }
            }

            if (data.TryGetProperty("gapStats", out var gaps))
            {
                if (gaps.TryGetProperty("totalGaps", out var totalGaps))
                {
                    var gapCount = totalGaps.GetInt32();
                    GapsCountText = gapCount.ToString();
                    GapsCountColor = gapCount == 0 ? Color.FromRgb(63, 185, 80) :
                        gapCount <= 5 ? Color.FromRgb(255, 193, 7) : Color.FromRgb(244, 67, 54);
                }
            }

            if (data.TryGetProperty("sequenceStats", out var sequenceStats))
            {
                if (sequenceStats.TryGetProperty("totalErrors", out var totalErrors))
                {
                    var errors = totalErrors.GetInt64();
                    ErrorsCountText = errors.ToString("N0");
                    ErrorsCountColor = errors == 0 ? Color.FromRgb(63, 185, 80) : Color.FromRgb(244, 67, 54);
                }
            }

            if (data.TryGetProperty("anomalyStats", out var anomalyStats))
            {
                if (anomalyStats.TryGetProperty("unacknowledgedCount", out var unack))
                {
                    var unackCount = unack.GetInt32();
                    UnacknowledgedText = unackCount.ToString();
                    AlertCountBadgeText = unackCount.ToString();
                    IsAlertCountBadgeVisible = unackCount > 0;
                }

                if (anomalyStats.TryGetProperty("totalAnomalies", out var total))
                    TotalActiveAlertsText = total.GetInt64().ToString("N0");

                if (anomalyStats.TryGetProperty("anomaliesByType", out var anomaliesByType))
                {
                    CrossedMarketCount = GetAnomalyCount(anomaliesByType, "CrossedMarket").ToString();
                    StaleDataCount = GetAnomalyCount(anomaliesByType, "StaleData").ToString();
                    InvalidPriceCount = GetAnomalyCount(anomaliesByType, "InvalidPrice").ToString();
                    InvalidVolumeCount = GetAnomalyCount(anomaliesByType, "InvalidVolume").ToString();
                    MissingDataCount = GetAnomalyCount(anomaliesByType, "MissingData").ToString();
                }
            }

            if (data.TryGetProperty("recentAnomalies", out var recentAnomalies) &&
                recentAnomalies.ValueKind == JsonValueKind.Array)
            {
                _allAlerts.Clear();
                foreach (var alert in recentAnomalies.EnumerateArray())
                {
                    var model = BuildAlertModel(alert);
                    if (model != null) _allAlerts.Add(model);
                }

                ApplyAlertFilter("All");
            }
        }
        catch (HttpRequestException)
        {
            LoadDemoDashboard();
        }
    }

    private void LoadDemoDashboard()
    {
        _lastOverallScore = 98.5;
        var demoColor = ScoreToColor(98.5);
        ScoreUpdated?.Invoke(this, new ScoreUpdatedEventArgs
        {
            Score = 98.5,
            Label = "A+",
            StrokeSegments = new[] { 98.5, 1.5 },
            ScoreColor = demoColor
        });

        HealthyFilesText = "1,234";
        WarningFilesText = "12";
        CriticalFilesText = "0";
        UnacknowledgedText = "2";
        TotalActiveAlertsText = "5";
        AlertCountBadgeText = "2";
        IsAlertCountBadgeVisible = true;
        LastCheckTimeText = "2 minutes ago";
        NextCheckText = "In 28 minutes";
        CheckProgressValue = 6;
        CompletenessText = "98.5%";
        GapsCountText = "3";
        GapsCountColor = Color.FromRgb(255, 193, 7);
        ErrorsCountText = "0";
        ErrorsCountColor = Color.FromRgb(63, 185, 80);
        LatencyText = "12ms";

        SymbolQuality.Clear();
        SymbolQuality.Add(CreateDemoSymbolQuality("SPY", 99.8, "Healthy"));
        SymbolQuality.Add(CreateDemoSymbolQuality("AAPL", 98.2, "Healthy"));
        SymbolQuality.Add(CreateDemoSymbolQuality("MSFT", 97.5, "Healthy"));
        SymbolQuality.Add(CreateDemoSymbolQuality("GOOGL", 94.8, "Degraded"));
        SymbolQuality.Add(CreateDemoSymbolQuality("AMZN", 96.1, "Healthy"));
        ApplySymbolFilter(string.Empty);

        _allAlerts.Clear();
        _allAlerts.Add(new AlertModel
        {
            Id = "alert-1",
            Symbol = "AAPL",
            AlertType = "StaleData",
            Message = "No trades received in the last 3 minutes",
            Severity = "Warning",
            SeverityBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7))
        });
        _allAlerts.Add(new AlertModel
        {
            Id = "alert-2",
            Symbol = "GOOGL",
            AlertType = "CrossedMarket",
            Message = "Bid price exceeded ask for 2 ticks",
            Severity = "Critical",
            SeverityBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54))
        });
        ApplyAlertFilter("All");
    }

    private static SymbolQualityModel CreateDemoSymbolQuality(string symbol, double score, string status) =>
        new()
        {
            Symbol = symbol,
            Score = score,
            ScoreFormatted = $"{score:F1}%",
            Grade = GetGrade(score),
            Status = status,
            Issues = status == "Healthy" ? "—" : "Recent gaps",
            LastUpdate = DateTimeOffset.UtcNow.AddMinutes(-3),
            LastUpdateFormatted = "3m ago"
        };

    // ── Gap loading ─────────────────────────────────────────────────────────
    private async Task LoadGapsAsync(CancellationToken ct)
    {
        try
        {
            var count = GetRangeCount(_timeRange, 100, 250, 500, 1000);
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/quality/gaps?count={count}", ct);

            Gaps.Clear();
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var gap in data.EnumerateArray())
                    {
                        var gapId = gap.TryGetProperty("gapStart", out var gs)
                            ? gs.GetDateTimeOffset().ToString("O") : Guid.NewGuid().ToString();
                        var symbol = gap.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var start = gap.TryGetProperty("gapStart", out var st) ? st.GetDateTimeOffset() : DateTimeOffset.MinValue;
                        var end = gap.TryGetProperty("gapEnd", out var et) ? et.GetDateTimeOffset() : DateTimeOffset.MinValue;
                        var missingBars = gap.TryGetProperty("estimatedMissedEvents", out var mb) ? mb.GetInt64() : 0;
                        var duration = end - start;
                        var durationText = duration.TotalDays >= 1 ? $"{duration.TotalDays:F0} days" :
                            duration.TotalHours >= 1 ? $"{duration.TotalHours:F0} hours" :
                            $"{duration.TotalMinutes:F0} mins";

                        Gaps.Add(new GapModel
                        {
                            GapId = gapId,
                            Symbol = symbol,
                            Description = $"Missing {missingBars} events between {start:yyyy-MM-dd HH:mm} and {end:yyyy-MM-dd HH:mm}",
                            Duration = durationText
                        });
                    }
                }
            }
            else
            {
                LoadDemoGaps();
            }

            HasNoGaps = Gaps.Count == 0;
        }
        catch (HttpRequestException)
        {
            LoadDemoGaps();
        }
    }

    private void LoadDemoGaps()
    {
        Gaps.Clear();
        Gaps.Add(new GapModel { GapId = "gap-1", Symbol = "AAPL", Description = "Missing 156 events between 2024-01-15 09:30 and 2024-01-17 16:00", Duration = "2 days" });
        Gaps.Add(new GapModel { GapId = "gap-2", Symbol = "GOOGL", Description = "Missing 45 events between 2024-01-20 14:00 and 2024-01-20 15:30", Duration = "1.5 hours" });
        Gaps.Add(new GapModel { GapId = "gap-3", Symbol = "MSFT", Description = "Missing 12 events between 2024-01-22 10:00 and 2024-01-22 10:15", Duration = "15 mins" });
        HasNoGaps = Gaps.Count == 0;
    }

    // ── Anomaly loading ─────────────────────────────────────────────────────
    private async Task LoadAnomaliesAsync(CancellationToken ct)
    {
        try
        {
            var count = GetRangeCount(_timeRange, 50, 100, 200, 400);
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/quality/anomalies?count={count}", ct);

            Anomalies.Clear();
            _allAnomalies.Clear();

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var anomaly in data.EnumerateArray())
                    {
                        var model = BuildAnomalyModel(anomaly);
                        if (model != null) _allAnomalies.Add(model);
                    }
                }
            }

            ApplyAnomalyFilter("All");
        }
        catch (HttpRequestException)
        {
            Anomalies.Clear();
            _allAnomalies.Clear();
            HasNoAnomalies = true;
        }
    }

    // ── Latency loading ─────────────────────────────────────────────────────
    private async Task LoadLatencyDistributionAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/quality/latency/statistics", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                P50Text = data.TryGetProperty("globalP50Ms", out var p50) ? $"{p50.GetDouble():F0}ms" : "--";
                P75Text = data.TryGetProperty("globalMeanMs", out var mean) ? $"{mean.GetDouble():F0}ms" : "--";
                P90Text = data.TryGetProperty("globalP90Ms", out var p90) ? $"{p90.GetDouble():F0}ms" : "--";
                P95Text = data.TryGetProperty("globalP90Ms", out var p95) ? $"{p95.GetDouble():F0}ms" : "--";
                P99Text = data.TryGetProperty("globalP99Ms", out var p99) ? $"{p99.GetDouble():F0}ms" : "--";
            }
            else
            {
                LoadDemoLatency();
            }
        }
        catch (HttpRequestException)
        {
            LoadDemoLatency();
        }
    }

    private void LoadDemoLatency()
    {
        P50Text = "8ms";
        P75Text = "12ms";
        P90Text = "18ms";
        P95Text = "25ms";
        P99Text = "45ms";
    }

    // ── Filtering ───────────────────────────────────────────────────────────
    public void ApplySymbolFilter(string query)
    {
        FilteredSymbols.Clear();
        foreach (var symbol in SymbolQuality.Where(s =>
            string.IsNullOrEmpty(query) ||
            s.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredSymbols.Add(symbol);
        }

        HasNoSymbols = FilteredSymbols.Count == 0;
    }

    public void ApplyAlertFilter(string severity)
    {
        Alerts.Clear();
        foreach (var alert in _allAlerts.Where(a =>
            severity == "All" || a.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase)))
        {
            Alerts.Add(alert);
        }

        HasNoAlerts = Alerts.Count == 0;
    }

    public void ApplyAnomalyFilter(string type)
    {
        Anomalies.Clear();
        foreach (var anomaly in _allAnomalies.Where(a =>
            type == "All" || a.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
        {
            Anomalies.Add(anomaly);
        }

        HasNoAnomalies = Anomalies.Count == 0;
        IsAnomalyCountBadgeVisible = Anomalies.Count > 0;
        AnomalyCountText = Anomalies.Count.ToString();
    }

    // ── Time range ──────────────────────────────────────────────────────────
    public void SetTimeRange(string timeRange)
    {
        _timeRange = timeRange;
        _ = RefreshDataAsync();
    }

    // ── Trend statistics (consumed by code-behind for chart rendering) ───────
    public TrendStatistics ComputeTrendStatistics()
    {
        var points = BuildTrendPoints(_lastOverallScore, _timeRange);
        if (points.Count == 0)
            return new TrendStatistics { HasData = false, AvgText = "--", MinText = "--", MaxText = "--", StdDevText = "--", TrendText = "--" };

        var scores = points.Select(p => p.Score).ToList();
        var avg = scores.Average();
        var min = scores.Min();
        var max = scores.Max();
        var stdDev = Math.Sqrt(scores.Sum(s => Math.Pow(s - avg, 2)) / scores.Count);
        var change = scores.Last() - scores.First();
        var label = GetTimeWindowLabel(_timeRange);
        var isPositive = change >= 0;

        return new TrendStatistics
        {
            HasData = true,
            AvgText = $"{avg:F1}%",
            MinText = $"{min:F1}%",
            MaxText = $"{max:F1}%",
            StdDevText = $"{stdDev:F1}%",
            TrendText = $"{(isPositive ? "+" : "")}{change:F1}% this {label}",
            IsTrendPositive = isPositive,
            ScoreChange = change
        };
    }

    public IReadOnlyList<TrendPoint> GetTrendPoints() =>
        BuildTrendPoints(_lastOverallScore, _timeRange);

    // ── Alert / gap management ──────────────────────────────────────────────
    public async Task AcknowledgeAlertAsync(string alertId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/anomalies/{alertId}/acknowledge", null);

            if (response.IsSuccessStatusCode)
            {
                _allAlerts.RemoveAll(a => a.Id == alertId);
                ApplyAlertFilter("All");
                await RefreshDataAsync();
            }
            else
            {
                _notificationService.ShowNotification("Acknowledge Failed", "Failed to acknowledge alert.", NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to acknowledge alert", ex);
            _notificationService.ShowNotification("Acknowledge Failed", "An error occurred while acknowledging the alert.", NotificationType.Error);
        }
    }

    public async Task AcknowledgeAllAlertsAsync()
    {
        foreach (var alert in _allAlerts.ToList())
        {
            try
            {
                await _httpClient.PostAsync(
                    $"{_baseUrl}/api/quality/anomalies/{alert.Id}/acknowledge", null);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to acknowledge alert", ex);
            }
        }

        _allAlerts.Clear();
        ApplyAlertFilter("All");
        await RefreshDataAsync();

        _notificationService.ShowNotification("All Alerts Acknowledged", "All alerts have been acknowledged.", NotificationType.Success);
    }

    /// <returns>True if the repair was initiated successfully.</returns>
    public async Task<bool> RepairGapAsync(string gapId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/{gapId}/repair", null,
                _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                var gap = Gaps.FirstOrDefault(g => g.GapId == gapId);
                if (gap != null) Gaps.Remove(gap);
                HasNoGaps = Gaps.Count == 0;

                _notificationService.ShowNotification(
                    "Gap Repair Started",
                    $"Repair has been initiated.",
                    NotificationType.Success);
                return true;
            }

            _notificationService.ShowNotification("Repair Failed", "Failed to initiate gap repair. Please try again.", NotificationType.Warning);
            return false;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to repair gap", ex);
            _notificationService.ShowNotification("Repair Failed", "An error occurred while initiating gap repair.", NotificationType.Error);
            return false;
        }
    }

    /// <returns>True if all repairs were initiated successfully.</returns>
    public async Task<bool> RepairAllGapsAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/repair-all", null,
                _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                Gaps.Clear();
                HasNoGaps = true;
                _notificationService.ShowNotification("Repair Started", "Initiated repair for all gaps.", NotificationType.Success);
                return true;
            }

            _notificationService.ShowNotification("Repair Failed", "Failed to initiate gap repairs. Please try again.", NotificationType.Warning);
            return false;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to repair all gaps", ex);
            _notificationService.ShowNotification("Repair Failed", "An error occurred while initiating gap repairs.", NotificationType.Error);
            return false;
        }
    }

    public async Task RunQualityCheckAsync(string path)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/storage/quality/score?path={Uri.EscapeDataString(path)}", null);

            if (response.IsSuccessStatusCode)
            {
                _notificationService.ShowNotification("Quality Check Complete", "Quality check completed successfully.", NotificationType.Success);
                await RefreshDataAsync();
            }
            else
            {
                _notificationService.ShowNotification("Quality Check Failed", "Failed to run quality check. Please verify the path or symbol.", NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to run quality check", ex);
            _notificationService.ShowNotification("Quality Check Failed", "An error occurred while running the quality check.", NotificationType.Error);
        }
    }

    /// <summary>Fetches provider comparison data for a symbol. Returns default JsonElement on failure.</summary>
    public async Task<JsonElement> GetProviderComparisonAsync(string symbol)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/comparison/{Uri.EscapeDataString(symbol)}",
                _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_cts?.Token ?? CancellationToken.None);
                return JsonSerializer.Deserialize<JsonElement>(json);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load provider comparison for symbol", ex, ("symbol", symbol));
        }

        return default;
    }

    // ── Static helpers ──────────────────────────────────────────────────────
    private AlertModel? BuildAlertModel(JsonElement alert)
    {
        if (alert.ValueKind != JsonValueKind.Object) return null;

        var id = alert.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
        var symbol = alert.TryGetProperty("symbol", out var sv) ? sv.GetString() ?? "" : "";
        var type = alert.TryGetProperty("type", out var tv) ? tv.GetString() ?? "" : "";
        var description = alert.TryGetProperty("description", out var dv) ? dv.GetString() ?? "" : "";
        var severity = alert.TryGetProperty("severity", out var sev) ? ReadEnumString(sev, AnomalySeverityNames) : "Warning";

        return new AlertModel
        {
            Id = id,
            Symbol = symbol,
            AlertType = type,
            Message = description,
            Severity = severity,
            SeverityBrush = new SolidColorBrush(severity.ToLowerInvariant() switch
            {
                "critical" or "error" => Color.FromRgb(244, 67, 54),
                "warning" => Color.FromRgb(255, 193, 7),
                _ => Color.FromRgb(33, 150, 243)
            })
        };
    }

    private static AnomalyModel? BuildAnomalyModel(JsonElement anomaly)
    {
        if (anomaly.ValueKind != JsonValueKind.Object) return null;

        var symbol = anomaly.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
        var description = anomaly.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var severity = anomaly.TryGetProperty("severity", out var sev) ? ReadEnumString(sev, AnomalySeverityNames) : "Warning";
        var type = anomaly.TryGetProperty("type", out var t) ? ReadEnumString(t, AnomalyTypeNames) : "";
        var timestamp = anomaly.TryGetProperty("detectedAt", out var ts) ? ts.GetDateTimeOffset() : DateTimeOffset.UtcNow;

        return new AnomalyModel
        {
            Symbol = symbol,
            Description = description,
            Timestamp = timestamp.ToString("MMM d HH:mm"),
            Type = type,
            SeverityColor = new SolidColorBrush(severity.ToLowerInvariant() switch
            {
                "critical" or "error" => Color.FromRgb(244, 67, 54),
                "warning" => Color.FromRgb(255, 193, 7),
                _ => Color.FromRgb(139, 148, 158)
            })
        };
    }

    public static List<TrendPoint> BuildTrendPoints(double baseScore, string window)
    {
        var count = window switch { "1d" => 6, "7d" => 7, "30d" => 10, "90d" => 12, _ => 7 };
        var points = new List<TrendPoint>(count);
        for (var i = 0; i < count; i++)
        {
            var factor = count == 1 ? 0 : i / (double)(count - 1);
            var delta = Math.Sin(factor * Math.PI) * 2.0 - 1.0;
            var score = Math.Clamp(baseScore + delta, 80, 100);
            var label = window switch
            {
                "1d" => DateTime.UtcNow.AddHours(-(count - 1 - i) * 4).ToString("HH:mm"),
                "7d" => DateTime.UtcNow.AddDays(-(count - 1 - i)).ToString("ddd"),
                "30d" => DateTime.UtcNow.AddDays(-(count - 1 - i) * 3).ToString("MMM d"),
                "90d" => DateTime.UtcNow.AddDays(-(count - 1 - i) * 7).ToString("MMM d"),
                _ => DateTime.UtcNow.AddDays(-(count - 1 - i)).ToString("MMM d")
            };
            points.Add(new TrendPoint(score, label));
        }
        return points;
    }

    public static string GetTimeWindowLabel(string window) => window switch
    {
        "1d" => "day",
        "7d" => "week",
        "30d" => "month",
        "90d" => "quarter",
        _ => "period"
    };

    private static Color ScoreToColor(double score) => score switch
    {
        >= 90 => Color.FromRgb(63, 185, 80),
        >= 75 => Color.FromRgb(33, 150, 243),
        >= 50 => Color.FromRgb(255, 193, 7),
        _ => Color.FromRgb(244, 67, 54)
    };

    private static string ReadEnumString(JsonElement element, IReadOnlyList<string> names)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var value) && value >= 0 && value < names.Count => names[value],
            _ => element.ToString()
        };
    }

    private static int GetGradeCount(JsonElement gradeDistribution, string grade)
    {
        if (gradeDistribution.ValueKind != JsonValueKind.Object) return 0;
        return gradeDistribution.TryGetProperty(grade, out var value) ? value.GetInt32() : 0;
    }

    private static int GetAnomalyCount(JsonElement stats, string type)
    {
        if (stats.ValueKind != JsonValueKind.Object) return 0;
        return stats.TryGetProperty(type, out var value) ? value.GetInt32() : 0;
    }

    private static int GetRangeCount(string range, int oneDay, int sevenDay, int thirtyDay, int ninetyDay) =>
        range switch { "1d" => oneDay, "7d" => sevenDay, "30d" => thirtyDay, "90d" => ninetyDay, _ => sevenDay };

    private static string FormatRelativeTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        return span.TotalSeconds < 60 ? "Just now"
            : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} minutes ago"
            : span.TotalHours < 24 ? $"{(int)span.TotalHours} hours ago"
            : $"{(int)span.TotalDays} days ago";
    }

    public static string GetGrade(double score) => score switch
    {
        >= 95 => "A+", >= 90 => "A", >= 85 => "A-", >= 80 => "B+", >= 75 => "B",
        >= 70 => "B-", >= 65 => "C+", >= 60 => "C", >= 55 => "C-", >= 50 => "D", _ => "F"
    };

    public static string GetStatus(double score) => score switch
    {
        >= 90 => "Excellent", >= 75 => "Healthy", >= 50 => "Warning", _ => "Critical"
    };

    private static readonly string[] HealthStateNames = { "Healthy", "Degraded", "Unhealthy", "Stale", "Unknown" };
    private static readonly string[] AnomalySeverityNames = { "Info", "Warning", "Error", "Critical" };
    private static readonly string[] AnomalyTypeNames =
    {
        "PriceSpike", "PriceDrop", "VolumeSpike", "VolumeDrop", "SpreadWide", "StaleData",
        "RapidPriceChange", "AbnormalVolatility", "MissingData", "DuplicateData",
        "CrossedMarket", "InvalidPrice", "InvalidVolume"
    };

    public void Dispose() => Stop();
}
