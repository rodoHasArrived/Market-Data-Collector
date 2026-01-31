using System.Windows;
using System.Windows.Media;

namespace MarketDataCollector.Wpf.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ILoggingService _logger;
    private AppTheme _currentTheme = AppTheme.Light;

    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService(ILoggingService logger)
    {
        _logger = logger;
        
        // Initialize with saved preference or system default
        var savedTheme = LoadThemePreference();
        SetTheme(savedTheme);
    }

    public void SetTheme(AppTheme theme)
    {
        _logger.Log($"Setting theme to: {theme}");
        
        var actualTheme = theme == AppTheme.System ? GetSystemTheme() : theme;
        CurrentTheme = actualTheme;
        
        ApplyTheme(actualTheme);
        SaveThemePreference(theme);
    }

    public AppTheme GetSystemTheme()
    {
        try
        {
            // Check Windows system theme
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to get system theme: {ex.Message}");
        }
        
        return AppTheme.Light;
    }

    private void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        try
        {
            // Clear existing theme resources
            var themeDicts = app.Resources.MergedDictionaries
                .Where(d => d.Source?.OriginalString.Contains("Themes/") == true)
                .ToList();
            
            foreach (var dict in themeDicts)
            {
                app.Resources.MergedDictionaries.Remove(dict);
            }

            // Load new theme
            var themeFile = theme == AppTheme.Dark ? "DarkTheme.xaml" : "LightTheme.xaml";
            var themeUri = new Uri($"Themes/{themeFile}", UriKind.Relative);
            
            var themeDict = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(themeDict);
            
            _logger.Log($"Applied {theme} theme");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to apply theme: {ex.Message}");
        }
    }

    private AppTheme LoadThemePreference()
    {
        try
        {
            var saved = Properties.Settings.Default.Theme;
            if (Enum.TryParse<AppTheme>(saved, out var theme))
            {
                return theme;
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to load theme preference: {ex.Message}");
        }
        
        return AppTheme.System;
    }

    private void SaveThemePreference(AppTheme theme)
    {
        try
        {
            Properties.Settings.Default.Theme = theme.ToString();
            Properties.Settings.Default.Save();
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to save theme preference: {ex.Message}");
        }
    }
}
