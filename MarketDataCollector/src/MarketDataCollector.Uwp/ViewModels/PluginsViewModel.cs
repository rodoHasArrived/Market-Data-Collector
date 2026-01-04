using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Infrastructure.DataSources.Plugins;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.ViewModels;

/// <summary>
/// ViewModel for the Plugins management page.
/// </summary>
public partial class PluginsViewModel : ObservableObject, IDisposable
{
    private readonly IDataSourcePluginManager? _pluginManager;
    private readonly IDisposable? _stateSubscription;

    /// <summary>
    /// Collection of plugins for display.
    /// </summary>
    public ObservableCollection<PluginDisplayInfo> Plugins { get; } = new();

    /// <summary>
    /// Recent plugin load results.
    /// </summary>
    public ObservableCollection<PluginInstallResult> RecentResults { get; } = new();

    #region Observable Properties

    /// <summary>
    /// Total number of plugins.
    /// </summary>
    [ObservableProperty]
    private int _totalPlugins;

    /// <summary>
    /// Number of active plugins.
    /// </summary>
    [ObservableProperty]
    private int _activePlugins;

    /// <summary>
    /// Number of plugins with errors.
    /// </summary>
    [ObservableProperty]
    private int _errorPlugins;

    /// <summary>
    /// Plugin directory path.
    /// </summary>
    [ObservableProperty]
    private string _pluginDirectory = string.Empty;

    /// <summary>
    /// Whether directory watching is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _directoryWatchingEnabled;

    /// <summary>
    /// Whether auto-load is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _autoLoadEnabled;

    /// <summary>
    /// Whether hot reload is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _hotReloadEnabled;

    /// <summary>
    /// Host version.
    /// </summary>
    [ObservableProperty]
    private string _hostVersion = "1.0.0";

    /// <summary>
    /// Whether the system is initialized.
    /// </summary>
    [ObservableProperty]
    private bool _isInitialized;

    /// <summary>
    /// Whether an operation is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Currently selected plugin.
    /// </summary>
    [ObservableProperty]
    private PluginDisplayInfo? _selectedPlugin;

    /// <summary>
    /// Status message for InfoBar.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Severity of the status (Informational, Success, Warning, Error).
    /// </summary>
    [ObservableProperty]
    private string _statusSeverity = "Informational";

    /// <summary>
    /// Whether the status bar is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusVisible;

    #endregion

    /// <summary>
    /// Creates a new PluginsViewModel.
    /// </summary>
    public PluginsViewModel(IDataSourcePluginManager? pluginManager = null)
    {
        _pluginManager = pluginManager;

        // Subscribe to state changes
        if (_pluginManager != null)
        {
            _stateSubscription = _pluginManager.StateChanges
                .Subscribe(OnPluginStateChanged);
        }
    }

    /// <summary>
    /// Loads the initial plugin data.
    /// </summary>
    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            if (_pluginManager == null)
            {
                // Demo mode without plugin manager
                LoadDemoData();
                return;
            }

            // Get system status
            var status = _pluginManager.GetStatus();
            UpdateStatus(status);

            // Load plugins
            RefreshPluginList();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading plugins: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateStatus(PluginSystemStatus status)
    {
        TotalPlugins = status.TotalPlugins;
        ActivePlugins = status.ActivePlugins;
        ErrorPlugins = status.ErrorPlugins;
        PluginDirectory = status.PluginDirectory ?? "Not configured";
        DirectoryWatchingEnabled = status.DirectoryWatchingEnabled;
        IsInitialized = status.IsInitialized;
        HostVersion = status.HostVersion?.ToString() ?? "Unknown";
    }

    private void RefreshPluginList()
    {
        Plugins.Clear();

        if (_pluginManager == null) return;

        foreach (var plugin in _pluginManager.AllPlugins)
        {
            Plugins.Add(PluginDisplayInfo.FromManagedPlugin(plugin));
        }

        TotalPlugins = Plugins.Count;
        ActivePlugins = Plugins.Count(p => p.Status == "Active");
        ErrorPlugins = Plugins.Count(p => p.Status == "Error");
    }

    private void LoadDemoData()
    {
        // Demo data for design-time or testing
        PluginDirectory = "plugins";
        DirectoryWatchingEnabled = true;
        AutoLoadEnabled = true;
        HotReloadEnabled = true;
        HostVersion = "1.5.0";
        IsInitialized = true;

        Plugins.Add(new PluginDisplayInfo
        {
            PluginId = "custom-yahoo",
            Name = "Yahoo Finance Enhanced",
            Version = "2.1.0",
            Author = "Community",
            Description = "Enhanced Yahoo Finance data source with additional metrics",
            Status = "Active",
            IsEnabled = true,
            DataSourceType = "Historical",
            Category = "Free",
            PermissionsText = "Network",
            Priority = 50
        });

        Plugins.Add(new PluginDisplayInfo
        {
            PluginId = "crypto-binance",
            Name = "Binance Crypto",
            Version = "1.0.0",
            Author = "Crypto Team",
            Description = "Real-time cryptocurrency data from Binance",
            Status = "Active",
            IsEnabled = true,
            DataSourceType = "Realtime",
            Category = "Exchange",
            PermissionsText = "Network, Environment",
            Priority = 20
        });

        TotalPlugins = Plugins.Count;
        ActivePlugins = 2;
    }

    #region Commands

    /// <summary>
    /// Refreshes the plugin list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        ShowStatus("Plugin list refreshed", "Success");
    }

    /// <summary>
    /// Scans the plugin directory for new plugins.
    /// </summary>
    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        if (_pluginManager == null) return;

        IsLoading = true;
        try
        {
            var results = await _pluginManager.ScanDirectoryAsync(PluginDirectory);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;

            RefreshPluginList();

            if (failCount > 0)
            {
                ShowStatus($"Scan complete: {successCount} loaded, {failCount} failed", "Warning");
            }
            else
            {
                ShowStatus($"Scan complete: {successCount} plugins loaded", "Success");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Scan failed: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Installs a plugin from a file path.
    /// </summary>
    [RelayCommand]
    private async Task InstallPluginAsync(string filePath)
    {
        if (_pluginManager == null || string.IsNullOrEmpty(filePath)) return;

        IsLoading = true;
        try
        {
            var result = await _pluginManager.LoadPluginAsync(filePath);

            var installResult = new PluginInstallResult
            {
                Success = result.Success,
                PluginId = result.Plugin?.PluginId,
                ErrorMessage = result.ErrorMessage,
                FilePath = filePath
            };

            RecentResults.Insert(0, installResult);
            if (RecentResults.Count > 10)
            {
                RecentResults.RemoveAt(RecentResults.Count - 1);
            }

            if (result.Success)
            {
                RefreshPluginList();
                ShowStatus($"Plugin '{result.Plugin?.PluginId}' installed successfully", "Success");
            }
            else
            {
                ShowStatus($"Failed to install plugin: {result.ErrorMessage}", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Installation error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Enables a plugin.
    /// </summary>
    [RelayCommand]
    private async Task EnablePluginAsync(string pluginId)
    {
        if (_pluginManager == null || string.IsNullOrEmpty(pluginId)) return;

        try
        {
            var result = await _pluginManager.EnablePluginAsync(pluginId);
            if (result.Success)
            {
                UpdatePluginInList(pluginId, p => { p.IsEnabled = true; p.Status = "Active"; });
                ShowStatus($"Plugin '{pluginId}' enabled", "Success");
            }
            else
            {
                ShowStatus($"Failed to enable plugin: {result.ErrorMessage}", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error enabling plugin: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Disables a plugin.
    /// </summary>
    [RelayCommand]
    private async Task DisablePluginAsync(string pluginId)
    {
        if (_pluginManager == null || string.IsNullOrEmpty(pluginId)) return;

        try
        {
            var result = await _pluginManager.DisablePluginAsync(pluginId);
            if (result.Success)
            {
                UpdatePluginInList(pluginId, p => { p.IsEnabled = false; p.Status = "Paused"; });
                ShowStatus($"Plugin '{pluginId}' disabled", "Success");
            }
            else
            {
                ShowStatus($"Failed to disable plugin: {result.ErrorMessage}", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error disabling plugin: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Reloads a plugin.
    /// </summary>
    [RelayCommand]
    private async Task ReloadPluginAsync(string pluginId)
    {
        if (_pluginManager == null || string.IsNullOrEmpty(pluginId)) return;

        IsLoading = true;
        try
        {
            var result = await _pluginManager.ReloadPluginAsync(pluginId);
            if (result.Success)
            {
                RefreshPluginList();
                ShowStatus($"Plugin '{pluginId}' reloaded", "Success");
            }
            else
            {
                ShowStatus($"Failed to reload plugin: {result.ErrorMessage}", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error reloading plugin: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Unloads a plugin.
    /// </summary>
    [RelayCommand]
    private async Task UnloadPluginAsync(string pluginId)
    {
        if (_pluginManager == null || string.IsNullOrEmpty(pluginId)) return;

        IsLoading = true;
        try
        {
            var result = await _pluginManager.UnloadPluginAsync(pluginId);
            if (result.Success)
            {
                var plugin = Plugins.FirstOrDefault(p => p.PluginId == pluginId);
                if (plugin != null)
                {
                    Plugins.Remove(plugin);
                }
                UpdateCounts();
                ShowStatus($"Plugin '{pluginId}' unloaded", "Success");
            }
            else
            {
                ShowStatus($"Failed to unload plugin: {result.ErrorMessage}", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error unloading plugin: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helpers

    private void OnPluginStateChanged(PluginStateChange change)
    {
        // Update the UI on the main thread
        UpdatePluginInList(change.PluginId, p =>
        {
            p.Status = change.NewState.ToString();
        });
        UpdateCounts();
    }

    private void UpdatePluginInList(string pluginId, Action<PluginDisplayInfo> update)
    {
        var plugin = Plugins.FirstOrDefault(p => p.PluginId == pluginId);
        if (plugin != null)
        {
            update(plugin);
        }
    }

    private void UpdateCounts()
    {
        TotalPlugins = Plugins.Count;
        ActivePlugins = Plugins.Count(p => p.Status == "Active");
        ErrorPlugins = Plugins.Count(p => p.Status == "Error");
    }

    private void ShowStatus(string message, string severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusVisible = true;
    }

    /// <summary>
    /// Hides the status message.
    /// </summary>
    public void HideStatus()
    {
        IsStatusVisible = false;
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        _stateSubscription?.Dispose();
    }
}
