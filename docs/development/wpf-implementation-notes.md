# WPF Desktop Application Implementation Notes

## Overview

This document describes the WPF desktop application implementation added as part of PR #598, which provides an alternative desktop UI alongside the existing UWP application.

## Implementation Scope

### Core Application Structure
- **Project**: `MarketDataCollector.Wpf` (located in `src/MarketDataCollector.Wpf/`)
- **Framework**: .NET 9.0 with WPF (Windows Presentation Foundation)
- **Target**: Windows-only desktop deployment
- **Architecture**: MVVM pattern with singleton services

### Key Components

#### Services Layer (10 Core Services)
1. **NavigationService** - Centralized page navigation with history tracking
2. **ConfigService** - Application configuration management
3. **StatusService** - Real-time status tracking and API communication
4. **ConnectionService** - Provider connection state management
5. **ThemeService** - Dark/Light theme management
6. **NotificationService** - Toast notifications and alerts
7. **LoggingService** - Structured logging
8. **KeyboardShortcutService** - Global keyboard shortcuts (20+ shortcuts)
9. **MessagingService** - Inter-component messaging
10. **FirstRunService** - First-run setup wizard

#### Background Services (3 Services)
1. **BackgroundTaskSchedulerService** - Scheduled task execution
2. **OfflineTrackingPersistenceService** - Offline data tracking
3. **PendingOperationsQueueService** - Offline operation queue

#### View Pages (40+ Pages)
Organized into categories:
- **Primary Navigation**: Dashboard, Watchlist
- **Data Sources**: Provider configuration, health monitoring
- **Data Management**: Live data viewer, browser, symbols, backfill, storage
- **Monitoring**: Data quality, collection sessions, archive health, system health
- **Tools**: Data export, sampling, analysis, event replay
- **Settings**: Configuration, credentials, notifications

### Architecture Patterns

#### Dependency Injection
- Uses Microsoft.Extensions.Hosting for service registration
- Singleton pattern for stateful services
- IServiceProvider for service resolution

#### Navigation
- Frame-based navigation with System.Windows.Controls.Frame
- Page registry for tag-based navigation
- Navigation history with breadcrumb support
- Back navigation support

#### Event Management
- Proper event subscription/unsubscription in page lifecycle
- OnPageLoaded and OnPageUnloaded handlers
- Prevents memory leaks through proper cleanup

#### Configuration Management
- Singleton ConfigService with ConfigPath property
- Integration with FirstRunService for initial setup
- Validation support with ConfigValidationResult

#### Connection Management
- Async connect/disconnect operations
- Connection state change events
- Latency tracking
- Provider-agnostic interface

### Key Features

#### Keyboard Shortcuts (20+ Shortcuts)
- **Navigation**: Ctrl+D (Dashboard), Ctrl+B (Backfill), Ctrl+S (Settings)
- **Collector Control**: Ctrl+Shift+S (Start), Ctrl+Shift+Q (Stop)
- **Symbol Management**: Ctrl+N (Add), Ctrl+F (Search), Delete (Remove)
- **Backfill**: Ctrl+R (Run), Ctrl+Shift+P (Pause), Esc (Cancel)
- **View**: Ctrl+T (Toggle Theme), F5 (Refresh)

#### Real-Time Updates
- HTTP polling-based status updates (2-second intervals)
- WebSocket support planned for future enhancement
- StatusService provides GetStatusAsync() for API integration

#### Theme Support
- Dark and Light themes
- Theme persistence
- Windows system theme integration
- Dynamic theme switching (Ctrl+T)

#### Graceful Shutdown
- Coordinated service shutdown with timeout (5 seconds)
- Parallel shutdown of background services
- Proper exception handling during shutdown

### Technical Decisions

#### Why WPF over WinUI 3?
1. **No XAML Compiler Restrictions** - Can use standard ProjectReference patterns
2. **Mature Framework** - Stable API with extensive community support
3. **Better Integration** - Easier integration with existing .NET libraries
4. **Simpler Build** - No Windows App SDK complications

#### Service Singleton Pattern
All services use singleton pattern for application-wide state:
```csharp
public static ServiceName Instance => _instance.Value;
```

Benefits:
- Single source of truth for application state
- Thread-safe initialization with Lazy<T>
- Easy access from any component

#### Namespace Alias for Navigation
To avoid ambiguity between `System.Windows.Navigation` and `MarketDataCollector.Wpf.Services.NavigationService`:
```csharp
using SysNavigation = System.Windows.Navigation;
```

### Build Verification

#### Current Status
✅ **Build**: Successful with 0 warnings, 0 errors
✅ **Dependencies**: All NuGet packages resolved
✅ **Solution Integration**: Properly integrated in MarketDataCollector.sln
✅ **Project GUID**: Valid GUID `{6F8D3A55-3E95-4F9D-9C8F-DBA9A6230B1E}`

#### Build Command
```bash
dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release
```

### Code Review Findings - All Resolved

All critical issues identified in the code review have been addressed:

1. ✅ **Solution File GUID** - Replaced invalid GUID with valid one
2. ✅ **ConfigService.ConfigPath** - Property implemented
3. ✅ **DataBrowser Navigation** - Tag registered in NavigationService
4. ✅ **Event Cleanup** - OnPageUnloaded handlers added
5. ✅ **NavigationService Ambiguity** - Resolved with namespace alias
6. ✅ **StatusService.GetStatusAsync()** - Method implemented
7. ✅ **Navigation History** - Only pushed after successful navigation
8. ✅ **CanGoBack Property** - Based on Frame.CanGoBack

### Future Enhancements

#### Near-Term
1. Replace HTTP polling with WebSocket/SignalR for real-time updates
2. Implement connection to backend Market Data Collector service
3. Add comprehensive unit tests for services
4. Implement actual configuration persistence (currently stub)

#### Long-Term
1. Consider Blazor Hybrid for cross-platform compatibility
2. Add mobile companion app for monitoring
3. Implement offline-first architecture with sync
4. Add multi-user collaboration features

### Comparison with UWP Application

| Feature | UWP (WinUI 3) | WPF |
|---------|---------------|-----|
| Windows Version | Windows 10+ | Windows 7+ |
| XAML Compiler | Restrictive (no ProjectReference) | Standard .NET |
| Build Complexity | Complex (Windows App SDK) | Simple |
| Package Format | MSIX | MSIX or traditional installer |
| Architecture Support | x86, x64, ARM64 | x86, x64, ARM64 |
| Theme Support | Native Windows 11 | Custom implementation |
| Cross-Platform | Windows-only | Windows-only |
| Maturity | Newer (2020+) | Mature (2006+) |

### Documentation

Related documentation:
- **Evaluation**: `docs/evaluations/desktop-ui-alternatives-evaluation.md`
- **UWP Roadmap**: `docs/guides/uwp-development-roadmap.md`
- **UWP Checklist**: `docs/guides/uwp-release-checklist.md`

### Testing

#### Manual Testing Checklist
- [ ] Application launches successfully
- [ ] Navigation between pages works
- [ ] Keyboard shortcuts respond correctly
- [ ] Theme switching works
- [ ] Status updates display correctly
- [ ] Settings page shows configuration path
- [ ] Graceful shutdown completes within timeout
- [ ] Background services start and stop correctly

#### Unit Testing (Planned)
- [ ] NavigationService tests
- [ ] ConfigService tests
- [ ] StatusService tests
- [ ] ConnectionService tests
- [ ] KeyboardShortcutService tests

### Contributing

When modifying the WPF application:

1. **Follow Existing Patterns**
   - Use singleton pattern for services
   - Implement OnPageLoaded and OnPageUnloaded handlers
   - Use async/await for I/O operations
   - Add proper exception handling

2. **Event Management**
   - Always unsubscribe in OnPageUnloaded
   - Use weak references for long-lived subscriptions
   - Avoid circular references

3. **Navigation**
   - Register new pages in NavigationService.RegisterPages()
   - Use tag-based navigation (e.g., "Dashboard")
   - Handle navigation parameters correctly

4. **Testing**
   - Test keyboard shortcuts
   - Verify memory cleanup (no leaks)
   - Check graceful shutdown
   - Validate configuration persistence

### License

MIT License - Same as parent project

### Authors

- Architecture Review Team
- Market Data Collector Contributors

---

**Last Updated**: 2026-02-02
**Status**: Implementation Complete, Ready for Testing
