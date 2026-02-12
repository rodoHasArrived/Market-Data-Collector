using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;
using Timer = System.Timers.Timer;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Data quality monitoring page showing completeness, gaps, and anomalies.
/// </summary>
public partial class DataQualityPage : Page
{
    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<SymbolQualityModel> _symbolQuality = new();
    private readonly ObservableCollection<SymbolQualityModel> _filteredSymbols = new();
    private readonly ObservableCollection<GapModel> _gaps = new();
    private readonly ObservableCollection<AlertModel> _alerts = new();
    private readonly ObservableCollection<AnomalyModel> _anomalies = new();
    private readonly List<AlertModel> _allAlerts = new();
    private readonly List<AnomalyModel> _allAnomalies = new();
    private Timer? _refreshTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _timeRange = "7d";
    private double _lastOverallScore = 98.5;

    private readonly StatusService _statusService;
    private readonly LoggingService _loggingService;
    private readonly NotificationService _notificationService;

    public DataQualityPage(
        StatusService statusService,
        LoggingService loggingService,
        NotificationService notificationService)
    {
        InitializeComponent();

        _statusService = statusService;
        _loggingService = loggingService;
        _notificationService = notificationService;

        SymbolQualityList.ItemsSource = _filteredSymbols;
        GapsControl.ItemsSource = _gaps;
        AlertsList.ItemsSource = _alerts;
        AnomaliesList.ItemsSource = _anomalies;

        _baseUrl = _statusService.BaseUrl;

        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();

        _refreshTimer = new Timer(30000);
        _refreshTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(RefreshDataAsync);
        _refreshTimer.Start();
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
            UpdateTrendDisplay();

            LastUpdateText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to refresh data quality", ex);
        }
    }

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
                OverallScoreText.Text = score > 0 ? $"{score:F1}" : "--";
                OverallGradeText.Text = GetGrade(score);
                StatusText.Text = GetStatus(score);

                var statusBrush = score switch
                {
                    >= 90 => (Brush)Resources["SuccessColorBrush"],
                    >= 75 => (Brush)Resources["InfoColorBrush"],
                    >= 50 => (Brush)Resources["WarningColorBrush"],
                    _ => (Brush)Resources["ErrorColorBrush"]
                };
                StatusBadge.Background = statusBrush;
                OverallScoreText.Foreground = statusBrush;
                ScoreRing.Stroke = statusBrush;
                ScoreRing.StrokeDashArray = new DoubleCollection { score, Math.Max(0, 100 - score) };

                if (metrics.TryGetProperty("averageLatencyMs", out var avgLatency))
                {
                    LatencyText.Text = $"{avgLatency.GetDouble():F0}ms";
                }

                _symbolQuality.Clear();
                if (metrics.TryGetProperty("symbolHealth", out var symbolHealth) && symbolHealth.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sym in symbolHealth.EnumerateArray())
                    {
                        var symbol = sym.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var scoreValue = sym.TryGetProperty("score", out var q) ? q.GetDouble() * 100 : 0;
                        var state = sym.TryGetProperty("state", out var st)
                            ? ReadEnumString(st, HealthStateNames)
                            : "Unknown";
                        var lastEvent = sym.TryGetProperty("lastEvent", out var le) ? le.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                        var issues = sym.TryGetProperty("activeIssues", out var ai) && ai.ValueKind == JsonValueKind.Array
                            ? string.Join(", ", ai.EnumerateArray().Select(i => i.GetString()).Where(i => !string.IsNullOrWhiteSpace(i)))
                            : "";

                        _symbolQuality.Add(new SymbolQualityModel
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

                ApplySymbolFilter();
            }

            if (data.TryGetProperty("completenessStats", out var completeness))
            {
                if (completeness.TryGetProperty("averageScore", out var avgScore))
                {
                    var value = avgScore.GetDouble() * 100;
                    CompletenessText.Text = value > 0 ? $"{value:F1}%" : "--";
                }

                var gradeDistribution = completeness.TryGetProperty("gradeDistribution", out var dist)
                    ? dist
                    : default;

                var healthy = GetGradeCount(gradeDistribution, "A") + GetGradeCount(gradeDistribution, "B");
                var warning = GetGradeCount(gradeDistribution, "C");
                var critical = GetGradeCount(gradeDistribution, "D") + GetGradeCount(gradeDistribution, "F");

                HealthyFilesText.Text = healthy.ToString("N0");
                WarningFilesText.Text = warning.ToString("N0");
                CriticalFilesText.Text = critical.ToString("N0");

                if (completeness.TryGetProperty("calculatedAt", out var calculatedAt))
                {
                    var timestamp = calculatedAt.GetDateTimeOffset();
                    LastCheckTimeText.Text = FormatRelativeTime(timestamp.UtcDateTime);
                    NextCheckText.Text = "In 30 minutes";
                    CheckProgress.Value = Math.Min(100, (DateTimeOffset.UtcNow - timestamp).TotalMinutes / 30 * 100);
                }
            }

            if (data.TryGetProperty("gapStats", out var gaps))
            {
                if (gaps.TryGetProperty("totalGaps", out var totalGaps))
                {
                    var gapCount = totalGaps.GetInt32();
                    GapsCountText.Text = gapCount.ToString();
                    GapsCountText.Foreground = new SolidColorBrush(
                        gapCount == 0 ? Color.FromRgb(63, 185, 80) :
                        gapCount <= 5 ? Color.FromRgb(255, 193, 7) :
                        Color.FromRgb(244, 67, 54));
                }
            }

            if (data.TryGetProperty("sequenceStats", out var sequenceStats))
            {
                if (sequenceStats.TryGetProperty("totalErrors", out var totalErrors))
                {
                    var errors = totalErrors.GetInt64();
                    ErrorsCountText.Text = errors.ToString("N0");
                    ErrorsCountText.Foreground = new SolidColorBrush(
                        errors == 0 ? Color.FromRgb(63, 185, 80) :
                        Color.FromRgb(244, 67, 54));
                }
            }

            if (data.TryGetProperty("anomalyStats", out var anomalyStats))
            {
                if (anomalyStats.TryGetProperty("unacknowledgedCount", out var unack))
                {
                    var unackCount = unack.GetInt32();
                    UnacknowledgedText.Text = unackCount.ToString();

                    if (unackCount > 0)
                    {
                        AlertCountBadge.Visibility = Visibility.Visible;
                        AlertCountText.Text = unackCount.ToString();
                    }
                    else
                    {
                        AlertCountBadge.Visibility = Visibility.Collapsed;
                    }
                }

                if (anomalyStats.TryGetProperty("totalAnomalies", out var total))
                {
                    TotalActiveAlertsText.Text = total.GetInt64().ToString("N0");
                }

                if (anomalyStats.TryGetProperty("anomaliesByType", out var anomaliesByType))
                {
                    CrossedMarketCount.Text = GetAnomalyCount(anomaliesByType, "CrossedMarket").ToString();
                    StaleDataCount.Text = GetAnomalyCount(anomaliesByType, "StaleData").ToString();
                    InvalidPriceCount.Text = GetAnomalyCount(anomaliesByType, "InvalidPrice").ToString();
                    InvalidVolumeCount.Text = GetAnomalyCount(anomaliesByType, "InvalidVolume").ToString();
                    MissingDataCount.Text = GetAnomalyCount(anomaliesByType, "MissingData").ToString();
                }
            }

            if (data.TryGetProperty("recentAnomalies", out var recentAnomalies) && recentAnomalies.ValueKind == JsonValueKind.Array)
            {
                _allAlerts.Clear();
                foreach (var alert in recentAnomalies.EnumerateArray())
                {
                    var model = BuildAlertModel(alert);
                    if (model != null)
                    {
                        _allAlerts.Add(model);
                    }
                }

                ApplyAlertFilter();
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
        OverallScoreText.Text = "98.5";
        OverallGradeText.Text = "A+";
        StatusText.Text = "Excellent";
        StatusBadge.Background = (Brush)Resources["SuccessColorBrush"];
        ScoreRing.StrokeDashArray = new DoubleCollection { 98.5, 1.5 };

        HealthyFilesText.Text = "1,234";
        WarningFilesText.Text = "12";
        CriticalFilesText.Text = "0";

        UnacknowledgedText.Text = "2";
        TotalActiveAlertsText.Text = "5";
        AlertCountBadge.Visibility = Visibility.Visible;
        AlertCountText.Text = "2";

        LastCheckTimeText.Text = "2 minutes ago";
        NextCheckText.Text = "In 28 minutes";
        CheckProgress.Value = 6;

        CompletenessText.Text = "98.5%";
        GapsCountText.Text = "3";
        ErrorsCountText.Text = "0";
        LatencyText.Text = "12ms";

        _symbolQuality.Clear();
        _symbolQuality.Add(CreateDemoSymbolQuality("SPY", 99.8, "Healthy"));
        _symbolQuality.Add(CreateDemoSymbolQuality("AAPL", 98.2, "Healthy"));
        _symbolQuality.Add(CreateDemoSymbolQuality("MSFT", 97.5, "Healthy"));
        _symbolQuality.Add(CreateDemoSymbolQuality("GOOGL", 94.8, "Degraded"));
        _symbolQuality.Add(CreateDemoSymbolQuality("AMZN", 96.1, "Healthy"));
        ApplySymbolFilter();

        _allAlerts.Clear();
        _allAlerts.Add(new AlertModel
        {
            Id = "alert-1",
            Symbol = "AAPL",
            AlertType = "StaleData",
            Message = "No trades received in the last 3 minutes",
            Severity = "Warning",
            SeverityBrush = (Brush)Resources["WarningColorBrush"]
        });
        _allAlerts.Add(new AlertModel
        {
            Id = "alert-2",
            Symbol = "GOOGL",
            AlertType = "CrossedMarket",
            Message = "Bid price exceeded ask for 2 ticks",
            Severity = "Critical",
            SeverityBrush = (Brush)Resources["ErrorColorBrush"]
        });
        ApplyAlertFilter();
    }

    private static SymbolQualityModel CreateDemoSymbolQuality(string symbol, double score, string status)
    {
        return new SymbolQualityModel
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
    }

    private async Task LoadGapsAsync(CancellationToken ct)
    {
        try
        {
            var count = GetRangeCount(_timeRange, 100, 250, 500, 1000);
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/gaps?count={count}", ct);

            _gaps.Clear();

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var gap in data.EnumerateArray())
                    {
                        var gapId = gap.TryGetProperty("gapStart", out var gapStart)
                            ? gapStart.GetDateTimeOffset().ToString("O")
                            : Guid.NewGuid().ToString();
                        var symbol = gap.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var start = gap.TryGetProperty("gapStart", out var st) ? st.GetDateTimeOffset() : DateTimeOffset.MinValue;
                        var end = gap.TryGetProperty("gapEnd", out var et) ? et.GetDateTimeOffset() : DateTimeOffset.MinValue;
                        var missingBars = gap.TryGetProperty("estimatedMissedEvents", out var mb) ? mb.GetInt64() : 0;

                        var duration = end - start;
                        var durationText = duration.TotalDays >= 1 ? $"{duration.TotalDays:F0} days" :
                                          duration.TotalHours >= 1 ? $"{duration.TotalHours:F0} hours" :
                                          $"{duration.TotalMinutes:F0} mins";

                        _gaps.Add(new GapModel
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

            NoGapsText.Visibility = _gaps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (HttpRequestException)
        {
            LoadDemoGaps();
        }
    }

    private void LoadDemoGaps()
    {
        _gaps.Clear();
        _gaps.Add(new GapModel
        {
            GapId = "gap-1",
            Symbol = "AAPL",
            Description = "Missing 156 events between 2024-01-15 09:30 and 2024-01-17 16:00",
            Duration = "2 days"
        });
        _gaps.Add(new GapModel
        {
            GapId = "gap-2",
            Symbol = "GOOGL",
            Description = "Missing 45 events between 2024-01-20 14:00 and 2024-01-20 15:30",
            Duration = "1.5 hours"
        });
        _gaps.Add(new GapModel
        {
            GapId = "gap-3",
            Symbol = "MSFT",
            Description = "Missing 12 events between 2024-01-22 10:00 and 2024-01-22 10:15",
            Duration = "15 mins"
        });

        NoGapsText.Visibility = _gaps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadAnomaliesAsync(CancellationToken ct)
    {
        try
        {
            var count = GetRangeCount(_timeRange, 50, 100, 200, 400);
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/anomalies?count={count}", ct);

            _anomalies.Clear();
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
                        if (model != null)
                        {
                            _allAnomalies.Add(model);
                        }
                    }
                }
            }

            ApplyAnomalyFilter();
        }
        catch (HttpRequestException)
        {
            _anomalies.Clear();
            _allAnomalies.Clear();
            NoAnomaliesText.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadLatencyDistributionAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/latency/statistics", ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                P50Text.Text = data.TryGetProperty("globalP50Ms", out var p50) ? $"{p50.GetDouble():F0}ms" : "--";
                P75Text.Text = data.TryGetProperty("globalMeanMs", out var mean) ? $"{mean.GetDouble():F0}ms" : "--";
                P90Text.Text = data.TryGetProperty("globalP90Ms", out var p90) ? $"{p90.GetDouble():F0}ms" : "--";
                P95Text.Text = data.TryGetProperty("globalP90Ms", out var p95) ? $"{p95.GetDouble():F0}ms" : "--";
                P99Text.Text = data.TryGetProperty("globalP99Ms", out var p99) ? $"{p99.GetDouble():F0}ms" : "--";
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
        P50Text.Text = "8ms";
        P75Text.Text = "12ms";
        P90Text.Text = "18ms";
        P95Text.Text = "25ms";
        P99Text.Text = "45ms";
    }

    private void TimeWindow_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TimeWindowCombo.SelectedItem is ComboBoxItem item && item.Tag is string window)
        {
            _timeRange = window;
            UpdateTrendDisplay();
            _ = RefreshDataAsync();
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = RefreshDataAsync();
        _notificationService.ShowNotification(
            "Refreshed",
            "Data quality metrics have been refreshed.",
            NotificationType.Info);
    }

    private async void RunQualityCheck_Click(object sender, RoutedEventArgs e)
    {
        var path = PromptForQualityCheckPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/storage/quality/score?path={Uri.EscapeDataString(path)}",
                null);

            if (response.IsSuccessStatusCode)
            {
                _notificationService.ShowNotification(
                    "Quality Check Complete",
                    "Quality check completed successfully.",
                    NotificationType.Success);
                await RefreshDataAsync();
            }
            else
            {
                _notificationService.ShowNotification(
                    "Quality Check Failed",
                    "Failed to run quality check. Please verify the path or symbol.",
                    NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to run quality check", ex);
            _notificationService.ShowNotification(
                "Quality Check Failed",
                "An error occurred while running the quality check.",
                NotificationType.Error);
        }
    }

    private async void RepairGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string gapId) return;

        var gap = _gaps.FirstOrDefault(g => g.GapId == gapId);
        if (gap == null) return;

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/{gapId}/repair",
                null,
                _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                _gaps.Remove(gap);
                NoGapsText.Visibility = _gaps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                _notificationService.ShowNotification(
                    "Gap Repair Started",
                    $"Repair for {gap.Symbol} gap has been initiated.",
                    NotificationType.Success);
            }
            else
            {
                _notificationService.ShowNotification(
                    "Repair Failed",
                    "Failed to initiate gap repair. Please try again.",
                    NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to repair gap", ex);
            _notificationService.ShowNotification(
                "Repair Failed",
                "An error occurred while initiating gap repair.",
                NotificationType.Error);
        }
    }

    private async void RepairAllGaps_Click(object sender, RoutedEventArgs e)
    {
        if (_gaps.Count == 0)
        {
            _notificationService.ShowNotification(
                "No Gaps",
                "There are no gaps to repair.",
                NotificationType.Info);
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/repair-all",
                null,
                _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                var count = _gaps.Count;
                _gaps.Clear();
                NoGapsText.Visibility = Visibility.Visible;

                _notificationService.ShowNotification(
                    "Repair Started",
                    $"Initiated repair for {count} gap(s).",
                    NotificationType.Success);
            }
            else
            {
                _notificationService.ShowNotification(
                    "Repair Failed",
                    "Failed to initiate gap repairs. Please try again.",
                    NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to repair all gaps", ex);
            _notificationService.ShowNotification(
                "Repair Failed",
                "An error occurred while initiating gap repairs.",
                NotificationType.Error);
        }
    }

    private void SymbolFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySymbolFilter();
    }

    private void SymbolQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolQualityList.SelectedItem is SymbolQualityModel selected)
        {
            _notificationService.ShowNotification(
                "Symbol Selected",
                $"Viewing data quality details for {selected.Symbol}.",
                NotificationType.Info);
        }
    }

    private void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyAlertFilter();
    }

    private async void AcknowledgeAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string alertId)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/quality/anomalies/{alertId}/acknowledge",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    _allAlerts.RemoveAll(a => a.Id == alertId);
                    ApplyAlertFilter();
                    await RefreshDataAsync();
                }
                else
                {
                    _notificationService.ShowNotification(
                        "Acknowledge Failed",
                        "Failed to acknowledge alert.",
                        NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to acknowledge alert", ex);
                _notificationService.ShowNotification(
                    "Acknowledge Failed",
                    "An error occurred while acknowledging the alert.",
                    NotificationType.Error);
            }
        }
    }

    private async void AcknowledgeAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var alert in _allAlerts.ToList())
        {
            try
            {
                await _httpClient.PostAsync(
                    $"{_baseUrl}/api/quality/anomalies/{alert.Id}/acknowledge",
                    null);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to acknowledge alert", ex);
            }
        }

        _allAlerts.Clear();
        ApplyAlertFilter();
        await RefreshDataAsync();

        _notificationService.ShowNotification(
            "All Alerts Acknowledged",
            "All alerts have been acknowledged.",
            NotificationType.Success);
    }

    private void AnomalyType_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyAnomalyFilter();
    }

    private void ApplySymbolFilter()
    {
        var query = SymbolFilterBox.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        _filteredSymbols.Clear();

        foreach (var symbol in _symbolQuality.Where(s => string.IsNullOrEmpty(query) || s.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            _filteredSymbols.Add(symbol);
        }

        NoSymbolsText.Visibility = _filteredSymbols.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyAlertFilter()
    {
        var severity = (SeverityFilterCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        _alerts.Clear();

        foreach (var alert in _allAlerts.Where(a => severity == "All" || a.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase)))
        {
            _alerts.Add(alert);
        }

        NoAlertsText.Visibility = _alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyAnomalyFilter()
    {
        var type = (AnomalyTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        _anomalies.Clear();

        foreach (var anomaly in _allAnomalies.Where(a => type == "All" || a.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
        {
            _anomalies.Add(anomaly);
        }

        NoAnomaliesText.Visibility = _anomalies.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AnomalyCountBadge.Visibility = _anomalies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AnomalyCountText.Text = _anomalies.Count.ToString();
    }

    private void UpdateTrendDisplay()
    {
        var points = BuildTrendPoints(_lastOverallScore, _timeRange);
        if (points.Count == 0)
        {
            AvgScoreText.Text = "--";
            MinScoreText.Text = "--";
            MaxScoreText.Text = "--";
            StdDevText.Text = "--";
            return;
        }

        var scores = points.Select(p => p.Score).ToList();
        var avg = scores.Average();
        var min = scores.Min();
        var max = scores.Max();
        var stdDev = Math.Sqrt(scores.Sum(s => Math.Pow(s - avg, 2)) / scores.Count);

        AvgScoreText.Text = $"{avg:F1}%";
        MinScoreText.Text = $"{min:F1}%";
        MaxScoreText.Text = $"{max:F1}%";
        StdDevText.Text = $"{stdDev:F1}%";

        var change = scores.Last() - scores.First();
        var isPositive = change >= 0;
        TrendIcon.Text = isPositive ? "\uE70E" : "\uE70D";
        TrendText.Text = $"{(isPositive ? "+" : "")}{change:F1}% this {GetTimeWindowLabel(_timeRange)}";

        var trendBrush = change > 0.5
            ? (Brush)Resources["SuccessColorBrush"]
            : change < -0.5
                ? (Brush)Resources["ErrorColorBrush"]
                : (Brush)Resources["WarningColorBrush"];

        TrendIcon.Foreground = trendBrush;
        TrendText.Foreground = trendBrush;

        RenderTrendChart(points);
    }

    private void RenderTrendChart(IReadOnlyList<TrendPoint> points)
    {
        if (points.Count == 0)
        {
            TrendChartLine.Points = new PointCollection();
            TrendChartFill.Points = new PointCollection();
            return;
        }

        var width = TrendChart.ActualWidth;
        var height = TrendChart.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            width = 600;
            height = 200;
        }

        var maxScore = Math.Max(100, points.Max(p => p.Score));
        var minScore = Math.Min(0, points.Min(p => p.Score));

        var pointsCollection = new PointCollection();
        var fillCollection = new PointCollection();

        for (var i = 0; i < points.Count; i++)
        {
            var x = i * (width / Math.Max(1, points.Count - 1));
            var normalized = (points[i].Score - minScore) / Math.Max(1, maxScore - minScore);
            var y = height - (normalized * height);

            pointsCollection.Add(new Point(x, y));
            fillCollection.Add(new Point(x, y));
        }

        fillCollection.Add(new Point(width, height));
        fillCollection.Add(new Point(0, height));

        TrendChartLine.Points = pointsCollection;
        TrendChartFill.Points = fillCollection;

        XAxisLabels.Children.Clear();
        foreach (var label in points.Select(p => p.Label))
        {
            XAxisLabels.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = (Brush)Resources["ConsoleTextMutedBrush"],
                Margin = new Thickness(0, 0, 16, 0)
            });
        }
    }

    private static List<TrendPoint> BuildTrendPoints(double baseScore, string window)
    {
        var count = window switch
        {
            "1d" => 6,
            "7d" => 7,
            "30d" => 10,
            "90d" => 12,
            _ => 7
        };

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

    private static string GetTimeWindowLabel(string window)
    {
        return window switch
        {
            "1d" => "day",
            "7d" => "week",
            "30d" => "month",
            "90d" => "quarter",
            _ => "period"
        };
    }

    private static int GetRangeCount(string range, int oneDay, int sevenDay, int thirtyDay, int ninetyDay)
    {
        return range switch
        {
            "1d" => oneDay,
            "7d" => sevenDay,
            "30d" => thirtyDay,
            "90d" => ninetyDay,
            _ => sevenDay
        };
    }

    private static string FormatRelativeTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        return span.TotalSeconds < 60 ? "Just now"
             : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} minutes ago"
             : span.TotalHours < 24 ? $"{(int)span.TotalHours} hours ago"
             : $"{(int)span.TotalDays} days ago";
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

    private static int GetGradeCount(JsonElement gradeDistribution, string grade)
    {
        if (gradeDistribution.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return gradeDistribution.TryGetProperty(grade, out var value) ? value.GetInt32() : 0;
    }

    private static int GetAnomalyCount(JsonElement anomalyStats, string type)
    {
        if (anomalyStats.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return anomalyStats.TryGetProperty(type, out var value) ? value.GetInt32() : 0;
    }

    private AlertModel? BuildAlertModel(JsonElement alert)
    {
        if (alert.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = alert.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
        var symbol = alert.TryGetProperty("symbol", out var symbolValue) ? symbolValue.GetString() ?? "" : "";
        var type = alert.TryGetProperty("type", out var typeValue) ? typeValue.GetString() ?? "" : "";
        var description = alert.TryGetProperty("description", out var descValue) ? descValue.GetString() ?? "" : "";
        var severity = alert.TryGetProperty("severity", out var severityValue)
            ? ReadEnumString(severityValue, AnomalySeverityNames)
            : "Warning";

        return new AlertModel
        {
            Id = id,
            Symbol = symbol,
            AlertType = type,
            Message = description,
            Severity = severity,
            SeverityBrush = severity.ToLowerInvariant() switch
            {
                "critical" or "error" => (Brush)Resources["ErrorColorBrush"],
                "warning" => (Brush)Resources["WarningColorBrush"],
                _ => (Brush)Resources["InfoColorBrush"]
            }
        };
    }

    private static AnomalyModel? BuildAnomalyModel(JsonElement anomaly)
    {
        if (anomaly.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var symbol = anomaly.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
        var description = anomaly.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var severity = anomaly.TryGetProperty("severity", out var sev)
            ? ReadEnumString(sev, AnomalySeverityNames)
            : "Warning";
        var type = anomaly.TryGetProperty("type", out var t)
            ? ReadEnumString(t, AnomalyTypeNames)
            : "";
        var timestamp = anomaly.TryGetProperty("detectedAt", out var ts) ? ts.GetDateTimeOffset() : DateTimeOffset.UtcNow;

        return new AnomalyModel
        {
            Symbol = symbol,
            Description = description,
            Timestamp = timestamp.ToString("MMM d HH:mm"),
            Type = type,
            SeverityColor = severity.ToLowerInvariant() switch
            {
                "critical" or "error" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                "warning" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                _ => new SolidColorBrush(Color.FromRgb(139, 148, 158))
            }
        };
    }

    private static string ReadEnumString(JsonElement element, IReadOnlyList<string> names)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var value) && value >= 0 && value < names.Count => names[value],
            _ => element.ToString()
        };
    }

    private static readonly string[] HealthStateNames = { "Healthy", "Degraded", "Unhealthy", "Stale", "Unknown" };
    private static readonly string[] AnomalySeverityNames = { "Info", "Warning", "Error", "Critical" };
    private static readonly string[] AnomalyTypeNames =
    {
        "PriceSpike",
        "PriceDrop",
        "VolumeSpike",
        "VolumeDrop",
        "SpreadWide",
        "StaleData",
        "RapidPriceChange",
        "AbnormalVolatility",
        "MissingData",
        "DuplicateData",
        "CrossedMarket",
        "InvalidPrice",
        "InvalidVolume"
    };

    private static string? PromptForQualityCheckPath()
    {
        var window = new Window
        {
            Title = "Run Quality Check",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };

        var textBox = new TextBox
        {
            Margin = new Thickness(0, 12, 0, 12),
            MinWidth = 320
        };

        var okButton = new Button
        {
            Content = "Run",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonsPanel.Children.Add(okButton);
        buttonsPanel.Children.Add(cancelButton);

        var stack = new StackPanel
        {
            Margin = new Thickness(16)
        };
        stack.Children.Add(new TextBlock { Text = "Enter path or symbol to check:" });
        stack.Children.Add(textBox);
        stack.Children.Add(buttonsPanel);

        window.Content = stack;

        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = textBox.Text;
            window.DialogResult = true;
            window.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            window.DialogResult = false;
            window.Close();
        };

        window.ShowDialog();
        return result;
    }
}

public class TrendPoint
{
    public TrendPoint(double score, string label)
    {
        Score = score;
        Label = label;
    }

    public double Score { get; }
    public string Label { get; }
}

/// <summary>
/// Model for symbol quality display.
/// </summary>
public class SymbolQualityModel
{
    public string Symbol { get; set; } = string.Empty;
    public double Score { get; set; }
    public string ScoreFormatted { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Issues { get; set; } = string.Empty;
    public DateTimeOffset LastUpdate { get; set; }
    public string LastUpdateFormatted { get; set; } = string.Empty;
}

/// <summary>
/// Model for gap display.
/// </summary>
public class GapModel
{
    public string GapId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

public class AlertModel
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Brush SeverityBrush { get; set; } = Brushes.Gray;
}

/// <summary>
/// Model for anomaly display.
/// </summary>
public class AnomalyModel
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public SolidColorBrush SeverityColor { get; set; } = new(Colors.Gray);
}
