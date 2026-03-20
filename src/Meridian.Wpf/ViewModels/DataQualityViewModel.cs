using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Meridian.Ui.Services.DataQuality;
using Meridian.Ui.Services;
using Meridian.Ui.Services.Contracts;
using Meridian.Ui.Services.Services;
using Meridian.Wpf.Models;
using WpfServices = Meridian.Wpf.Services;

namespace Meridian.Wpf.ViewModels;

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
/// Consumes shared data-quality services for refresh orchestration, API access, and presentation mapping.
/// </summary>
public sealed class DataQualityViewModel : BindableBase, IDisposable
{
    private readonly IDataQualityApiClient _apiClient;
    private readonly IDataQualityPresentationService _presentationService;
    private readonly IDataQualityRefreshService _refreshService;
    private readonly WpfServices.LoggingService _loggingService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly HttpClient _httpClient = new();
    private readonly DataQualityRefreshCoordinator _refreshCoordinator;

    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _timeRange = "7d";
    private double _lastOverallScore = 98.5;
    private string _symbolFilter = string.Empty;
    private string _severityFilter = "All";
    private string _anomalyTypeFilter = "All";

    private readonly List<AlertModel> _allAlerts = new();
    private readonly List<AnomalyModel> _allAnomalies = new();

    public ObservableCollection<SymbolQualityModel> SymbolQuality { get; } = new();
    public ObservableCollection<SymbolQualityModel> FilteredSymbols { get; } = new();
    public ObservableCollection<GapModel> Gaps { get; } = new();
    public ObservableCollection<AlertModel> Alerts { get; } = new();
    public ObservableCollection<AnomalyModel> Anomalies { get; } = new();

    private string _lastUpdateText = "Last updated: --";
    public string LastUpdateText { get => _lastUpdateText; private set => SetProperty(ref _lastUpdateText, value); }

    private string _overallScoreText = "--";
    public string OverallScoreText { get => _overallScoreText; private set => SetProperty(ref _overallScoreText, value); }

    private string _overallGradeText = "--";
    public string OverallGradeText { get => _overallGradeText; private set => SetProperty(ref _overallGradeText, value); }

    private string _statusText = "--";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private Brush _scoreBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    public Brush ScoreBrush { get => _scoreBrush; private set => SetProperty(ref _scoreBrush, value); }

    private DoubleCollection _scoreSegments = new() { 100, 100 };
    public DoubleCollection ScoreSegments { get => _scoreSegments; private set => SetProperty(ref _scoreSegments, value); }

    private string _latencyText = "--";
    public string LatencyText { get => _latencyText; private set => SetProperty(ref _latencyText, value); }

    private string _completenessText = "--";
    public string CompletenessText { get => _completenessText; private set => SetProperty(ref _completenessText, value); }

    private string _healthyFilesText = "--";
    public string HealthyFilesText { get => _healthyFilesText; private set => SetProperty(ref _healthyFilesText, value); }

    private string _warningFilesText = "--";
    public string WarningFilesText { get => _warningFilesText; private set => SetProperty(ref _warningFilesText, value); }

    private string _criticalFilesText = "--";
    public string CriticalFilesText { get => _criticalFilesText; private set => SetProperty(ref _criticalFilesText, value); }

    private string _gapsCountText = "--";
    public string GapsCountText { get => _gapsCountText; private set => SetProperty(ref _gapsCountText, value); }

    private Brush _gapsCountBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    public Brush GapsCountBrush { get => _gapsCountBrush; private set => SetProperty(ref _gapsCountBrush, value); }

    private string _errorsCountText = "--";
    public string ErrorsCountText { get => _errorsCountText; private set => SetProperty(ref _errorsCountText, value); }

    private Brush _errorsCountBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    public Brush ErrorsCountBrush { get => _errorsCountBrush; private set => SetProperty(ref _errorsCountBrush, value); }

    private string _unacknowledgedText = "--";
    public string UnacknowledgedText { get => _unacknowledgedText; private set => SetProperty(ref _unacknowledgedText, value); }

    private string _totalActiveAlertsText = "--";
    public string TotalActiveAlertsText { get => _totalActiveAlertsText; private set => SetProperty(ref _totalActiveAlertsText, value); }

    private string _alertCountBadgeText = "0";
    public string AlertCountBadgeText { get => _alertCountBadgeText; private set => SetProperty(ref _alertCountBadgeText, value); }

    private bool _isAlertCountBadgeVisible;
    public bool IsAlertCountBadgeVisible { get => _isAlertCountBadgeVisible; private set => SetProperty(ref _isAlertCountBadgeVisible, value); }

    private string _crossedMarketCount = "--";
    public string CrossedMarketCount { get => _crossedMarketCount; private set => SetProperty(ref _crossedMarketCount, value); }

    private string _staleDataCount = "--";
    public string StaleDataCount { get => _staleDataCount; private set => SetProperty(ref _staleDataCount, value); }

    private string _invalidPriceCount = "--";
    public string InvalidPriceCount { get => _invalidPriceCount; private set => SetProperty(ref _invalidPriceCount, value); }

    private string _invalidVolumeCount = "--";
    public string InvalidVolumeCount { get => _invalidVolumeCount; private set => SetProperty(ref _invalidVolumeCount, value); }

    private string _missingDataCount = "--";
    public string MissingDataCount { get => _missingDataCount; private set => SetProperty(ref _missingDataCount, value); }

    private string _lastCheckTimeText = "--";
    public string LastCheckTimeText { get => _lastCheckTimeText; private set => SetProperty(ref _lastCheckTimeText, value); }

    private string _nextCheckText = "--";
    public string NextCheckText { get => _nextCheckText; private set => SetProperty(ref _nextCheckText, value); }

    private double _checkProgressValue;
    public double CheckProgressValue { get => _checkProgressValue; private set => SetProperty(ref _checkProgressValue, value); }

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

    private bool _hasNoGaps = true;
    public bool HasNoGaps { get => _hasNoGaps; private set => SetProperty(ref _hasNoGaps, value); }

    private bool _hasNoAlerts = true;
    public bool HasNoAlerts { get => _hasNoAlerts; private set => SetProperty(ref _hasNoAlerts, value); }

    private bool _hasNoAnomalies = true;
    public bool HasNoAnomalies { get => _hasNoAnomalies; private set => SetProperty(ref _hasNoAnomalies, value); }

    private bool _hasNoSymbols = true;
    public bool HasNoSymbols { get => _hasNoSymbols; private set => SetProperty(ref _hasNoSymbols, value); }

    private bool _isAnomalyCountBadgeVisible;
    public bool IsAnomalyCountBadgeVisible { get => _isAnomalyCountBadgeVisible; private set => SetProperty(ref _isAnomalyCountBadgeVisible, value); }

    private string _anomalyCountText = "0";
    public string AnomalyCountText { get => _anomalyCountText; private set => SetProperty(ref _anomalyCountText, value); }

    public DataQualityViewModel(
        WpfServices.StatusService statusService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService,
        IRefreshScheduler? refreshScheduler = null)
    {
        _statusService = statusService;
        _loggingService = loggingService;
        _notificationService = notificationService;
        _baseUrl = _statusService.BaseUrl;
        _refreshCoordinator = new DataQualityRefreshCoordinator(
            refreshScheduler ?? new PeriodicRefreshScheduler(),
            RefreshDataAsync,
            ex => _loggingService.LogError("Failed to refresh data quality", ex));
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────
    public async Task StartAsync(CancellationToken ct = default)
    {
        await _refreshCoordinator.StartAsync(TimeSpan.FromSeconds(30), ct);
    }

    public void Stop()
    {
        _refreshCoordinator.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _httpClient.Dispose();
    }

    private string _trendText = "--";
    public string TrendText { get => _trendText; private set => SetProperty(ref _trendText, value); }

    private async Task RefreshDataAsync(CancellationToken ct = default)
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

    private Brush _trendBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    public Brush TrendBrush { get => _trendBrush; private set => SetProperty(ref _trendBrush, value); }

    private bool _isDrilldownVisible;
    public bool IsDrilldownVisible { get => _isDrilldownVisible; private set => SetProperty(ref _isDrilldownVisible, value); }

    private string _drilldownSymbolHeader = string.Empty;
    public string DrilldownSymbolHeader { get => _drilldownSymbolHeader; private set => SetProperty(ref _drilldownSymbolHeader, value); }

    private string _drilldownScoreText = "--";
    public string DrilldownScoreText { get => _drilldownScoreText; private set => SetProperty(ref _drilldownScoreText, value); }

    private Brush _drilldownScoreBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    public Brush DrilldownScoreBrush { get => _drilldownScoreBrush; private set => SetProperty(ref _drilldownScoreBrush, value); }

    private string _drilldownCompletenessText = "--";
    public string DrilldownCompletenessText { get => _drilldownCompletenessText; private set => SetProperty(ref _drilldownCompletenessText, value); }

    private string _drilldownGapsText = "--";
    public string DrilldownGapsText { get => _drilldownGapsText; private set => SetProperty(ref _drilldownGapsText, value); }

    private string _drilldownErrorsText = "--";
    public string DrilldownErrorsText { get => _drilldownErrorsText; private set => SetProperty(ref _drilldownErrorsText, value); }

    private string _drilldownLatencyText = "--";
    public string DrilldownLatencyText { get => _drilldownLatencyText; private set => SetProperty(ref _drilldownLatencyText, value); }

    private bool _hasNoDrilldownIssues = true;
    public bool HasNoDrilldownIssues { get => _hasNoDrilldownIssues; private set => SetProperty(ref _hasNoDrilldownIssues, value); }

    public ObservableCollection<DrilldownIssue> DrilldownIssues { get; } = new();
    public IReadOnlyList<DataQualityHeatmapCellPresentation> DrilldownHeatmapCells { get; private set; } = Array.Empty<DataQualityHeatmapCellPresentation>();
    public IReadOnlyList<TrendPoint> TrendPoints { get; private set; } = Array.Empty<TrendPoint>();

    public event EventHandler? TrendChartChanged;
    public event EventHandler? DrilldownChanged;

    public DataQualityViewModel(
        IDataQualityApiClient apiClient,
        IDataQualityPresentationService presentationService,
        IDataQualityRefreshService refreshService,
        WpfServices.LoggingService loggingService,
        WpfServices.NotificationService notificationService)
    {
        _apiClient = apiClient;
        _presentationService = presentationService;
        _refreshService = refreshService;
        _loggingService = loggingService;
        _notificationService = notificationService;
        UpdateTrendState();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await RefreshDataAsync(ct);
        _refreshService.Start(TimeSpan.FromSeconds(30), RefreshDataAsync);
    }

    public void Stop()
    {
        _refreshService.Stop();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await RefreshDataAsync(ct);
        _notificationService.ShowNotification(
            "Refreshed",
            "Data quality metrics have been refreshed.",
            NotificationType.Info);
    }

    public void SetTimeRange(string timeRange)
    {
        _timeRange = timeRange;
        UpdateTrendState();
        _ = RefreshDataAsync();
    }

    public void ApplySymbolFilter(string query)
    {
        _symbolFilter = query ?? string.Empty;
        FilteredSymbols.Clear();
        foreach (var symbol in SymbolQuality.Where(s =>
                     string.IsNullOrWhiteSpace(_symbolFilter)
                     || s.Symbol.Contains(_symbolFilter, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredSymbols.Add(symbol);
        }

        HasNoSymbols = FilteredSymbols.Count == 0;
    }

    public void ApplyAlertFilter(string severity)
    {
        _severityFilter = string.IsNullOrWhiteSpace(severity) ? "All" : severity;
        Alerts.Clear();
        foreach (var alert in _allAlerts.Where(a =>
                     _severityFilter == "All" || a.Severity.Equals(_severityFilter, StringComparison.OrdinalIgnoreCase)))
        {
            Alerts.Add(alert);
        }

        HasNoAlerts = Alerts.Count == 0;
    }

    public void ApplyAnomalyFilter(string type)
    {
        _anomalyTypeFilter = string.IsNullOrWhiteSpace(type) ? "All" : type;
        Anomalies.Clear();
        foreach (var anomaly in _allAnomalies.Where(a =>
                     _anomalyTypeFilter == "All" || a.Type.Equals(_anomalyTypeFilter, StringComparison.OrdinalIgnoreCase)))
        {
            Anomalies.Add(anomaly);
        }

        HasNoAnomalies = Anomalies.Count == 0;
        IsAnomalyCountBadgeVisible = Anomalies.Count > 0;
        AnomalyCountText = Anomalies.Count.ToString();
    }

    public void ShowSymbolDrilldown(SymbolQualityModel model)
    {
        var presentation = _presentationService.BuildSymbolDrilldown(new DataQualitySymbolPresentation
        {
            Symbol = model.Symbol,
            Score = model.Score,
            ScoreFormatted = model.ScoreFormatted,
            Grade = model.Grade,
            Status = model.Status,
            Issues = model.Issues,
            LastUpdate = model.LastUpdate,
            LastUpdateFormatted = model.LastUpdateFormatted
        });

        DrilldownSymbolHeader = presentation.HeaderText;
        DrilldownScoreText = presentation.ScoreText;
        DrilldownScoreBrush = ToneToBrush(presentation.ScoreTone);
        DrilldownCompletenessText = presentation.CompletenessText;
        DrilldownGapsText = presentation.GapsText;
        DrilldownErrorsText = presentation.ErrorsText;
        DrilldownLatencyText = presentation.LatencyText;
        DrilldownHeatmapCells = presentation.HeatmapCells;

        DrilldownIssues.Clear();
        foreach (var issue in presentation.Issues)
        {
            DrilldownIssues.Add(new DrilldownIssue
            {
                Description = issue.Description,
                Timestamp = issue.Timestamp,
                SeverityBrush = new SolidColorBrush(((SolidColorBrush)ToneToBrush(issue.Tone)).Color)
            });
        }

        HasNoDrilldownIssues = DrilldownIssues.Count == 0;
        IsDrilldownVisible = true;
        DrilldownChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideSymbolDrilldown()
    {
        IsDrilldownVisible = false;
        DrilldownChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AcknowledgeAlertAsync(string alertId, CancellationToken ct = default)
    {
        try
        {
            if (await _apiClient.AcknowledgeAnomalyAsync(alertId, ct))
            {
                _allAlerts.RemoveAll(a => a.Id == alertId);
                ApplyAlertFilter(_severityFilter);
                await RefreshDataAsync(ct);
                return;
            }

            _notificationService.ShowNotification("Acknowledge Failed", "Failed to acknowledge alert.", NotificationType.Warning);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to acknowledge alert", ex);
            _notificationService.ShowNotification("Acknowledge Failed", "An error occurred while acknowledging the alert.", NotificationType.Error);
        }
    }

    public async Task AcknowledgeAllAlertsAsync(CancellationToken ct = default)
    {
        foreach (var alert in _allAlerts.ToList())
        {
            try
            {
                await _apiClient.AcknowledgeAnomalyAsync(alert.Id, ct);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to acknowledge alert", ex);
            }
        }

        _allAlerts.Clear();
        ApplyAlertFilter(_severityFilter);
        await RefreshDataAsync(ct);

        _notificationService.ShowNotification(
            "All Alerts Acknowledged",
            "All alerts have been acknowledged.",
            NotificationType.Success);
    }

    public async Task<bool> RepairGapAsync(string gapId, CancellationToken ct = default)
    {
        try
        {
            if (await _apiClient.RepairGapAsync(gapId, ct))
            {
                var gap = Gaps.FirstOrDefault(g => g.GapId == gapId);
                if (gap != null)
                {
                    Gaps.Remove(gap);
                }

                HasNoGaps = Gaps.Count == 0;
                _notificationService.ShowNotification("Gap Repair Started", "Repair has been initiated.", NotificationType.Success);
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

    public async Task<bool> RepairAllGapsAsync(CancellationToken ct = default)
    {
        try
        {
            if (await _apiClient.RepairAllGapsAsync(ct))
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

    public async Task RunQualityCheckAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var result = await _apiClient.RunQualityCheckAsync(path, ct);
            if (result?.Success == true)
            {
                _notificationService.ShowNotification("Quality Check Complete", "Quality check completed successfully.", NotificationType.Success);
                await RefreshDataAsync(ct);
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

    public Task<DataQualityProviderComparisonPresentation> GetProviderComparisonAsync(string symbol, CancellationToken ct = default)
        => _presentationService.GetProviderComparisonAsync(symbol, ct);

    public TrendStatistics ComputeTrendStatistics()
    {
        var points = TrendPoints;
        if (points.Count == 0)
        {
            return new TrendStatistics
            {
                HasData = false,
                AvgText = "--",
                MinText = "--",
                MaxText = "--",
                StdDevText = "--",
                TrendText = "--"
            };
        }

        var scores = points.Select(point => point.Score).ToList();
        var avg = scores.Average();
        var min = scores.Min();
        var max = scores.Max();
        var stdDev = Math.Sqrt(scores.Sum(score => Math.Pow(score - avg, 2)) / scores.Count);
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
            TrendText = $"{(isPositive ? "+" : string.Empty)}{change:F1}% this {label}",
            IsTrendPositive = isPositive,
            ScoreChange = change
        };
    }

    private async Task RefreshDataAsync(CancellationToken ct = default)
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            var snapshot = await _presentationService.GetSnapshotAsync(_timeRange, _refreshCts.Token);
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
            // Expected during refresh rollover/shutdown.
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to refresh data quality", ex);
        }
    }

    private void ApplySnapshot(DataQualityPresentationSnapshot snapshot)
    {
        _lastOverallScore = snapshot.OverallScore;
        LastUpdateText = snapshot.LastUpdateText;
        OverallScoreText = snapshot.OverallScoreText;
        OverallGradeText = snapshot.OverallGradeText;
        StatusText = snapshot.StatusText;
        ScoreBrush = ToneToBrush(snapshot.ScoreTone);
        ScoreSegments = new DoubleCollection { snapshot.OverallScore, Math.Max(0, 100 - snapshot.OverallScore) };
        LatencyText = snapshot.LatencyText;
        CompletenessText = snapshot.CompletenessText;
        HealthyFilesText = snapshot.HealthyFilesText;
        WarningFilesText = snapshot.WarningFilesText;
        CriticalFilesText = snapshot.CriticalFilesText;
        GapsCountText = snapshot.GapsCountText;
        GapsCountBrush = ToneToBrush(snapshot.GapsTone);
        ErrorsCountText = snapshot.ErrorsCountText;
        ErrorsCountBrush = ToneToBrush(snapshot.ErrorsTone);
        UnacknowledgedText = snapshot.UnacknowledgedText;
        TotalActiveAlertsText = snapshot.TotalActiveAlertsText;
        AlertCountBadgeText = snapshot.AlertCountBadgeText;
        IsAlertCountBadgeVisible = snapshot.IsAlertCountBadgeVisible;
        CrossedMarketCount = snapshot.CrossedMarketCount;
        StaleDataCount = snapshot.StaleDataCount;
        InvalidPriceCount = snapshot.InvalidPriceCount;
        InvalidVolumeCount = snapshot.InvalidVolumeCount;
        MissingDataCount = snapshot.MissingDataCount;
        LastCheckTimeText = snapshot.LastCheckTimeText;
        NextCheckText = snapshot.NextCheckText;
        CheckProgressValue = snapshot.CheckProgressValue;
        P50Text = snapshot.P50Text;
        P75Text = snapshot.P75Text;
        P90Text = snapshot.P90Text;
        P95Text = snapshot.P95Text;
        P99Text = snapshot.P99Text;

        ReplaceCollection(SymbolQuality, snapshot.Symbols.Select(symbol => new SymbolQualityModel
        {
            Symbol = symbol.Symbol,
            Score = symbol.Score,
            ScoreFormatted = symbol.ScoreFormatted,
            Grade = symbol.Grade,
            Status = symbol.Status,
            Issues = symbol.Issues,
            LastUpdate = symbol.LastUpdate,
            LastUpdateFormatted = symbol.LastUpdateFormatted
        }));

        ReplaceCollection(Gaps, snapshot.Gaps.Select(gap => new GapModel
        {
            GapId = gap.GapId,
            Symbol = gap.Symbol,
            Description = gap.Description,
            Duration = gap.Duration
        }));
        HasNoGaps = Gaps.Count == 0;

        _allAlerts.Clear();
        _allAlerts.AddRange(snapshot.Alerts.Select(alert => new AlertModel
        {
            Id = alert.Id,
            Symbol = alert.Symbol,
            AlertType = alert.AlertType,
            Message = alert.Message,
            Severity = alert.Severity,
            SeverityBrush = ToneToBrush(alert.SeverityTone)
        }));

        _allAnomalies.Clear();
        _allAnomalies.AddRange(snapshot.Anomalies.Select(anomaly => new AnomalyModel
        {
            Symbol = anomaly.Symbol,
            Description = anomaly.Description,
            Timestamp = anomaly.Timestamp,
            Type = anomaly.Type,
            SeverityColor = new SolidColorBrush(((SolidColorBrush)ToneToBrush(anomaly.SeverityTone)).Color)
        }));

        ApplySymbolFilter(_symbolFilter);
        ApplyAlertFilter(_severityFilter);
        ApplyAnomalyFilter(_anomalyTypeFilter);
        UpdateTrendState();
    }

    private void UpdateTrendState()
    {
        TrendPoints = BuildTrendPoints(_lastOverallScore, _timeRange);
        var trend = ComputeTrendStatistics();

        AvgScoreText = trend.AvgText;
        MinScoreText = trend.MinText;
        MaxScoreText = trend.MaxText;
        StdDevText = trend.StdDevText;
        TrendText = trend.TrendText;
        TrendIconGlyph = trend.IsTrendPositive ? "\uE70E" : "\uE70D";
        TrendBrush = trend.ScoreChange switch
        {
            > 0.5 => ToneToBrush(DataQualityVisualTones.Success),
            < -0.5 => ToneToBrush(DataQualityVisualTones.Error),
            _ => ToneToBrush(DataQualityVisualTones.Warning)
        };

        TrendChartChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Brush ToneToBrush(string tone)
    {
        return tone switch
        {
            DataQualityVisualTones.Success => new SolidColorBrush(Color.FromRgb(63, 185, 80)),
            DataQualityVisualTones.Info => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            DataQualityVisualTones.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            DataQualityVisualTones.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            _ => new SolidColorBrush(Color.FromRgb(139, 148, 158))
        };
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
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

    public void Dispose()
    {
        Stop();
        _refreshService.Dispose();
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

    public void Dispose()
    {
        Stop();
        _refreshCoordinator.Dispose();
    }
}
