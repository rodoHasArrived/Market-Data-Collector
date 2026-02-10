using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Views;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display wrapper for credential information with UI-specific properties.
/// Uses shared SettingsBrushes to avoid duplicate brush allocations.
/// </summary>
public sealed class CredentialDisplayInfo
{
    private readonly CredentialInfo _credential;

    public CredentialDisplayInfo(CredentialInfo credential)
    {
        _credential = credential;
    }

    public string Name => _credential.Name;
    public string Resource => _credential.Resource;
    public string Status => _credential.Status;
    public string ExpirationDisplay => _credential.ExpirationDisplay;
    public string LastAuthDisplay => _credential.LastAuthDisplay;

    public SolidColorBrush TestStatusColor => _credential.TestStatus switch
    {
        CredentialTestStatus.Success => SettingsBrushes.Green,
        CredentialTestStatus.Failed => SettingsBrushes.Red,
        CredentialTestStatus.Expired => SettingsBrushes.Red,
        CredentialTestStatus.Testing => SettingsBrushes.Blue,
        _ => SettingsBrushes.Gray
    };

    public SolidColorBrush ExpirationColor
    {
        get
        {
            if (_credential.IsExpired) return SettingsBrushes.Red;
            if (_credential.IsExpiringSoon) return SettingsBrushes.Yellow;
            return SettingsBrushes.Gray;
        }
    }

    public string TypeBadge => _credential.CredentialType switch
    {
        CredentialType.OAuth2Token => "OAuth",
        CredentialType.ApiKeyWithSecret => "API Key",
        CredentialType.BearerToken => "Bearer",
        _ => "Key"
    };

    public SolidColorBrush TypeBadgeColor => _credential.CredentialType switch
    {
        CredentialType.OAuth2Token => SettingsBrushes.Purple,
        CredentialType.ApiKeyWithSecret => SettingsBrushes.Blue,
        _ => SettingsBrushes.Gray
    };

    public Visibility TypeBadgeVisibility =>
        _credential.CredentialType == CredentialType.OAuth2Token ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasMetadata =>
        _credential.ExpiresAt.HasValue || _credential.LastAuthenticatedAt.HasValue
            ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasExpiration =>
        _credential.ExpiresAt.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasLastAuth =>
        _credential.LastAuthenticatedAt.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RefreshButtonVisibility =>
        _credential.CredentialType == CredentialType.OAuth2Token && _credential.CanAutoRefresh
            ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// Recent activity item.
/// </summary>
public sealed class ActivityItem
{
    public string Icon { get; set; } = string.Empty;
    public SolidColorBrush IconColor { get; set; } = new(Color.FromArgb(255, 160, 160, 160));
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
