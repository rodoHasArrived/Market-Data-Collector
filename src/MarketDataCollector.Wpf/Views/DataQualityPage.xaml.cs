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
    private readonly ObservableCollection<GapModel> _gaps = new();
    private readonly ObservableCollection<AnomalyModel> _anomalies = new();
    private Timer? _refreshTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _timeRange = "1h";

    public DataQualityPage()
    {
        InitializeComponent();

        SymbolQualityControl.ItemsSource = _symbolQuality;
        GapsControl.ItemsSource = _gaps;
        AnomaliesControl.ItemsSource = _anomalies;

        // Get base URL from StatusService
        _baseUrl = StatusService.Instance.BaseUrl;

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

        // Start auto-refresh timer (every 30 seconds)
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
            // Load quality metrics
            await LoadQualityMetricsAsync(_cts.Token);

            // Load gaps
            await LoadGapsAsync(_cts.Token);

            // Load anomalies
            await LoadAnomaliesAsync(_cts.Token);

            // Load latency distribution
            await LoadLatencyDistributionAsync(_cts.Token);

            // Update last refresh time
            LastUpdateText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to refresh data quality", ex);
        }
    }

    private async Task LoadQualityMetricsAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/metrics?range={_timeRange}", ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                // Overall metrics
                if (data.TryGetProperty("completeness", out var comp))
                {
                    var completeness = comp.GetDouble() * 100;
                    CompletenessText.Text = $"{completeness:F1}%";
                    CompletenessText.Foreground = new SolidColorBrush(
                        completeness >= 99 ? Color.FromRgb(63, 185, 80) :
                        completeness >= 95 ? Color.FromRgb(255, 193, 7) :
                        Color.FromRgb(244, 67, 54));
                }

                if (data.TryGetProperty("gapCount", out var gapCount))
                {
                    var gaps = gapCount.GetInt32();
                    GapsCountText.Text = gaps.ToString();
                    GapsCountText.Foreground = new SolidColorBrush(
                        gaps == 0 ? Color.FromRgb(63, 185, 80) :
                        gaps <= 5 ? Color.FromRgb(255, 193, 7) :
                        Color.FromRgb(244, 67, 54));
                }

                if (data.TryGetProperty("errorCount", out var errorCount))
                {
                    var errors = errorCount.GetInt32();
                    ErrorsCountText.Text = errors.ToString();
                    ErrorsCountText.Foreground = new SolidColorBrush(
                        errors == 0 ? Color.FromRgb(63, 185, 80) :
                        Color.FromRgb(244, 67, 54));
                }

                if (data.TryGetProperty("avgLatencyMs", out var latency))
                {
                    LatencyText.Text = $"{latency.GetDouble():F0}ms";
                }

                // Symbol-level metrics
                _symbolQuality.Clear();
                if (data.TryGetProperty("symbols", out var symbols) && symbols.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sym in symbols.EnumerateArray())
                    {
                        var symbol = sym.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var quality = sym.TryGetProperty("completeness", out var q) ? q.GetDouble() * 100 : 0;
                        var gaps = sym.TryGetProperty("gaps", out var g) ? g.GetInt32() : 0;
                        var lat = sym.TryGetProperty("avgLatencyMs", out var l) ? l.GetDouble() : 0;

                        _symbolQuality.Add(new SymbolQualityModel
                        {
                            Symbol = symbol,
                            QualityPercent = $"{quality:F1}%",
                            QualityWidth = quality * 2, // Scale to 200px max width
                            QualityColor = new SolidColorBrush(
                                quality >= 99 ? Color.FromRgb(63, 185, 80) :
                                quality >= 95 ? Color.FromRgb(255, 193, 7) :
                                Color.FromRgb(244, 67, 54)),
                            GapsText = gaps == 0 ? "No gaps" : $"{gaps} gap{(gaps > 1 ? "s" : "")}",
                            LatencyText = $"{lat:F0}ms"
                        });
                    }
                }

                NoSymbolsText.Visibility = _symbolQuality.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Load demo data if API not available
                LoadDemoMetrics();
            }
        }
        catch (HttpRequestException)
        {
            LoadDemoMetrics();
        }
    }

    private void LoadDemoMetrics()
    {
        CompletenessText.Text = "98.5%";
        GapsCountText.Text = "3";
        ErrorsCountText.Text = "0";
        LatencyText.Text = "12ms";

        _symbolQuality.Clear();
        _symbolQuality.Add(CreateDemoSymbolQuality("SPY", 99.8, 0, 8));
        _symbolQuality.Add(CreateDemoSymbolQuality("AAPL", 98.2, 2, 12));
        _symbolQuality.Add(CreateDemoSymbolQuality("MSFT", 99.5, 1, 10));
        _symbolQuality.Add(CreateDemoSymbolQuality("GOOGL", 97.8, 3, 15));
        _symbolQuality.Add(CreateDemoSymbolQuality("AMZN", 99.1, 0, 11));

        NoSymbolsText.Visibility = Visibility.Collapsed;
    }

    private static SymbolQualityModel CreateDemoSymbolQuality(string symbol, double quality, int gaps, double latency)
    {
        return new SymbolQualityModel
        {
            Symbol = symbol,
            QualityPercent = $"{quality:F1}%",
            QualityWidth = quality * 2,
            QualityColor = new SolidColorBrush(
                quality >= 99 ? Color.FromRgb(63, 185, 80) :
                quality >= 95 ? Color.FromRgb(255, 193, 7) :
                Color.FromRgb(244, 67, 54)),
            GapsText = gaps == 0 ? "No gaps" : $"{gaps} gap{(gaps > 1 ? "s" : "")}",
            LatencyText = $"{latency:F0}ms"
        };
    }

    private async Task LoadGapsAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/gaps?range={_timeRange}", ct);

            _gaps.Clear();

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var gap in data.EnumerateArray())
                    {
                        var gapId = gap.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                        var symbol = gap.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var start = gap.TryGetProperty("startTime", out var st) ? st.GetDateTime() : DateTime.MinValue;
                        var end = gap.TryGetProperty("endTime", out var et) ? et.GetDateTime() : DateTime.MinValue;
                        var missingBars = gap.TryGetProperty("missingBars", out var mb) ? mb.GetInt32() : 0;

                        var duration = end - start;
                        var durationText = duration.TotalDays >= 1 ? $"{duration.TotalDays:F0} days" :
                                          duration.TotalHours >= 1 ? $"{duration.TotalHours:F0} hours" :
                                          $"{duration.TotalMinutes:F0} mins";

                        _gaps.Add(new GapModel
                        {
                            GapId = gapId,
                            Symbol = symbol,
                            Description = $"Missing {missingBars} bars between {start:yyyy-MM-dd HH:mm} and {end:yyyy-MM-dd HH:mm}",
                            Duration = durationText
                        });
                    }
                }
            }
            else
            {
                // Load demo gaps
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
            Description = "Missing 156 bars between 2024-01-15 09:30 and 2024-01-17 16:00",
            Duration = "2 days"
        });
        _gaps.Add(new GapModel
        {
            GapId = "gap-2",
            Symbol = "GOOGL",
            Description = "Missing 45 bars between 2024-01-20 14:00 and 2024-01-20 15:30",
            Duration = "1.5 hours"
        });
        _gaps.Add(new GapModel
        {
            GapId = "gap-3",
            Symbol = "MSFT",
            Description = "Missing 12 bars between 2024-01-22 10:00 and 2024-01-22 10:15",
            Duration = "15 mins"
        });

        NoGapsText.Visibility = _gaps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadAnomaliesAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/anomalies?range={_timeRange}", ct);

            _anomalies.Clear();

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var anomaly in data.EnumerateArray())
                    {
                        var symbol = anomaly.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                        var description = anomaly.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        var severity = anomaly.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "low" : "low";
                        var timestamp = anomaly.TryGetProperty("timestamp", out var ts) ? ts.GetDateTime() : DateTime.UtcNow;

                        _anomalies.Add(new AnomalyModel
                        {
                            Symbol = symbol,
                            Description = description,
                            Timestamp = timestamp.ToString("MMM d HH:mm"),
                            SeverityColor = new SolidColorBrush(severity.ToLowerInvariant() switch
                            {
                                "high" or "critical" => Color.FromRgb(244, 67, 54),
                                "medium" => Color.FromRgb(255, 193, 7),
                                _ => Color.FromRgb(139, 148, 158)
                            })
                        });
                    }
                }
            }

            NoAnomaliesText.Visibility = _anomalies.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (HttpRequestException)
        {
            _anomalies.Clear();
            NoAnomaliesText.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadLatencyDistributionAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/quality/latency?range={_timeRange}", ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                P50Text.Text = data.TryGetProperty("p50", out var p50) ? $"{p50.GetDouble():F0}ms" : "--";
                P75Text.Text = data.TryGetProperty("p75", out var p75) ? $"{p75.GetDouble():F0}ms" : "--";
                P90Text.Text = data.TryGetProperty("p90", out var p90) ? $"{p90.GetDouble():F0}ms" : "--";
                P95Text.Text = data.TryGetProperty("p95", out var p95) ? $"{p95.GetDouble():F0}ms" : "--";
                P99Text.Text = data.TryGetProperty("p99", out var p99) ? $"{p99.GetDouble():F0}ms" : "--";
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

    private void TimeRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeRangeCombo.SelectedItem is ComboBoxItem item)
        {
            _timeRange = item.Content?.ToString() switch
            {
                "Last Hour" => "1h",
                "Last 24 Hours" => "24h",
                "Last 7 Days" => "7d",
                "Last 30 Days" => "30d",
                _ => "1h"
            };

            _ = RefreshDataAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
        NotificationService.Instance.ShowNotification(
            "Refreshed",
            "Data quality metrics have been refreshed.",
            NotificationType.Info);
    }

    private async void RepairGap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string gapId) return;

        var gap = _gaps.FirstOrDefault(g => g.GapId == gapId);
        if (gap == null) return;

        try
        {
            // Request gap repair via API
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/quality/gaps/{gapId}/repair",
                null,
                _cts!.Token);

            if (response.IsSuccessStatusCode)
            {
                _gaps.Remove(gap);
                NoGapsText.Visibility = _gaps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                NotificationService.Instance.ShowNotification(
                    "Gap Repair Started",
                    $"Repair for {gap.Symbol} gap has been initiated.",
                    NotificationType.Success);
            }
            else
            {
                NotificationService.Instance.ShowNotification(
                    "Repair Failed",
                    "Failed to initiate gap repair. Please try again.",
                    NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to repair gap", ex);
            NotificationService.Instance.ShowNotification(
                "Repair Failed",
                "An error occurred while initiating gap repair.",
                NotificationType.Error);
        }
    }

    private async void RepairAllGaps_Click(object sender, RoutedEventArgs e)
    {
        if (_gaps.Count == 0)
        {
            NotificationService.Instance.ShowNotification(
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
                _cts!.Token);

            if (response.IsSuccessStatusCode)
            {
                var count = _gaps.Count;
                _gaps.Clear();
                NoGapsText.Visibility = Visibility.Visible;

                NotificationService.Instance.ShowNotification(
                    "Repair Started",
                    $"Initiated repair for {count} gap(s).",
                    NotificationType.Success);
            }
            else
            {
                NotificationService.Instance.ShowNotification(
                    "Repair Failed",
                    "Failed to initiate gap repairs. Please try again.",
                    NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to repair all gaps", ex);
            NotificationService.Instance.ShowNotification(
                "Repair Failed",
                "An error occurred while initiating gap repairs.",
                NotificationType.Error);
        }
    }
}

/// <summary>
/// Model for symbol quality display.
/// </summary>
public class SymbolQualityModel
{
    public string Symbol { get; set; } = string.Empty;
    public string QualityPercent { get; set; } = string.Empty;
    public double QualityWidth { get; set; }
    public SolidColorBrush QualityColor { get; set; } = new(Colors.Gray);
    public string GapsText { get; set; } = string.Empty;
    public string LatencyText { get; set; } = string.Empty;
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

/// <summary>
/// Model for anomaly display.
/// </summary>
public class AnomalyModel
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public SolidColorBrush SeverityColor { get; set; } = new(Colors.Gray);
}
