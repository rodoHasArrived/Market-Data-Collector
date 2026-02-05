using System.Windows.Media;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Centralized registry for cached SolidColorBrush instances.
/// Prevents repeated allocations of the same brush colors across the application.
/// All brushes are static readonly to ensure single allocation and thread safety.
/// </summary>
public static class BrushRegistry
{
    #region Status Brushes

    /// <summary>Success/Active state (green).</summary>
    public static readonly SolidColorBrush Success = new(Color.FromArgb(255, 72, 187, 120));

    /// <summary>Warning state (orange).</summary>
    public static readonly SolidColorBrush Warning = new(Color.FromArgb(255, 237, 137, 54));

    /// <summary>Error/Danger state (red).</summary>
    public static readonly SolidColorBrush Error = new(Color.FromArgb(255, 245, 101, 101));

    /// <summary>Informational state (blue).</summary>
    public static readonly SolidColorBrush Info = new(Color.FromArgb(255, 88, 166, 255));

    /// <summary>Inactive/Disabled state (gray).</summary>
    public static readonly SolidColorBrush Inactive = new(Color.FromArgb(255, 160, 174, 192));

    /// <summary>Critical severity (bright red).</summary>
    public static readonly SolidColorBrush Critical = new(Color.FromArgb(255, 248, 81, 73));

    /// <summary>Warning events (amber/gold).</summary>
    public static readonly SolidColorBrush WarningEvent = new(Color.FromArgb(255, 210, 153, 34));

    #endregion

    #region Chart/Visualization Brushes

    /// <summary>Primary chart line color (blue).</summary>
    public static readonly SolidColorBrush ChartPrimary = new(Color.FromArgb(255, 66, 153, 225));

    /// <summary>Secondary chart line color (purple).</summary>
    public static readonly SolidColorBrush ChartSecondary = new(Color.FromArgb(255, 159, 122, 234));

    /// <summary>Tertiary chart line color (teal).</summary>
    public static readonly SolidColorBrush ChartTertiary = new(Color.FromArgb(255, 56, 178, 172));

    /// <summary>Chart positive/up color (green).</summary>
    public static readonly SolidColorBrush ChartPositive = new(Color.FromArgb(255, 72, 187, 120));

    /// <summary>Chart negative/down color (red).</summary>
    public static readonly SolidColorBrush ChartNegative = new(Color.FromArgb(255, 245, 101, 101));

    #endregion

    #region Provider Status Brushes

    /// <summary>Provider connected status (green).</summary>
    public static readonly SolidColorBrush ProviderConnected = Success;

    /// <summary>Provider connecting status (blue).</summary>
    public static readonly SolidColorBrush ProviderConnecting = Info;

    /// <summary>Provider disconnected status (gray).</summary>
    public static readonly SolidColorBrush ProviderDisconnected = Inactive;

    /// <summary>Provider error status (red).</summary>
    public static readonly SolidColorBrush ProviderError = Error;

    #endregion

    #region Data Quality Brushes

    /// <summary>Data quality excellent (bright green).</summary>
    public static readonly SolidColorBrush QualityExcellent = new(Color.FromArgb(255, 56, 161, 105));

    /// <summary>Data quality good (green).</summary>
    public static readonly SolidColorBrush QualityGood = Success;

    /// <summary>Data quality fair (yellow).</summary>
    public static readonly SolidColorBrush QualityFair = new(Color.FromArgb(255, 236, 201, 75));

    /// <summary>Data quality poor (orange).</summary>
    public static readonly SolidColorBrush QualityPoor = Warning;

    /// <summary>Data quality critical (red).</summary>
    public static readonly SolidColorBrush QualityCritical = Error;

    #endregion

    #region UI Element Brushes

    /// <summary>Accent color for interactive elements.</summary>
    public static readonly SolidColorBrush Accent = new(Color.FromArgb(255, 99, 102, 241));

    /// <summary>Subtle background color.</summary>
    public static readonly SolidColorBrush SubtleBackground = new(Color.FromArgb(255, 45, 55, 72));

    /// <summary>Card background color.</summary>
    public static readonly SolidColorBrush CardBackground = new(Color.FromArgb(255, 26, 32, 44));

    /// <summary>Muted text color.</summary>
    public static readonly SolidColorBrush MutedText = new(Color.FromArgb(255, 160, 174, 192));

    /// <summary>Light text on dark background.</summary>
    public static readonly SolidColorBrush LightText = new(Color.FromArgb(255, 226, 232, 240));

    #endregion

    #region Semi-Transparent Status Backgrounds

    /// <summary>Semi-transparent success background (for progress indicators).</summary>
    public static readonly SolidColorBrush SuccessBackground = new(Color.FromArgb(40, 72, 187, 120));

    /// <summary>Semi-transparent warning background (for progress indicators).</summary>
    public static readonly SolidColorBrush WarningBackground = new(Color.FromArgb(40, 237, 137, 54));

    /// <summary>Semi-transparent error background (for progress indicators).</summary>
    public static readonly SolidColorBrush ErrorBackground = new(Color.FromArgb(40, 245, 101, 101));

    /// <summary>Semi-transparent info background (for progress indicators).</summary>
    public static readonly SolidColorBrush InfoBackground = new(Color.FromArgb(40, 88, 166, 255));

    #endregion

    #region Notification Type Brushes

    /// <summary>Gets the brush for a notification type.</summary>
    public static SolidColorBrush GetNotificationBrush(NotificationType type) => type switch
    {
        NotificationType.Success => Success,
        NotificationType.Warning => Warning,
        NotificationType.Error => Error,
        _ => Info
    };

    #endregion

    #region Severity Brushes

    /// <summary>Gets the brush for an integrity severity level.</summary>
    public static SolidColorBrush GetSeverityBrush(IntegritySeverity severity) => severity switch
    {
        IntegritySeverity.Critical => Critical,
        IntegritySeverity.Warning => WarningEvent,
        _ => Info
    };

    /// <summary>Gets the color for an integrity severity level.</summary>
    public static Color GetSeverityColor(IntegritySeverity severity) => severity switch
    {
        IntegritySeverity.Critical => Color.FromArgb(255, 248, 81, 73),
        IntegritySeverity.Warning => Color.FromArgb(255, 210, 153, 34),
        _ => Color.FromArgb(255, 88, 166, 255)
    };

    #endregion

    #region Connection State Brushes

    /// <summary>Gets the brush for a connection state.</summary>
    public static SolidColorBrush GetConnectionStateBrush(ConnectionState state) => state switch
    {
        ConnectionState.Connected => Success,
        ConnectionState.Connecting or ConnectionState.Reconnecting => Info,
        ConnectionState.Disconnected => Inactive,
        ConnectionState.Error => Error,
        _ => Inactive
    };

    #endregion

    #region Progress Brushes

    /// <summary>Gets the brush for a progress percentage (0-100).</summary>
    public static SolidColorBrush GetProgressBrush(double percentage) => percentage switch
    {
        >= 90 => Success,
        >= 70 => Info,
        >= 50 => Warning,
        _ => Error
    };

    #endregion

    #region Latency Brushes

    /// <summary>Gets the brush for a latency value in milliseconds.</summary>
    public static SolidColorBrush GetLatencyBrush(int latencyMs) => latencyMs switch
    {
        < 20 => Success,
        < 50 => Warning,
        _ => Error
    };

    #endregion

    #region Stream Status Brushes

    /// <summary>Gets the brush for stream status based on active state and collector state.</summary>
    public static SolidColorBrush GetStreamStatusBrush(bool isStreamActive, bool isCollectorRunning, bool isCollectorPaused)
    {
        if (isStreamActive && isCollectorRunning && !isCollectorPaused)
            return Success;
        if (isStreamActive && isCollectorPaused)
            return Warning;
        return Inactive;
    }

    #endregion
}
