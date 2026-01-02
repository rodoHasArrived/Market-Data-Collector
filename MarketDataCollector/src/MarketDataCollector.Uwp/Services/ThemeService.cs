using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Storage;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing application theme settings.
/// </summary>
public class ThemeService
{
    private static ThemeService? _instance;
    private static readonly object _lock = new();

    private const string ThemeSettingKey = "AppTheme";
    private const string AccentColorKey = "AccentColor";
    private const string CompactModeKey = "CompactMode";

    private Window? _mainWindow;
    private AppTheme _currentTheme = AppTheme.System;

    public static ThemeService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ThemeService();
                }
            }
            return _instance;
        }
    }

    private ThemeService()
    {
        LoadSettings();
    }

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    public AppTheme CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets the actual applied theme (resolves System to Light/Dark).
    /// </summary>
    public AppTheme ActualTheme
    {
        get
        {
            if (_currentTheme == AppTheme.System)
            {
                return GetSystemTheme();
            }
            return _currentTheme;
        }
    }

    /// <summary>
    /// Initializes the theme service with the main window.
    /// </summary>
    public void Initialize(Window window)
    {
        _mainWindow = window;
        ApplyTheme(_currentTheme);
    }

    /// <summary>
    /// Sets the application theme.
    /// </summary>
    public void SetTheme(AppTheme theme)
    {
        _currentTheme = theme;
        SaveSettings();
        ApplyTheme(theme);
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { Theme = theme, ActualTheme = ActualTheme });
    }

    /// <summary>
    /// Toggles between light and dark theme.
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = ActualTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        SetTheme(newTheme);
    }

    /// <summary>
    /// Cycles through themes: System -> Light -> Dark -> System.
    /// </summary>
    public void CycleTheme()
    {
        var newTheme = _currentTheme switch
        {
            AppTheme.System => AppTheme.Light,
            AppTheme.Light => AppTheme.Dark,
            AppTheme.Dark => AppTheme.System,
            _ => AppTheme.System
        };
        SetTheme(newTheme);
    }

    private void ApplyTheme(AppTheme theme)
    {
        if (_mainWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private AppTheme GetSystemTheme()
    {
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var foreground = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);

            // If foreground is light, system is in dark mode
            return foreground.R > 128 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    private void LoadSettings()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue(ThemeSettingKey, out var themeValue))
            {
                _currentTheme = Enum.TryParse<AppTheme>(themeValue?.ToString(), out var theme)
                    ? theme
                    : AppTheme.System;
            }
        }
        catch
        {
            _currentTheme = AppTheme.System;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[ThemeSettingKey] = _currentTheme.ToString();
        }
        catch
        {
            // Settings save failed
        }
    }

    /// <summary>
    /// Event raised when theme changes.
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}

/// <summary>
/// Application theme options.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Follow system theme.
    /// </summary>
    System,

    /// <summary>
    /// Light theme.
    /// </summary>
    Light,

    /// <summary>
    /// Dark theme.
    /// </summary>
    Dark
}

/// <summary>
/// Theme changed event args.
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public AppTheme Theme { get; set; }
    public AppTheme ActualTheme { get; set; }
}
