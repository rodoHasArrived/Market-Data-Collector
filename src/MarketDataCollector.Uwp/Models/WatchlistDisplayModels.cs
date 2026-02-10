using System;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for watchlist items in the UI.
/// </summary>
public sealed class WatchlistDisplayItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private string _symbol = string.Empty;
    private string _notes = string.Empty;
    private bool _isFavorite;
    private string _favoriteIcon = "\uE734";
    private SolidColorBrush _favoriteColor = new(Microsoft.UI.Colors.Gray);
    private bool _isStreaming;
    private string _statusText = "Idle";
    private SolidColorBrush _statusColor = new(Microsoft.UI.Colors.Gray);
    private string _eventRateText = "0";
    private long _eventCount;
    private double _healthScore = 100;
    private string _healthText = "100%";
    private SolidColorBrush _healthColor = new(Microsoft.UI.Colors.LimeGreen);
    private string _healthIcon = "\uE73E";
    private string _lastEventText = "No data";
    private DateTime _addedAt;

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public string FavoriteIcon
    {
        get => _favoriteIcon;
        set => SetProperty(ref _favoriteIcon, value);
    }

    public SolidColorBrush FavoriteColor
    {
        get => _favoriteColor;
        set => SetProperty(ref _favoriteColor, value);
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetProperty(ref _isStreaming, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public SolidColorBrush StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public string EventRateText
    {
        get => _eventRateText;
        set => SetProperty(ref _eventRateText, value);
    }

    public long EventCount
    {
        get => _eventCount;
        set => SetProperty(ref _eventCount, value);
    }

    public double HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    public string HealthText
    {
        get => _healthText;
        set => SetProperty(ref _healthText, value);
    }

    public SolidColorBrush HealthColor
    {
        get => _healthColor;
        set => SetProperty(ref _healthColor, value);
    }

    public string HealthIcon
    {
        get => _healthIcon;
        set => SetProperty(ref _healthIcon, value);
    }

    public string LastEventText
    {
        get => _lastEventText;
        set => SetProperty(ref _lastEventText, value);
    }

    public DateTime AddedAt
    {
        get => _addedAt;
        set => SetProperty(ref _addedAt, value);
    }
}
