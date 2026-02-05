using System;
using System.Windows;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for handling light/dark theme switching in WPF applications.
/// Implements singleton pattern for application-wide theme management.
/// </summary>
public sealed class ThemeService
{
    private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());

    private Window? _mainWindow;
    private AppTheme _currentTheme = AppTheme.Light;

    private const string LightThemeUri = "pack://application:,,,/Themes/LightTheme.xaml";
    private const string DarkThemeUri = "pack://application:,,,/Themes/DarkTheme.xaml";

    /// <summary>
    /// Gets the singleton instance of the ThemeService.
    /// </summary>
    public static ThemeService Instance => _instance.Value;

    /// <summary>
    /// Gets the current application theme.
    /// </summary>
    public AppTheme CurrentTheme => _currentTheme;

    /// <summary>
    /// Occurs when the theme is changed.
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    private ThemeService()
    {
    }

    /// <summary>
    /// Initializes the theme service with the main application window.
    /// </summary>
    /// <param name="window">The main application window.</param>
    /// <exception cref="ArgumentNullException">Thrown when window is null.</exception>
    public void Initialize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _mainWindow = window;
        ApplyTheme(_currentTheme);
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = _currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        SetTheme(newTheme);
    }

    /// <summary>
    /// Sets the application theme to the specified value.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    public void SetTheme(AppTheme theme)
    {
        if (_currentTheme == theme)
        {
            return;
        }

        var previousTheme = _currentTheme;
        _currentTheme = theme;
        ApplyTheme(theme);

        OnThemeChanged(new ThemeChangedEventArgs(previousTheme, theme));
    }

    private void ApplyTheme(AppTheme theme)
    {
        if (_mainWindow is null)
        {
            return;
        }

        var themeUri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;

        try
        {
            // Remove existing theme dictionaries
            var toRemove = new System.Collections.Generic.List<ResourceDictionary>();
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                if (dict.Source?.OriginalString.Contains("Theme", StringComparison.OrdinalIgnoreCase) is true)
                {
                    toRemove.Add(dict);
                }
            }

            foreach (var dict in toRemove)
            {
                Application.Current.Resources.MergedDictionaries.Remove(dict);
            }

            // Add new theme dictionary
            var newThemeDict = new ResourceDictionary
            {
                Source = new Uri(themeUri, UriKind.Absolute)
            };
            Application.Current.Resources.MergedDictionaries.Add(newThemeDict);

            // Update system colors for window chrome
            UpdateWindowChrome(theme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
            // Fall back to programmatic theming if resource dictionaries aren't available
            ApplyProgrammaticTheme(theme);
        }
    }

    private void ApplyProgrammaticTheme(AppTheme theme)
    {
        if (_mainWindow is null)
        {
            return;
        }

        var isDark = theme == AppTheme.Dark;

        Application.Current.Resources["WindowBackgroundBrush"] = isDark
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);

        Application.Current.Resources["TextBrush"] = isDark
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);

        Application.Current.Resources["AccentBrush"] = isDark
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 99, 177));
    }

    private void UpdateWindowChrome(AppTheme theme)
    {
        if (_mainWindow is null)
        {
            return;
        }

        // Update window background based on theme
        var isDark = theme == AppTheme.Dark;
        _mainWindow.Background = isDark
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
    }

    /// <summary>
    /// Raises the ThemeChanged event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnThemeChanged(ThemeChangedEventArgs e)
    {
        ThemeChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for theme change events.
/// </summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous theme.
    /// </summary>
    public AppTheme PreviousTheme { get; }

    /// <summary>
    /// Gets the new theme.
    /// </summary>
    public AppTheme NewTheme { get; }

    /// <summary>
    /// Initializes a new instance of the ThemeChangedEventArgs class.
    /// </summary>
    /// <param name="previousTheme">The previous theme.</param>
    /// <param name="newTheme">The new theme.</param>
    public ThemeChangedEventArgs(AppTheme previousTheme, AppTheme newTheme)
    {
        PreviousTheme = previousTheme;
        NewTheme = newTheme;
    }
}
