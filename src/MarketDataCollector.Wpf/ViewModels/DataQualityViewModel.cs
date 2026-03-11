using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Ui.Services;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.ViewModels;

/// <summary>
/// ViewModel for the Data Quality monitoring page.
/// Owns all state, HTTP calls, timer management, and commands so the code-behind is thin.
/// </summary>
public sealed class DataQualityViewModel : BindableBase, IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly WpfServices.StatusService _statusService;
    private readonly WpfServices.LoggingService _loggingService;
    private readonly WpfServices.NotificationService _notificationService;

    private readonly ObservableCollection<SymbolQualityModel> _symbolQuality = new();
    private readonly List<AlertModel> _allAlerts = new();
    private readonly List<AnomalyModel> _allAnomalies = new();

    private DispatcherTimer? _refreshTimer;
    private CancellationTokenSource? _cts;

    private string _baseUrl = "http://localhost:8080";
    private string _timeRange = "7d";
    private double _lastOverallScore = 98.5;
    private double _chartWidth = 600;
    private double _chartHeight = 200;

    // ── Read-only collections bound in XAML ───────────────────────────────────────
    public ObservableCollection<SymbolQualityModel> FilteredSymbols { get; } = new();
    public ObservableCollection<GapModel> Gaps { get; } = new();
    public ObservableCollection<AlertModel> Alerts { get; } = new();
    public ObservableCollection<AnomalyModel> Anomalies { get; } = new();

    // ── Overall score card ────────────────────────────────────────────────────────
    private string _overallScoreText = "--";
    public string OverallScoreText { get => _overallScoreText; private set => SetProperty(ref _overallScoreText, value); }

    private Brush _overallScoreForeground;
    public Brush OverallScoreForeground { get => _overallScoreForeground; private set => SetProperty(ref _overallScoreForeground, value); }

    private string _overallGradeText = "--";
    public string OverallGradeText { get => _overallGradeText; private set => SetProperty(ref _overallGradeText, value); }

    private string _statusText = "--";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private Brush _statusBadgeBackground;
    public Brush StatusBadgeBackground { get => _statusBadgeBackground; private set => SetProperty(ref _statusBadgeBackground, value); }

    private Brush _scoreRingStroke;
    public Brush ScoreRingStroke { get => _scoreRingStroke; private set => SetProperty(ref _scoreRingStroke, value); }

    private DoubleCollection _scoreRingDashArray = new() { 0, 100 };
    public DoubleCollection ScoreRingDashArray { get => _scoreRingDashArray; private set => SetProperty(ref _scoreRingDashArray, value); }

    private string _latencyText = "--";
    public string LatencyText { get => _latencyText; private set => SetProperty(ref _latencyText, value); }

    // ── Completeness stats ────────────────────────────────────────────────────────
    private string _completenessText = "--";
    public string CompletenessText { get => _completenessText; private set => SetProperty(ref _completenessText, value); }

    private string _healthyFilesText = "--";
    public string HealthyFilesText { get => _healthyFilesText; private set => SetProperty(ref _healthyFilesText, value); }

    private string _warningFilesText = "--";
    public string WarningFilesText { get => _warningFilesText; private set => SetProperty(ref _warningFilesText, value); }

    private string _criticalFilesText = "--";
    public string CriticalFilesText { get => _criticalFilesText; private set => SetProperty(ref _criticalFilesText, value); }

    private string _lastCheckTimeText = "--";
    public string LastCheckTimeText { get => _lastCheckTimeText; private set => SetProperty(ref _lastCheckTimeText, value); }

    private string _nextCheckText = "--";
    public string NextCheckText { get => _nextCheckText; private set => SetProperty(ref _nextCheckText, value); }

    private double _checkProgress;
    public double CheckProgress { get => _checkProgress; private set => SetProperty(ref _checkProgress, value); }

    // ── Anomaly/alert stats ───────────────────────────────────────────────────────
    private string _unacknowledgedText = "0";
    public string UnacknowledgedText { get => _unacknowledgedText; private set => SetProperty(ref _unacknowledgedText, value); }

    private bool _isAlertCountBadgeVisible;
    public bool IsAlertCountBadgeVisible { get => _isAlertCountBadgeVisible; private set => SetProperty(ref _isAlertCountBadgeVisible, value); }

    private string _alertCountText = "0";
    public string AlertCountText { get => _alertCountText; private set => SetProperty(ref _alertCountText, value); }

    private string _totalActiveAlertsText = "0";
    public string TotalActiveAlertsText { get => _totalActiveAlertsText; private set => SetProperty(ref _totalActiveAlertsText, value); }

    private string _crossedMarketCount = "0";
    public string CrossedMarketCount { get => _crossedMarketCount; private set => SetProperty(ref _crossedMarketCount, value); }

    private string _staleDataCount = "0";
    public string StaleDataCount { get => _staleDataCount; private set => SetProperty(ref _staleDataCount, value); }

    private string _invalidPriceCount = "0";
    public string InvalidPriceCount { get => _invalidPriceCount; private set => SetProperty(ref _invalidPriceCount, value); }

    private string _invalidVolumeCount = "0";
    public string InvalidVolumeCount { get => _invalidVolumeCount; private set => SetProperty(ref _invalidVolumeCount, value); }

    private string _missingDataCount = "0";
    public string MissingDataCount { get => _missingDataCount; private set => SetProperty(ref _missingDataCount, value); }

    private string _gapsCountText = "0";
    public string GapsCountText { get => _gapsCountText; private set => SetProperty(ref _gapsCountText, value); }

    private Brush _gapsCountForeground;
    public Brush GapsCountForeground { get => _gapsCountForeground; private set => SetProperty(ref _gapsCountForeground, value); }

    private string _errorsCountText = "0";
    public string ErrorsCountText { get => _errorsCountText; private set => SetProperty(ref _errorsCountText, value); }

    private Brush _errorsCountForeground;
    public Brush ErrorsCountForeground { get => _errorsCountForeground; private set => SetProperty(ref _errorsCountForeground, value); }

    // ── Latency percentiles ───────────────────────────────────────────────────────
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

    // ── Empty-state flags ─────────────────────────────────────────────────────────
    private bool _isNoGapsVisible;
    public bool IsNoGapsVisible { get => _isNoGapsVisible; private set => SetProperty(ref _isNoGapsVisible, value); }

    private bool _isNoAlertsVisible;
    public bool IsNoAlertsVisible { get => _isNoAlertsVisible; private set => SetProperty(ref _isNoAlertsVisible, value); }

    private bool _isNoAnomaliesVisible = true;
    public bool IsNoAnomaliesVisible { get => _isNoAnomaliesVisible; private set => SetProperty(ref _isNoAnomaliesVisible, value); }

    private bool _isAnomalyCountBadgeVisible;
    public bool IsAnomalyCountBadgeVisible { get => _isAnomalyCountBadgeVisible; private set => SetProperty(ref _isAnomalyCountBadgeVisible, value); }

    private string _anomalyCountText = "0";
    public string AnomalyCountText { get => _anomalyCountText; private set => SetProperty(ref _anomalyCountText, value); }

    private bool _isNoSymbolsVisible;
    public bool IsNoSymbolsVisible { get => _isNoSymbolsVisible; private set => SetProperty(ref _isNoSymbolsVisible, value); }

    private string _lastUpdateText = string.Empty;
    public string LastUpdateText { get => _lastUpdateText; private set => SetProperty(ref _lastUpdateText, value); }

    // ── Trend stats ───────────────────────────────────────────────────────────────
    private string _avgScoreText = "--";
    public string AvgScoreText { get => _avgScoreText; private set => SetProperty(ref _avgScoreText, value); }

    private string _minScoreText = "--";
    public string MinScoreText { get => _minScoreText; private set => SetProperty(ref _minScoreText, value); }

    private string _maxScoreText = "--";
    public string MaxScoreText { get => _maxScoreText; private set => SetProperty(ref _maxScoreText, value); }

    private string _stdDevText = "--";
    public string StdDevText { get => _stdDevText; private set => SetProperty(ref _stdDevText, value); }

    private string _trendIconGlyph = string.Empty;
    public string TrendIconGlyph { get => _trendIconGlyph; private set => SetProperty(ref _trendIconGlyph, value); }

    private string _trendText = string.Empty;
    public string TrendText { get => _trendText; private set => SetProperty(ref _trendText, value); }

    private Brush _trendBrush;
    public Brush TrendBrush { get => _trendBrush; private set => SetProperty(ref _trendBrush, value); }

    // ── Trend chart data ─────────────────────────────────────────────────────────
    private PointCollection _trendChartLinePoints = new();
    public PointCollection TrendChartLinePoints { get => _trendChartLinePoints; private set => SetProperty(ref _trendChartLinePoints, value); }

    private PointCollection _trendChartFillPoints = new();
    public PointCollection TrendChartFillPoints { get => _trendChartFillPoints; private set => SetProperty(ref _trendChartFillPoints, value); }

    private IReadOnlyList<string> _trendXAxisLabels = Array.Empty<string>();
    public IReadOnlyList<string> TrendXAxisLabels { get => _trendXAxisLabels; private set => SetProperty(ref _trendXAxisLabels, value); }

    // ── Symbol filter (two-way) ───────────────────────────────────────────────────
    private string _symbolFilter = string.Empty;
    public string SymbolFilter
    {
        get => _symbolFilter;
        set { if (SetProperty(ref _symbolFilter, value)) ApplySymbolFilter(); }
    }

    private string _alertSeverityFilter = "All";
    public string AlertSeverityFilter
    {
        get => _alertSeverityFilter;
        set { if (SetProperty(ref _alertSeverityFilter, value)) ApplyAlertFilter(); }
    }

    private string _anomalyTypeFilter = "All";
    public string AnomalyTypeFilter
    {
        get => _anomalyTypeFilter;
        set { if (SetProperty(ref _anomalyTypeFilter, value)) ApplyAnomalyFilter(); }
    }

    // ── Drilldown ─────────────────────────────────────────────────────────────────
    private bool _isDrilldownVisible;
    public bool IsDrilldownVisible { get => _isDrilldownVisible; private set => SetProperty(ref _isDrilldownVisible, value); }

    private string _drilldownSymbolHeader = string.Empty;
    public string DrilldownSymbolHeader { get => _drilldownSymbolHeader; private set => SetProperty(ref _drilldownSymbolHeader, value); }

    private string _drilldownScoreText = string.Empty;
    public string DrilldownScoreText { get => _drilldownScoreText; private set => SetProperty(ref _drilldownScoreText, value); }

    private Brush _drilldownScoreForeground;
    public Brush DrilldownScoreForeground { get => _drilldownScoreForeground; private set => SetProperty(ref _drilldownScoreForeground, value); }

    private string _drilldownCompletenessText = "--";
    public string DrilldownCompletenessText { get => _drilldownCompletenessText; private set => SetProperty(ref _drilldownCompletenessText, value); }

    private string _drilldownGapsText = "--";
    public string DrilldownGapsText { get => _drilldownGapsText; private set => SetProperty(ref _drilldownGapsText, value); }

    private string _drilldownErrorsText = "--";
    public string DrilldownErrorsText { get => _drilldownErrorsText; private set => SetProperty(ref _drilldownErrorsText, value); }

    private string _drilldownLatencyText = "--";
    public string DrilldownLatencyText { get => _drilldownLatencyText; private set => SetProperty(ref _drilldownLatencyText, value); }

    private IReadOnlyList<HeatmapCellModel> _heatmapCells = Array.Empty<HeatmapCellModel>();
    public IReadOnlyList<HeatmapCellModel> HeatmapCells { get => _heatmapCells; private set => SetProperty(ref _heatmapCells, value); }

    public ObservableCollection<DrilldownIssue> DrilldownIssues { get; } = new();

    private bool _isNoDrilldownIssuesVisible = true;
    public bool IsNoDrilldownIssuesVisible { get => _isNoDrilldownIssuesVisible; private set => SetProperty(ref _isNoDrilldownIssuesVisible, value); }

    // ── Commands ──────────────────────────────────────────────────────────────────
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<string> RunQualityCheckCommand { get; }
    public IAsyncRelayCommand<string> RepairGapCommand { get; }
    public IAsyncRelayCommand RepairAllGapsCommand { get; }
    public IAsyncRelayCommand<string> AcknowledgeAlertCommand { get; }
    public IAsyncRelayCommand AcknowledgeAllAlertsCommand { get; }
    public IRelayCommand<SymbolQualityModel?> SelectSymbolCommand { get; }
    public IRelayCommand CloseDrilldownCommand { get; }
    public IRelayCommand<string> SetTimeRangeCommand { get; }

    // ── Brushes cached at construction ────────────────────────────────────────────
    private readonly Brush _successBrush;
    private readonly Brush _errorBrush;
    private readonly Brush _warningBrush;
    private readonly Brush _infoBrush;
    private readonly Brush _mutedBrush;

    public DataQualityViewModel(
        WpfServices.StatusService statusService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        _statusService = statusService;
        _loggingService = loggingService;
        _notificationService = notificationService;
        _baseUrl = _statusService.BaseUrl;

        _successBrush = (Brush)Application.Current.Resources["SuccessColorBrush"];
        _errorBrush = (Brush)Application.Current.Resources["ErrorColorBrush"];
        _warningBrush = (Brush)Application.Current.Resources["WarningColorBrush"];
        _infoBrush = (Brush)Application.Current.Resources["InfoColorBrush"];
        _mutedBrush = (Brush)Application.Current.Resources["ConsoleTextMutedBrush"];

        _overallScoreForeground = _successBrush;
        _statusBadgeBackground = _successBrush;
        _scoreRingStroke = _successBrush;
        _gapsCountForeground = _successBrush;
        _errorsCountForeground = _successBrush;
        _trendBrush = _successBrush;
        _drilldownScoreForeground = _successBrush;

        RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
        RunQualityCheckCommand = new AsyncRelayCommand<string>(RunQualityCheckAsync);
        RepairGapCommand = new AsyncRelayCommand<string>(RepairGapAsync);
        RepairAllGapsCommand = new AsyncRelayCommand(RepairAllGapsAsync);
        AcknowledgeAlertCommand = new AsyncRelayCommand<string>(AcknowledgeAlertAsync);
        AcknowledgeAllAlertsCommand = new AsyncRelayCommand(AcknowledgeAllAlertsAsync);
        SelectSymbolCommand = new RelayCommand<SymbolQualityModel?>(SelectSymbol);
        CloseDrilldownCommand = new RelayCommand(ClearSelectedSymbol);
        SetTimeRangeCommand = new RelayCommand<string>(SetTimeRange);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    public void Start()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += async (_, _) => await RefreshDataAsync();
        _refreshTimer.Start();
        _ = RefreshDataAsync();
    }

    public void SetChartDimensions(double width, double height)
    {
        _chartWidth = width > 0 ? width : 600;
        _chartHeight = height > 0 ? height : 200;
        UpdateTrendDisplay();
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient.Dispose();
    }

    // ── Data loading ──────────────────────────────────────────────────────────────

    private async Task RefreshDataAsync()
    {
        var previousCts = _cts;
        if (previousCts != null)
        {
            previousCts.Cancel();
            previousCts.Dispose();
        }
        _cts = new CancellationTokenSource();

        try
        {
            await LoadDashboardAsync(_cts.Token);
            await LoadGapsAsync(_cts.Token);
            await LoadAnomaliesAsync(_cts.Token);
            await LoadLatencyDistributionAsync(_cts.Token);
            UpdateTrendDisplay();
            LastUpdateText = $"Last updated: {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { }
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
            if (!response.IsSuccessStatusCode) { LoadDemoDashboard(); return; }

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (data.TryGetProperty("realTimeMetrics", out var metrics))
            {
                var score = metrics.TryGetProperty("overallHealthScore", out var overall)
                    ? Math.Clamp(overall.GetDouble() * 100, 0, 100) : 0;
                _lastOverallScore = score;

                OverallScoreText = score > 0 ? $"{score:F1}" : "--";
                OverallGradeText = GetGrade(score);
                StatusText = GetStatus(score);

                var statusBrush = score switch
                {
                    >= 90 => _successBrush,
                    >= 75 => _infoBrush,
                    >= 50 => _warningBrush,
                    _ => _errorBrush
                };
                StatusBadgeBackground = statusBrush;
                OverallScoreForeground = statusBrush;
                ScoreRingStroke = statusBrush;
                ScoreRingDashArray = new DoubleCollection { score, Math.Max(0, 100 - score) };

                if (metrics.TryGetProperty("averageLatencyMs", out var avgLatency))
                    LatencyText = $"{avgLatency.GetDouble():F0}ms";

                _symbolQuality.Clear();
                if (metrics.TryGetProperty("symbolHealth", out var symbolHealth) && symbolHealth.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sym in symbolHealth.EnumerateArray())
                    {
                        var symbol = sym.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var scoreValue = sym.TryGetProperty("score", out var q) ? q.GetDouble() * 100 : 0;
                        var state = sym.TryGetProperty("state", out var st) ? ReadEnumString(st, HealthStateNames) : "Unknown";
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
                    CompletenessText = value > 0 ? $"{value:F1}%" : "--";
                }

                var gradeDistribution = completeness.TryGetProperty("gradeDistribution", out var dist) ? dist : default;
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
                    CheckProgress = Math.Min(100, (DateTimeOffset.UtcNow - timestamp).TotalMinutes / 30 * 100);
                }
            }

            if (data.TryGetProperty("gapStats", out var gaps) && gaps.TryGetProperty("totalGaps", out var totalGaps))
            {
                var gapCount = totalGaps.GetInt32();
                GapsCountText = gapCount.ToString();
                GapsCountForeground = gapCount == 0 ? _successBrush
                    : gapCount <= 5 ? _warningBrush
                    : _errorBrush;
            }

            if (data.TryGetProperty("sequenceStats", out var sequenceStats) && sequenceStats.TryGetProperty("totalErrors", out var totalErrors))
            {
                var errors = totalErrors.GetInt64();
                ErrorsCountText = errors.ToString("N0");
                ErrorsCountForeground = errors == 0 ? _successBrush : _errorBrush;
            }

            if (data.TryGetProperty("anomalyStats", out var anomalyStats))
            {
                if (anomalyStats.TryGetProperty("unacknowledgedCount", out var unack))
                {
                    var unackCount = unack.GetInt32();
                    UnacknowledgedText = unackCount.ToString();
                    IsAlertCountBadgeVisible = unackCount > 0;
                    AlertCountText = unackCount.ToString();
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

            if (data.TryGetProperty("recentAnomalies", out var recentAnomalies) && recentAnomalies.ValueKind == JsonValueKind.Array)
            {
                _allAlerts.Clear();
                foreach (var alert in recentAnomalies.EnumerateArray())
                {
                    var model = BuildAlertModel(alert);
                    if (model != null) _allAlerts.Add(model);
                }
                ApplyAlertFilter();
            }
        }
        catch (HttpRequestException) { LoadDemoDashboard(); }
    }

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
                        var gapId = gap.TryGetProperty("gapStart", out var gapStart)
                            ? gapStart.GetDateTimeOffset().ToString("O") : Guid.NewGuid().ToString();
                        var symbol = gap.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var start = gap.TryGetProperty("gapStart", out var st) ? st.GetDateTimeOffset() : DateTimeOffset.MinValue;
                        var end = gap.TryGetProperty("gapEnd", out var et) ? et.GetDateTimeOffset() : DateTimeOffset.MinValue;
                        var missingBars = gap.TryGetProperty("estimatedMissedEvents", out var mb) ? mb.GetInt64() : 0;

                        var duration = end - start;
                        var durationText = duration.TotalDays >= 1 ? $"{duration.TotalDays:F0} days"
                            : duration.TotalHours >= 1 ? $"{duration.TotalHours:F0} hours"
                            : $"{duration.TotalMinutes:F0} mins";

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
            else { LoadDemoGaps(); }

            IsNoGapsVisible = Gaps.Count == 0;
        }
        catch (HttpRequestException) { LoadDemoGaps(); }
    }

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
            ApplyAnomalyFilter();
        }
        catch (HttpRequestException)
        {
            Anomalies.Clear();
            _allAnomalies.Clear();
            IsNoAnomaliesVisible = true;
        }
    }

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
            else { LoadDemoLatency(); }
        }
        catch (HttpRequestException) { LoadDemoLatency(); }
    }

    // ── Demo data ─────────────────────────────────────────────────────────────────

    private void LoadDemoDashboard()
    {
        _lastOverallScore = 98.5;
        OverallScoreText = "98.5";
        OverallGradeText = "A+";
        StatusText = "Excellent";
        StatusBadgeBackground = _successBrush;
        ScoreRingDashArray = new DoubleCollection { 98.5, 1.5 };
        HealthyFilesText = "1,234";
        WarningFilesText = "12";
        CriticalFilesText = "0";
        UnacknowledgedText = "2";
        TotalActiveAlertsText = "5";
        IsAlertCountBadgeVisible = true;
        AlertCountText = "2";
        LastCheckTimeText = "2 minutes ago";
        NextCheckText = "In 28 minutes";
        CheckProgress = 6;
        CompletenessText = "98.5%";
        GapsCountText = "3";
        ErrorsCountText = "0";
        LatencyText = "12ms";

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
            SeverityBrush = _warningBrush
        });
        _allAlerts.Add(new AlertModel
        {
            Id = "alert-2",
            Symbol = "GOOGL",
            AlertType = "CrossedMarket",
            Message = "Bid price exceeded ask for 2 ticks",
            Severity = "Critical",
            SeverityBrush = _errorBrush
        });
        ApplyAlertFilter();
    }

    private void LoadDemoGaps()
    {
        Gaps.Clear();
        Gaps.Add(new GapModel { GapId = "gap-1", Symbol = "AAPL", Description = "Missing 156 events between 2024-01-15 09:30 and 2024-01-17 16:00", Duration = "2 days" });
        Gaps.Add(new GapModel { GapId = "gap-2", Symbol = "GOOGL", Description = "Missing 45 events between 2024-01-20 14:00 and 2024-01-20 15:30", Duration = "1.5 hours" });
        Gaps.Add(new GapModel { GapId = "gap-3", Symbol = "MSFT", Description = "Missing 12 events between 2024-01-22 10:00 and 2024-01-22 10:15", Duration = "15 mins" });
        IsNoGapsVisible = false;
    }

    private void LoadDemoLatency()
    {
        P50Text = "8ms";
        P75Text = "12ms";
        P90Text = "18ms";
        P95Text = "25ms";
        P99Text = "45ms";
    }

    // ── Actions ───────────────────────────────────────────────────────────────────

    public async Task RunQualityCheckAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

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

    public async Task RepairGapAsync(string? gapId)
    {
        if (string.IsNullOrWhiteSpace(gapId)) return;

        var gap = Gaps.FirstOrDefault(g => g.GapId == gapId);
        if (gap == null) return;

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/{gapId}/repair", null, _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                Gaps.Remove(gap);
                IsNoGapsVisible = Gaps.Count == 0;
                _notificationService.ShowNotification("Gap Repair Started", $"Repair for {gap.Symbol} gap has been initiated.", NotificationType.Success);
            }
            else
            {
                _notificationService.ShowNotification("Repair Failed", "Failed to initiate gap repair. Please try again.", NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to repair gap", ex);
            _notificationService.ShowNotification("Repair Failed", "An error occurred while initiating gap repair.", NotificationType.Error);
        }
    }

    public async Task RepairAllGapsAsync()
    {
        if (Gaps.Count == 0)
        {
            _notificationService.ShowNotification("No Gaps", "There are no gaps to repair.", NotificationType.Info);
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/repair-all", null, _cts?.Token ?? CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                var count = Gaps.Count;
                Gaps.Clear();
                IsNoGapsVisible = true;
                _notificationService.ShowNotification("Repair Started", $"Initiated repair for {count} gap(s).", NotificationType.Success);
            }
            else
            {
                _notificationService.ShowNotification("Repair Failed", "Failed to initiate gap repairs. Please try again.", NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to repair all gaps", ex);
            _notificationService.ShowNotification("Repair Failed", "An error occurred while initiating gap repairs.", NotificationType.Error);
        }
    }

    public async Task<JsonElement> GetProviderComparisonDataAsync(string symbol)
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
            _loggingService.LogError($"Failed to load provider comparison for {symbol}", ex);
        }

        return default;
    }

    private async Task AcknowledgeAlertAsync(string? alertId)
    {
        if (string.IsNullOrWhiteSpace(alertId)) return;

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/anomalies/{alertId}/acknowledge", null);

            if (response.IsSuccessStatusCode)
            {
                _allAlerts.RemoveAll(a => a.Id == alertId);
                ApplyAlertFilter();
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

    private async Task AcknowledgeAllAlertsAsync()
    {
        foreach (var alert in _allAlerts.ToList())
        {
            try
            {
                await _httpClient.PostAsync($"{_baseUrl}/api/quality/anomalies/{alert.Id}/acknowledge", null);
            }
            catch (Exception ex) { _loggingService.LogError("Failed to acknowledge alert", ex); }
        }

        _allAlerts.Clear();
        ApplyAlertFilter();
        await RefreshDataAsync();

        _notificationService.ShowNotification("All Alerts Acknowledged", "All alerts have been acknowledged.", NotificationType.Success);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────────

    private void ApplySymbolFilter()
    {
        var query = _symbolFilter.Trim().ToUpperInvariant();
        FilteredSymbols.Clear();
        foreach (var symbol in _symbolQuality.Where(s => string.IsNullOrEmpty(query) || s.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase)))
            FilteredSymbols.Add(symbol);
        IsNoSymbolsVisible = FilteredSymbols.Count == 0;
    }

    private void ApplyAlertFilter()
    {
        Alerts.Clear();
        foreach (var alert in _allAlerts.Where(a => _alertSeverityFilter == "All" || a.Severity.Equals(_alertSeverityFilter, StringComparison.OrdinalIgnoreCase)))
            Alerts.Add(alert);
        IsNoAlertsVisible = Alerts.Count == 0;
    }

    private void ApplyAnomalyFilter()
    {
        Anomalies.Clear();
        foreach (var anomaly in _allAnomalies.Where(a => _anomalyTypeFilter == "All" || a.Type.Equals(_anomalyTypeFilter, StringComparison.OrdinalIgnoreCase)))
            Anomalies.Add(anomaly);
        IsNoAnomaliesVisible = Anomalies.Count == 0;
        IsAnomalyCountBadgeVisible = Anomalies.Count > 0;
        AnomalyCountText = Anomalies.Count.ToString();
    }

    // ── Trend display ─────────────────────────────────────────────────────────────

    public void SetTimeRange(string? window)
    {
        if (string.IsNullOrEmpty(window)) return;
        _timeRange = window;
        UpdateTrendDisplay();
        _ = RefreshDataAsync();
    }

    private void UpdateTrendDisplay()
    {
        var points = BuildTrendPoints(_lastOverallScore, _timeRange);
        if (points.Count == 0)
        {
            AvgScoreText = "--"; MinScoreText = "--"; MaxScoreText = "--"; StdDevText = "--";
            return;
        }

        var scores = points.Select(p => p.Score).ToList();
        var avg = scores.Average();
        var min = scores.Min();
        var max = scores.Max();
        var stdDev = Math.Sqrt(scores.Sum(s => Math.Pow(s - avg, 2)) / scores.Count);

        AvgScoreText = $"{avg:F1}%";
        MinScoreText = $"{min:F1}%";
        MaxScoreText = $"{max:F1}%";
        StdDevText = $"{stdDev:F1}%";

        var change = scores.Last() - scores.First();
        var isPositive = change >= 0;
        TrendIconGlyph = isPositive ? "\uE70E" : "\uE70D";
        TrendText = $"{(isPositive ? "+" : "")}{change:F1}% this {GetTimeWindowLabel(_timeRange)}";
        TrendBrush = change > 0.5 ? _successBrush : change < -0.5 ? _errorBrush : _warningBrush;

        RenderTrendChartData(points);
    }

    private void RenderTrendChartData(IReadOnlyList<TrendPoint> points)
    {
        if (points.Count == 0)
        {
            TrendChartLinePoints = new PointCollection();
            TrendChartFillPoints = new PointCollection();
            TrendXAxisLabels = Array.Empty<string>();
            return;
        }

        var width = _chartWidth;
        var height = _chartHeight;
        var maxScore = Math.Max(100, points.Max(p => p.Score));
        var minScore = Math.Min(0, points.Min(p => p.Score));

        var linePoints = new PointCollection();
        var fillPoints = new PointCollection();

        for (var i = 0; i < points.Count; i++)
        {
            var x = i * (width / Math.Max(1, points.Count - 1));
            var normalized = (points[i].Score - minScore) / Math.Max(1, maxScore - minScore);
            var y = height - (normalized * height);
            linePoints.Add(new Point(x, y));
            fillPoints.Add(new Point(x, y));
        }

        fillPoints.Add(new Point(width, height));
        fillPoints.Add(new Point(0, height));

        TrendChartLinePoints = linePoints;
        TrendChartFillPoints = fillPoints;
        TrendXAxisLabels = points.Select(p => p.Label).ToList();
    }

    // ── Symbol drilldown ──────────────────────────────────────────────────────────

    private void SelectSymbol(SymbolQualityModel? model)
    {
        if (model == null) { ClearSelectedSymbol(); return; }

        IsDrilldownVisible = true;
        DrilldownSymbolHeader = $"{model.Symbol} — Quality Drilldown";
        DrilldownScoreText = model.ScoreFormatted;
        DrilldownScoreForeground = model.Score >= 90 ? _successBrush
            : model.Score >= 70 ? _warningBrush
            : _errorBrush;

        var random = new Random(model.Symbol.GetHashCode());
        DrilldownCompletenessText = $"{random.Next(85, 100)}%";
        DrilldownGapsText = random.Next(0, 5).ToString();
        DrilldownErrorsText = random.Next(0, 3).ToString();
        DrilldownLatencyText = $"{random.Next(5, 120)}ms";

        var cells = new List<HeatmapCellModel>(7);
        for (var i = 0; i < 7; i++)
        {
            var day = DateTime.Today.AddDays(-6 + i);
            var dayScore = random.Next(60, 100);
            var background = dayScore >= 95
                ? new SolidColorBrush(Color.FromArgb(200, 63, 185, 80))
                : dayScore >= 85 ? new SolidColorBrush(Color.FromArgb(200, 78, 201, 176))
                : dayScore >= 70 ? new SolidColorBrush(Color.FromArgb(200, 227, 179, 65))
                : new SolidColorBrush(Color.FromArgb(200, 244, 67, 54));
            cells.Add(new HeatmapCellModel { DayLabel = day.ToString("ddd"), Background = background, Tooltip = $"{day:MMM dd}: Score {dayScore}%" });
        }
        HeatmapCells = cells;

        DrilldownIssues.Clear();
        var issueTypes = new[] { "Sequence gap detected", "Stale data (>5s delay)", "Price spike anomaly", "Missing quotes window", "Volume irregularity" };
        var issueCount = random.Next(0, 4);
        for (var i = 0; i < issueCount; i++)
        {
            var severity = random.Next(0, 3);
            DrilldownIssues.Add(new DrilldownIssue
            {
                Description = issueTypes[random.Next(issueTypes.Length)],
                Timestamp = DateTime.Now.AddMinutes(-random.Next(10, 2880)).ToString("MMM dd HH:mm"),
                SeverityBrush = severity == 0 ? new SolidColorBrush(Color.FromRgb(244, 67, 54))
                    : severity == 1 ? new SolidColorBrush(Color.FromRgb(227, 179, 65))
                    : new SolidColorBrush(Color.FromRgb(33, 150, 243))
            });
        }
        IsNoDrilldownIssuesVisible = DrilldownIssues.Count == 0;
    }

    private void ClearSelectedSymbol()
    {
        IsDrilldownVisible = false;
    }

    // ── Static helpers ────────────────────────────────────────────────────────────

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

    private AlertModel? BuildAlertModel(JsonElement alert)
    {
        if (alert.ValueKind != JsonValueKind.Object) return null;
        var id = alert.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
        var symbol = alert.TryGetProperty("symbol", out var symbolValue) ? symbolValue.GetString() ?? "" : "";
        var type = alert.TryGetProperty("type", out var typeValue) ? typeValue.GetString() ?? "" : "";
        var description = alert.TryGetProperty("description", out var descValue) ? descValue.GetString() ?? "" : "";
        var severity = alert.TryGetProperty("severity", out var severityValue) ? ReadEnumString(severityValue, AnomalySeverityNames) : "Warning";

        return new AlertModel
        {
            Id = id,
            Symbol = symbol,
            AlertType = type,
            Message = description,
            Severity = severity,
            SeverityBrush = severity.ToLowerInvariant() switch
            {
                "critical" or "error" => _errorBrush,
                "warning" => _warningBrush,
                _ => _infoBrush
            }
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
            SeverityColor = severity.ToLowerInvariant() switch
            {
                "critical" or "error" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                "warning" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                _ => new SolidColorBrush(Color.FromRgb(139, 148, 158))
            }
        };
    }

    public static string FormatRelativeTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        return span.TotalSeconds < 60 ? "Just now"
             : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} minutes ago"
             : span.TotalHours < 24 ? $"{(int)span.TotalHours} hours ago"
             : $"{(int)span.TotalDays} days ago";
    }

    public static string GetGrade(double score) => score switch
    {
        >= 95 => "A+", >= 90 => "A", >= 85 => "A-", >= 80 => "B+",
        >= 75 => "B", >= 70 => "B-", >= 65 => "C+", >= 60 => "C",
        >= 55 => "C-", >= 50 => "D", _ => "F"
    };

    private static string GetStatus(double score) => score switch
    {
        >= 90 => "Excellent", >= 75 => "Healthy", >= 50 => "Warning", _ => "Critical"
    };

    private static int GetGradeCount(JsonElement gradeDistribution, string grade)
    {
        if (gradeDistribution.ValueKind != JsonValueKind.Object) return 0;
        return gradeDistribution.TryGetProperty(grade, out var value) ? value.GetInt32() : 0;
    }

    private static int GetAnomalyCount(JsonElement anomalyStats, string type)
    {
        if (anomalyStats.ValueKind != JsonValueKind.Object) return 0;
        return anomalyStats.TryGetProperty(type, out var value) ? value.GetInt32() : 0;
    }

    private static string ReadEnumString(JsonElement element, IReadOnlyList<string> names) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var value) && value >= 0 && value < names.Count => names[value],
            _ => element.ToString()
        };

    private static int GetRangeCount(string range, int oneDay, int sevenDay, int thirtyDay, int ninetyDay) =>
        range switch { "1d" => oneDay, "7d" => sevenDay, "30d" => thirtyDay, "90d" => ninetyDay, _ => sevenDay };

    private static string GetTimeWindowLabel(string window) =>
        window switch { "1d" => "day", "7d" => "week", "30d" => "month", "90d" => "quarter", _ => "period" };

    private static List<TrendPoint> BuildTrendPoints(double baseScore, string window)
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

    // ── Enum name tables ──────────────────────────────────────────────────────────
    private static readonly string[] HealthStateNames = { "Healthy", "Degraded", "Unhealthy", "Stale", "Unknown" };
    private static readonly string[] AnomalySeverityNames = { "Info", "Warning", "Error", "Critical" };
    private static readonly string[] AnomalyTypeNames =
    {
        "PriceSpike", "PriceDrop", "VolumeSpike", "VolumeDrop", "SpreadWide", "StaleData",
        "RapidPriceChange", "AbnormalVolatility", "MissingData", "DuplicateData",
        "CrossedMarket", "InvalidPrice", "InvalidVolume"
    };
}

// ── Model types ───────────────────────────────────────────────────────────────

public sealed class TrendPoint
{
    public TrendPoint(double score, string label) { Score = score; Label = label; }
    public double Score { get; }
    public string Label { get; }
}

public sealed class SymbolQualityModel
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

public sealed class GapModel
{
    public string GapId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

public sealed class AlertModel
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Brush SeverityBrush { get; set; } = Brushes.Gray;
}

public sealed class AnomalyModel
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public SolidColorBrush SeverityColor { get; set; } = new(Colors.Gray);
}

public sealed class HeatmapCellModel
{
    public string DayLabel { get; set; } = string.Empty;
    public Brush Background { get; set; } = Brushes.Transparent;
    public string Tooltip { get; set; } = string.Empty;
}

public class DrilldownIssue
{
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public SolidColorBrush SeverityBrush { get; set; } = new(Colors.Gray);
}
