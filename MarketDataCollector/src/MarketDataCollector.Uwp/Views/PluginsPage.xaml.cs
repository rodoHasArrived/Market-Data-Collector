using System.Collections.ObjectModel;
using System.Diagnostics;
using MarketDataCollector.Infrastructure.DataSources.Plugins;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing data source plugins.
/// </summary>
public sealed partial class PluginsPage : Page
{
    private readonly PluginsViewModel _viewModel;
    private readonly ObservableCollection<PluginDisplayInfo> _plugins = new();

    public PluginsPage()
    {
        InitializeComponent();

        // Get plugin manager from DI if available, otherwise use demo mode
        var pluginManager = App.Current?.Services?.GetService(typeof(IDataSourcePluginManager)) as IDataSourcePluginManager;
        _viewModel = new PluginsViewModel(pluginManager);

        PluginsList.ItemsSource = _plugins;

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadPluginsAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Dispose();
    }

    private async Task LoadPluginsAsync()
    {
        PageLoadingProgress.IsActive = true;

        try
        {
            await _viewModel.LoadAsync();
            UpdateUI();
        }
        finally
        {
            PageLoadingProgress.IsActive = false;
        }
    }

    private void UpdateUI()
    {
        // Update status metrics
        TotalPluginsText.Text = _viewModel.TotalPlugins.ToString();
        ActivePluginsText.Text = _viewModel.ActivePlugins.ToString();
        ErrorPluginsText.Text = _viewModel.ErrorPlugins.ToString();
        HostVersionText.Text = _viewModel.HostVersion;

        // Update settings
        PluginDirectoryText.Text = _viewModel.PluginDirectory;
        DirectoryWatchingCheck.IsChecked = _viewModel.DirectoryWatchingEnabled;
        HotReloadCheck.IsChecked = _viewModel.HotReloadEnabled;
        AutoLoadCheck.IsChecked = _viewModel.AutoLoadEnabled;

        // Update plugin list
        _plugins.Clear();
        foreach (var plugin in _viewModel.Plugins)
        {
            _plugins.Add(plugin);
        }

        PluginCountText.Text = $"({_plugins.Count})";
        NoPluginsText.Visibility = _plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #region Event Handlers

    private async void RefreshPlugins_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
        UpdateUI();
        ShowStatus("Plugin list refreshed", InfoBarSeverity.Success);
    }

    private async void ScanDirectory_Click(object sender, RoutedEventArgs e)
    {
        PageLoadingProgress.IsActive = true;
        try
        {
            await _viewModel.ScanDirectoryAsync();
            UpdateUI();
            ShowStatus("Directory scan complete", InfoBarSeverity.Success);
        }
        finally
        {
            PageLoadingProgress.IsActive = false;
        }
    }

    private async void BrowsePlugin_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add(".dll");

        // Initialize picker with window handle for WinUI 3
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            InstallProgress.IsActive = true;
            try
            {
                await _viewModel.InstallPluginAsync(file.Path);
                UpdateUI();

                if (_viewModel.IsStatusVisible)
                {
                    ShowStatus(_viewModel.StatusMessage ?? "Plugin installed",
                        _viewModel.StatusSeverity == "Error" ? InfoBarSeverity.Error : InfoBarSeverity.Success);
                }
            }
            finally
            {
                InstallProgress.IsActive = false;
            }
        }
    }

    private void OpenPluginFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = _viewModel.PluginDirectory;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
            else
            {
                ShowStatus("Plugin directory not found", InfoBarSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open folder: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void PluginsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PluginsList.SelectedItem is PluginDisplayInfo plugin)
        {
            _viewModel.SelectedPlugin = plugin;
            ShowPluginDetails(plugin);
        }
    }

    private void ShowPluginDetails(PluginDisplayInfo plugin)
    {
        DetailsPluginName.Text = plugin.Name;
        DetailsPluginId.Text = plugin.PluginId;
        DetailsVersion.Text = plugin.Version;
        DetailsDataSourceType.Text = plugin.DataSourceType;
        DetailsCategory.Text = plugin.Category;
        DetailsAssemblyPath.Text = plugin.AssemblyPath;
        DetailsPermissions.Text = plugin.PermissionsText;

        PluginDetailsPanel.Visibility = Visibility.Visible;
    }

    private void CloseDetails_Click(object sender, RoutedEventArgs e)
    {
        PluginDetailsPanel.Visibility = Visibility.Collapsed;
        PluginsList.SelectedItem = null;
        _viewModel.SelectedPlugin = null;
    }

    private async void PluginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.Tag is string pluginId)
        {
            // Delay slightly to allow binding to update
            await Task.Delay(100);

            if (toggle.IsOn)
            {
                await _viewModel.EnablePluginAsync(pluginId);
            }
            else
            {
                await _viewModel.DisablePluginAsync(pluginId);
            }

            // Update the specific plugin in the list
            var plugin = _plugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (plugin != null)
            {
                plugin.Status = toggle.IsOn ? "Active" : "Paused";
            }

            UpdateStatusCounts();
        }
    }

    private async void ReloadPlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string pluginId)
        {
            PageLoadingProgress.IsActive = true;
            try
            {
                await _viewModel.ReloadPluginAsync(pluginId);
                UpdateUI();
                ShowStatus($"Plugin '{pluginId}' reloaded", InfoBarSeverity.Success);
            }
            finally
            {
                PageLoadingProgress.IsActive = false;
            }
        }
    }

    private void ConfigurePlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string pluginId)
        {
            var plugin = _plugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (plugin != null)
            {
                ShowPluginDetails(plugin);
            }

            // TODO: Show configuration dialog/panel
            ShowStatus("Plugin configuration coming soon", InfoBarSeverity.Informational);
        }
    }

    private async void UnloadPlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string pluginId)
        {
            // Confirm unload
            var dialog = new ContentDialog
            {
                Title = "Unload Plugin",
                Content = $"Are you sure you want to unload the plugin '{pluginId}'? You can reload it later from the plugins directory.",
                PrimaryButtonText = "Unload",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                PageLoadingProgress.IsActive = true;
                try
                {
                    await _viewModel.UnloadPluginAsync(pluginId);

                    // Remove from local list
                    var plugin = _plugins.FirstOrDefault(p => p.PluginId == pluginId);
                    if (plugin != null)
                    {
                        _plugins.Remove(plugin);
                    }

                    UpdateStatusCounts();
                    PluginCountText.Text = $"({_plugins.Count})";
                    NoPluginsText.Visibility = _plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                    // Close details if this plugin was selected
                    if (_viewModel.SelectedPlugin?.PluginId == pluginId)
                    {
                        CloseDetails_Click(null, null);
                    }

                    ShowStatus($"Plugin '{pluginId}' unloaded", InfoBarSeverity.Success);
                }
                finally
                {
                    PageLoadingProgress.IsActive = false;
                }
            }
        }
    }

    #endregion

    #region Helpers

    private void UpdateStatusCounts()
    {
        TotalPluginsText.Text = _plugins.Count.ToString();
        ActivePluginsText.Text = _plugins.Count(p => p.Status == "Active").ToString();
        ErrorPluginsText.Text = _plugins.Count(p => p.Status == "Error").ToString();
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    #endregion
}
