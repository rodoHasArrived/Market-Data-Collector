// =============================================================================
// TypeForwards.cs - Phase 2 type forwarding
// =============================================================================
// Re-exports types that were extracted from WPF services into shared base classes
// in MarketDataCollector.Ui.Services.Services namespace.
// This ensures existing WPF view code referencing WpfServices.NotificationType,
// WpfServices.LogLevel, etc. continues to resolve without per-file changes.
// =============================================================================

// Notification types (NotificationType is in the root Ui.Services namespace;
// the rest are in Ui.Services.Services from NotificationServiceBase)
global using NotificationType = MarketDataCollector.Ui.Services.NotificationType;
global using NotificationSettings = MarketDataCollector.Ui.Services.Services.NotificationSettings;
global using NotificationHistoryItem = MarketDataCollector.Ui.Services.Services.NotificationHistoryItem;
global using NotificationEventArgs = MarketDataCollector.Ui.Services.Services.NotificationEventArgs;

// Logging types (from LoggingServiceBase)
global using LogLevel = MarketDataCollector.Ui.Services.Services.LogLevel;
global using LogEntryEventArgs = MarketDataCollector.Ui.Services.Services.LogEntryEventArgs;

// Backend service types (from BackendServiceManagerBase)
global using BackendServiceStatus = MarketDataCollector.Ui.Services.Services.BackendServiceStatus;
global using BackendServiceOperationResult = MarketDataCollector.Ui.Services.Services.BackendServiceOperationResult;
global using BackendInstallationInfo = MarketDataCollector.Ui.Services.Services.BackendInstallationInfo;
global using BackendRuntimeInfo = MarketDataCollector.Ui.Services.Services.BackendRuntimeInfo;

// Status types (from StatusServiceBase)
global using StatusChangedEventArgs = MarketDataCollector.Ui.Services.Services.StatusChangedEventArgs;
global using LiveStatusEventArgs = MarketDataCollector.Ui.Services.Services.LiveStatusEventArgs;
global using SimpleStatus = MarketDataCollector.Ui.Services.Services.SimpleStatus;
global using StatusProviderInfo = MarketDataCollector.Ui.Services.Services.StatusProviderInfo;
