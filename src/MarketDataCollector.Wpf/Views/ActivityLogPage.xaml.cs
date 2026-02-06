using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;
using Microsoft.Win32;
using Timer = System.Timers.Timer;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Activity log page showing system events and application logs.
/// </summary>
public partial class ActivityLogPage : Page
{
    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<LogEntryModel> _allLogs = new();
    private readonly ObservableCollection<LogEntryModel> _filteredLogs = new();
    private Timer? _refreshTimer;
    private CancellationTokenSource? _cts;
    private string _baseUrl = "http://localhost:8080";
    private string _levelFilter = "All";
    private string _categoryFilter = "All";
    private string _searchText = string.Empty;

    public ActivityLogPage()
    {
        InitializeComponent();

        LogList.ItemsSource = _filteredLogs;

        // Get base URL from StatusService
        _baseUrl = StatusService.Instance.BaseUrl;

        // Subscribe to logging service events
        LoggingService.Instance.LogWritten += OnLogEntryAdded;

        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        LoggingService.Instance.LogWritten -= OnLogEntryAdded;
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadLogsAsync();

        // Start refresh timer (every 5 seconds to check for new logs from API)
        _refreshTimer = new Timer(5000);
        _refreshTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(LoadLogsAsync);
        _refreshTimer.Start();
    }

    private void OnLogEntryAdded(object? sender, ActivityLogEntryEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var entry = new LogEntryModel
            {
                RawTimestamp = e.Timestamp,
                Timestamp = e.Timestamp.ToString("HH:mm:ss"),
                Level = e.Level.ToString(),
                Category = "System",
                Message = e.Message,
                LevelBackground = GetLevelBackground(e.Level.ToString()),
                LevelForeground = GetLevelForeground(e.Level.ToString())
            };

            _allLogs.Insert(0, entry);

            // Limit total logs
            while (_allLogs.Count > 1000)
            {
                _allLogs.RemoveAt(_allLogs.Count - 1);
            }

            ApplyFilters();

            // Auto-scroll if enabled
            if (AutoScrollCheck.IsChecked == true && _filteredLogs.Count > 0)
            {
                LogList.ScrollIntoView(_filteredLogs[0]);
            }
        });
    }

    private async Task LoadLogsAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/logs?limit=500", _cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_cts.Token);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.ValueKind == JsonValueKind.Array)
                {
                    var existingIds = _allLogs.Select(l => $"{l.RawTimestamp:O}_{l.Message}").ToHashSet();

                    foreach (var item in data.EnumerateArray())
                    {
                        var timestamp = item.TryGetProperty("timestamp", out var ts) ? ts.GetDateTime() : DateTime.UtcNow;
                        var level = item.TryGetProperty("level", out var lv) ? lv.GetString() ?? "Info" : "Info";
                        var category = item.TryGetProperty("category", out var cat) ? cat.GetString() ?? "System" : "System";
                        var message = item.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";

                        var id = $"{timestamp:O}_{message}";
                        if (!existingIds.Contains(id))
                        {
                            _allLogs.Add(new LogEntryModel
                            {
                                RawTimestamp = timestamp,
                                Timestamp = timestamp.ToString("HH:mm:ss"),
                                Level = level,
                                Category = category,
                                Message = message,
                                LevelBackground = GetLevelBackground(level),
                                LevelForeground = GetLevelForeground(level)
                            });
                        }
                    }
                }

                ApplyFilters();
            }
            else
            {
                // Load demo logs if API not available
                if (_allLogs.Count == 0)
                {
                    LoadDemoLogs();
                }
            }
        }
        catch (HttpRequestException)
        {
            if (_allLogs.Count == 0)
            {
                LoadDemoLogs();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load activity logs", ex);
        }
    }

    private void LoadDemoLogs()
    {
        var now = DateTime.Now;
        var demoLogs = new List<(string level, string category, string message, int minutesAgo)>
        {
            ("Info", "System", "Application started successfully", 0),
            ("Info", "Connection", "Connected to Alpaca Markets streaming API", 1),
            ("Info", "Data", "Subscribed to SPY, AAPL, MSFT, GOOGL, AMZN", 1),
            ("Debug", "Data", "Received 1,234 trade events in the last minute", 2),
            ("Info", "Backfill", "Historical backfill completed for SPY (2024-01-01 to 2024-01-20)", 5),
            ("Warning", "Connection", "Connection latency increased to 150ms", 8),
            ("Info", "Storage", "Data archived: 156 files compressed, 2.3 GB saved", 10),
            ("Info", "System", "Configuration reloaded from appsettings.json", 15),
            ("Error", "Data", "Failed to parse quote data for symbol XYZ: Invalid format", 18),
            ("Info", "Connection", "Reconnected to streaming API after brief interruption", 20),
            ("Debug", "System", "Memory usage: 245 MB, GC Gen0: 12, Gen1: 3, Gen2: 1", 25),
            ("Info", "Backfill", "Scheduled backfill started for TSLA", 30),
            ("Warning", "Data", "Data gap detected for AAPL between 14:30 and 14:45", 35),
            ("Info", "Storage", "Tiered storage migration completed: 45 files moved to cold storage", 40)
        };

        foreach (var (level, category, message, minutesAgo) in demoLogs)
        {
            var timestamp = now.AddMinutes(-minutesAgo);
            _allLogs.Add(new LogEntryModel
            {
                RawTimestamp = timestamp,
                Timestamp = timestamp.ToString("HH:mm:ss"),
                Level = level,
                Category = category,
                Message = message,
                LevelBackground = GetLevelBackground(level),
                LevelForeground = GetLevelForeground(level)
            });
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _filteredLogs.Clear();

        var filtered = _allLogs.AsEnumerable();

        // Apply level filter
        if (_levelFilter != "All")
        {
            filtered = filtered.Where(l => l.Level.Equals(_levelFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply category filter
        if (_categoryFilter != "All")
        {
            filtered = filtered.Where(l => l.Category.Equals(_categoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filtered = filtered.Where(l =>
                l.Message.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                l.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Sort by timestamp descending and add to filtered collection
        foreach (var log in filtered.OrderByDescending(l => l.RawTimestamp))
        {
            _filteredLogs.Add(log);
        }

        // Update count
        LogCountText.Text = $"{_filteredLogs.Count} entries";
        NoLogsText.Visibility = _filteredLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static SolidColorBrush GetLevelBackground(string level)
    {
        return new SolidColorBrush(level.ToUpperInvariant() switch
        {
            "ERROR" => Color.FromArgb(40, 244, 67, 54),
            "WARNING" or "WARN" => Color.FromArgb(40, 255, 193, 7),
            "INFO" => Color.FromArgb(40, 88, 166, 255),
            "DEBUG" => Color.FromArgb(40, 139, 148, 158),
            _ => Color.FromArgb(40, 139, 148, 158)
        });
    }

    private static SolidColorBrush GetLevelForeground(string level)
    {
        return new SolidColorBrush(level.ToUpperInvariant() switch
        {
            "ERROR" => Color.FromRgb(244, 67, 54),
            "WARNING" or "WARN" => Color.FromRgb(255, 193, 7),
            "INFO" => Color.FromRgb(88, 166, 255),
            "DEBUG" => Color.FromRgb(139, 148, 158),
            _ => Color.FromRgb(139, 148, 158)
        });
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LevelFilterCombo.SelectedItem is ComboBoxItem levelItem)
        {
            _levelFilter = levelItem.Content?.ToString() ?? "All";
        }

        if (CategoryFilterCombo.SelectedItem is ComboBoxItem categoryItem)
        {
            _categoryFilter = categoryItem.Content?.ToString() ?? "All";
        }

        ApplyFilters();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        ApplyFilters();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Activity Log",
            Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = $"activity_log_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();

                if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("Timestamp,Level,Category,Message");
                    foreach (var log in _filteredLogs)
                    {
                        var escapedMessage = $"\"{log.Message.Replace("\"", "\"\"")}\"";
                        sb.AppendLine($"{log.RawTimestamp:O},{log.Level},{log.Category},{escapedMessage}");
                    }
                }
                else
                {
                    foreach (var log in _filteredLogs)
                    {
                        sb.AppendLine($"[{log.RawTimestamp:O}] [{log.Level}] [{log.Category}] {log.Message}");
                    }
                }

                File.WriteAllText(dialog.FileName, sb.ToString());

                NotificationService.Instance.ShowNotification(
                    "Export Complete",
                    $"Exported {_filteredLogs.Count} log entries to {Path.GetFileName(dialog.FileName)}",
                    NotificationType.Success);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to export activity log", ex);
                NotificationService.Instance.ShowNotification(
                    "Export Failed",
                    "An error occurred while exporting the activity log.",
                    NotificationType.Error);
            }
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all log entries?",
            "Clear Activity Log",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _allLogs.Clear();
            _filteredLogs.Clear();
            NoLogsText.Visibility = Visibility.Visible;
            LogCountText.Text = "0 entries";

            NotificationService.Instance.ShowNotification(
                "Cleared",
                "Activity log has been cleared.",
                NotificationType.Info);
        }
    }
}

/// <summary>
/// Model for log entry display.
/// </summary>
public class LogEntryModel
{
    public DateTime RawTimestamp { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public SolidColorBrush LevelBackground { get; set; } = new(Colors.Transparent);
    public SolidColorBrush LevelForeground { get; set; } = new(Colors.Gray);
}

/// <summary>
/// Event args for log entry added events.
/// </summary>
public class ActivityLogEntryEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Message { get; set; } = string.Empty;
}
