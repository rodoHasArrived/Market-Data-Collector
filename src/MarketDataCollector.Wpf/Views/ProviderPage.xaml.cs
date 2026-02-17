using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Services;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Page for viewing and managing market data providers for real-time streaming and historical data.
/// Phase 1: Backfill provider settings panel with enabled toggle, priority, rate limits.
/// Phase 2: Fallback chain preview with health status and config source badges.
/// Phase 3: Driven by provider metadata descriptors for dynamic UI.
/// Phase 4: Validation, dry-run plan, audit trail.
/// </summary>
public partial class ProviderPage : Page
{
    private readonly WpfServices.NavigationService _navigationService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly WpfServices.ConfigService _configService;
    private readonly BackfillProviderConfigService _providerConfigService;
    private bool _isLoading;

    public ProviderPage(
        WpfServices.NavigationService navigationService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();

        _navigationService = navigationService;
        _notificationService = notificationService;
        _configService = WpfServices.ConfigService.Instance;
        _providerConfigService = BackfillProviderConfigService.Instance;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadProviderSettingsAsync();
    }

    private async System.Threading.Tasks.Task LoadProviderSettingsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            var providersConfig = await _configService.GetBackfillProvidersConfigAsync();
            var statuses = await _providerConfigService.GetProviderStatusesAsync(providersConfig);

            // Build view models for the settings list
            var settingsViewModels = statuses.Select(s => new ProviderSettingsViewModel
            {
                ProviderId = s.Metadata.ProviderId,
                DisplayName = s.Metadata.DisplayName,
                IsEnabled = s.Options.Enabled,
                Priority = s.Options.Priority ?? s.Metadata.DefaultPriority,
                DataTypes = string.Join(", ", s.Metadata.DataTypes),
                RateLimitPerMinute = s.Options.RateLimitPerMinute?.ToString() ?? "",
                RateLimitPerHour = s.Options.RateLimitPerHour?.ToString() ?? "",
                ConfigSource = s.EffectiveConfigSource,
                HealthStatus = s.HealthStatus,
            }).ToList();

            ProviderSettingsList.ItemsSource = settingsViewModels;

            // Update fallback chain
            await UpdateFallbackChainAsync(providersConfig);

            // Update audit log
            UpdateAuditLog();
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync(
                "Load Error",
                $"Failed to load provider settings: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async System.Threading.Tasks.Task UpdateFallbackChainAsync(BackfillProvidersConfigDto? config)
    {
        var chain = await _providerConfigService.GetFallbackChainAsync(config);

        var chainViewModels = chain.Select(s => new FallbackChainViewModel
        {
            Priority = (s.Options.Priority ?? s.Metadata.DefaultPriority).ToString(),
            DisplayName = s.Metadata.DisplayName,
            DataTypes = string.Join(", ", s.Metadata.DataTypes),
            HealthStatus = s.HealthStatus,
        }).ToList();

        FallbackChainList.ItemsSource = chainViewModels;
        EmptyFallbackChainText.Visibility = chain.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateAuditLog()
    {
        var entries = _providerConfigService.GetAuditLog(20);
        var viewModels = entries.Select(e => new AuditLogViewModel
        {
            Timestamp = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            ProviderId = e.ProviderId,
            Action = e.Action,
            Summary = e.Action == "reset"
                ? "Reset to defaults"
                : "Configuration updated",
        }).ToList();

        AuditLogList.ItemsSource = viewModels;
        EmptyAuditText.Visibility = entries.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void ProviderToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || sender is not CheckBox checkBox) return;
        if (checkBox.DataContext is not ProviderSettingsViewModel vm) return;

        try
        {
            var options = await BuildOptionsFromViewModel(vm);
            await _configService.SetBackfillProviderOptionsAsync(vm.ProviderId, options);

            var config = await _configService.GetBackfillProvidersConfigAsync();
            await UpdateFallbackChainAsync(config);
            UpdateAuditLog();
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync("Save Error", ex.Message);
        }
    }

    private async void PriorityField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoading || sender is not TextBox textBox) return;
        if (textBox.DataContext is not ProviderSettingsViewModel vm) return;

        if (!int.TryParse(textBox.Text, out var priority) || priority < 0)
        {
            _notificationService.NotifyWarning("Validation", "Priority must be a non-negative integer.");
            return;
        }

        try
        {
            vm.Priority = priority;
            var options = await BuildOptionsFromViewModel(vm);
            await _configService.SetBackfillProviderOptionsAsync(vm.ProviderId, options);

            var config = await _configService.GetBackfillProvidersConfigAsync();
            await UpdateFallbackChainAsync(config);
            UpdateAuditLog();
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync("Save Error", ex.Message);
        }
    }

    private async void RateLimitField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoading || sender is not TextBox textBox) return;
        if (textBox.DataContext is not ProviderSettingsViewModel vm) return;

        try
        {
            var options = await BuildOptionsFromViewModel(vm);
            await _configService.SetBackfillProviderOptionsAsync(vm.ProviderId, options);
            UpdateAuditLog();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _notificationService.NotifyWarning("Validation", ex.Message);
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync("Save Error", ex.Message);
        }
    }

    private async void ResetProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var providerId = button.Tag as string;
        if (string.IsNullOrEmpty(providerId)) return;

        try
        {
            await _configService.ResetBackfillProviderOptionsAsync(providerId);
            _notificationService.NotifyInfo("Reset", $"Provider '{providerId}' reset to defaults.");
            await LoadProviderSettingsAsync();
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync("Reset Error", ex.Message);
        }
    }

    private async void ValidateConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _configService.ValidateConfigAsync();

            ValidationPanel.Visibility = Visibility.Visible;

            if (result.IsValid && result.Warnings.Length == 0)
            {
                ValidationPanel.Background = new SolidColorBrush(Color.FromArgb(30, 0, 180, 0));
                ValidationText.Text = "Configuration is valid.";
                ValidationText.Foreground = (Brush)FindResource("SuccessColorBrush");
            }
            else if (result.IsValid)
            {
                ValidationPanel.Background = new SolidColorBrush(Color.FromArgb(30, 255, 180, 0));
                var text = "Configuration is valid with warnings:\n" +
                    string.Join("\n", result.Warnings.Select(w => $"  - {w}"));
                ValidationText.Text = text;
                ValidationText.Foreground = (Brush)FindResource("WarningColorBrush");
            }
            else
            {
                ValidationPanel.Background = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0));
                var text = "Configuration errors:\n" +
                    string.Join("\n", result.Errors.Select(err => $"  - {err}"));
                if (result.Warnings.Length > 0)
                {
                    text += "\nWarnings:\n" +
                        string.Join("\n", result.Warnings.Select(w => $"  - {w}"));
                }
                ValidationText.Text = text;
                ValidationText.Foreground = (Brush)FindResource("ErrorColorBrush");
            }
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync("Validation Error", ex.Message);
        }
    }

    private async void RefreshProviders_Click(object sender, RoutedEventArgs e)
    {
        await LoadProviderSettingsAsync();
    }

    private async void DryRunPlan_Click(object sender, RoutedEventArgs e)
    {
        var symbolText = DryRunSymbolsInput.Text?.Trim();
        if (string.IsNullOrEmpty(symbolText))
        {
            _notificationService.NotifyWarning("Input Required", "Enter one or more symbols separated by commas.");
            return;
        }

        try
        {
            var symbols = symbolText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var config = await _configService.GetBackfillProvidersConfigAsync();
            var plan = await _providerConfigService.GenerateDryRunPlanAsync(config, symbols);

            if (plan.ValidationErrors.Length > 0)
            {
                _ = _notificationService.NotifyErrorAsync("Plan Error", string.Join("; ", plan.ValidationErrors));
                DryRunResultsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var viewModels = plan.Symbols.Select(s => new DryRunResultViewModel
            {
                Symbol = s.Symbol,
                SelectedProvider = s.SelectedProvider ?? "none",
                FallbackSequence = $"({string.Join(" > ", s.ProviderSequence)})",
            }).ToList();

            DryRunResultsList.ItemsSource = viewModels;
            DryRunResultsPanel.Visibility = Visibility.Visible;

            if (plan.Warnings.Length > 0)
            {
                DryRunWarningsText.Text = string.Join("\n", plan.Warnings);
                DryRunWarningsText.Visibility = Visibility.Visible;
            }
            else
            {
                DryRunWarningsText.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            _ = _notificationService.NotifyErrorAsync("Dry-Run Error", ex.Message);
        }
    }

    private void TestAllConnections_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.NotifyInfo(
            "Connection Test",
            "Testing connectivity to all configured providers...");
    }

    private void ConfigureProvider_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("DataSources");
    }

    private static Task<BackfillProviderOptionsDto> BuildOptionsFromViewModel(ProviderSettingsViewModel vm)
    {
        var options = new BackfillProviderOptionsDto
        {
            Enabled = vm.IsEnabled,
            Priority = vm.Priority,
        };

        if (int.TryParse(vm.RateLimitPerMinute, out var rpm) && rpm > 0)
        {
            options.RateLimitPerMinute = rpm;
        }

        if (int.TryParse(vm.RateLimitPerHour, out var rph) && rph > 0)
        {
            options.RateLimitPerHour = rph;
        }

        return Task.FromResult(options);
    }
}

/// <summary>
/// View model for provider settings rows in the backfill provider settings panel.
/// </summary>
internal sealed class ProviderSettingsViewModel : INotifyPropertyChanged
{
    private bool _isEnabled;
    private int _priority;
    private string _rateLimitPerMinute = "";
    private string _rateLimitPerHour = "";

    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataTypes { get; set; } = string.Empty;
    public string ConfigSource { get; set; } = "default";
    public string HealthStatus { get; set; } = "unknown";

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public int Priority
    {
        get => _priority;
        set { _priority = value; OnPropertyChanged(); }
    }

    public string RateLimitPerMinute
    {
        get => _rateLimitPerMinute;
        set { _rateLimitPerMinute = value; OnPropertyChanged(); }
    }

    public string RateLimitPerHour
    {
        get => _rateLimitPerHour;
        set { _rateLimitPerHour = value; OnPropertyChanged(); }
    }

    public Brush ConfigSourceBrush => ConfigSource switch
    {
        "user" => new SolidColorBrush(Color.FromArgb(40, 0, 120, 212)),
        "env" => new SolidColorBrush(Color.FromArgb(40, 0, 180, 0)),
        _ => new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// View model for fallback chain preview items.
/// </summary>
internal sealed class FallbackChainViewModel
{
    public string Priority { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataTypes { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = "unknown";

    public Brush HealthBrush => HealthStatus switch
    {
        "healthy" => new SolidColorBrush(Color.FromRgb(0, 180, 0)),
        "degraded" => new SolidColorBrush(Color.FromRgb(255, 180, 0)),
        "unhealthy" => new SolidColorBrush(Color.FromRgb(255, 60, 60)),
        _ => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
    };
}

/// <summary>
/// View model for dry-run backfill plan result items.
/// </summary>
internal sealed class DryRunResultViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public string SelectedProvider { get; set; } = string.Empty;
    public string FallbackSequence { get; set; } = string.Empty;
}

/// <summary>
/// View model for audit trail log items.
/// </summary>
internal sealed class AuditLogViewModel
{
    public string Timestamp { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
