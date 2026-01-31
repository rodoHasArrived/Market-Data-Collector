# UWP Desktop Release Checklist (Desktop-Ready Scope)

This checklist defines the minimal **desktop-ready** scope for the UWP app release. Each item includes explicit acceptance criteria and is tagged as **must-ship** or **post-ship**. Links point to implementation locations or the refinement backlog so expectations are concrete.

## Desktop-Ready Scope

### Must-Ship

| Area | Acceptance Criteria | Links |
| --- | --- | --- |
| **Notification handling** | - Connection loss, reconnect attempts, and recovery trigger Windows toast notifications.<br>- Backfill completion and data gap warnings trigger notifications when enabled in settings.<br>- Notifications are suppressed during quiet hours and logged in history for review. | [NotificationService](../../src/MarketDataCollector.Uwp/Services/NotificationService.cs), [NotificationCenterPage](../../src/MarketDataCollector.Uwp/Views/NotificationCenterPage.xaml.cs), [MainPage notification routing](../../src/MarketDataCollector.Uwp/Views/MainPage.xaml.cs) |
| **Reconnection behavior** | - Automatic reconnection with exponential backoff is enabled and configurable (max attempts, base delay).<br>- Reconnection attempts and outcomes update UI status and activity feed.<br>- Manual pause/resume of auto-reconnect is supported for maintenance windows. | [ConnectionService](../../src/MarketDataCollector.Uwp/Services/ConnectionService.cs), [MainPage status handlers](../../src/MarketDataCollector.Uwp/Views/MainPage.xaml.cs) |
| **Integrity visibility** | - Dashboard displays integrity counters and recent integrity events.<br>- Users can expand/collapse integrity details, acknowledge alerts, and export an integrity report.<br>- Integrity events are recorded with severity and surfaced in the notification center. | [DashboardPage UI](../../src/MarketDataCollector.Uwp/Views/DashboardPage.xaml), [DashboardPage code-behind](../../src/MarketDataCollector.Uwp/Views/DashboardPage.xaml.cs), [IntegrityEventsService](../../src/MarketDataCollector.Uwp/Services/IntegrityEventsService.cs) |

### Post-Ship

| Area | Acceptance Criteria | Links |
| --- | --- | --- |
| **System tray + advanced notification routing** | - System tray icon supports quick actions and notification badges.<br>- Action Center deep links provide direct navigation to remediation pages. | [Feature refinements backlog](../../src/MarketDataCollector.Uwp/FEATURE_REFINEMENTS.md#1-real-time-notification-system) |
| **Expanded archive health reporting** | - Archive health page supports scheduled full verification and trend reporting across multiple sessions.<br>- Exportable compliance report includes integrity summary and verification metadata. | [Feature refinements backlog](../../src/MarketDataCollector.Uwp/FEATURE_REFINEMENTS.md#57-archive-verification--integrity-dashboard) |

## Release Notes Gate

A release is considered **desktop-ready** only when all **must-ship** items meet their acceptance criteria. Post-ship items are tracked in the refinement backlog and can be scheduled independently.
