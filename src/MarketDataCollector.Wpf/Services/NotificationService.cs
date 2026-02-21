using System;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF platform-specific notification service.
/// Extends <see cref="NotificationServiceBase"/> implementing the
/// <see cref="MarketDataCollector.Ui.Services.Contracts.INotificationService"/> contract.
/// Part of Phase 2 service extraction.
/// </summary>
public sealed class NotificationService : NotificationServiceBase, MarketDataCollector.Ui.Services.Contracts.INotificationService
{
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());

    public static NotificationService Instance => _instance.Value;

    private NotificationService()
    {
    }
}
