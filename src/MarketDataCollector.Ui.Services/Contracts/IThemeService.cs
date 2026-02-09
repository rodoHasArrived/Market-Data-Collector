namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Application theme options.
/// Shared between WPF and UWP desktop applications.
/// </summary>
public enum AppTheme
{
    Light,
    Dark,
    System
}

/// <summary>
/// Interface for managing application themes.
/// Shared between WPF and UWP desktop applications.
/// Part of C1 improvement (WPF/UWP service deduplication).
/// </summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    event EventHandler<AppTheme>? ThemeChanged;

    void SetTheme(AppTheme theme);
    AppTheme GetSystemTheme();
}
