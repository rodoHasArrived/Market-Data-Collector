namespace MarketDataCollector.Wpf.Services;

public enum AppTheme
{
    Light,
    Dark,
    System
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    
    event EventHandler<AppTheme>? ThemeChanged;
    
    void SetTheme(AppTheme theme);
    AppTheme GetSystemTheme();
}
