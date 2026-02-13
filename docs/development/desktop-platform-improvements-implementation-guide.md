# Desktop Platform Development Improvements - Implementation Guide

**Last Updated:** 2026-02-13
**Status:** Active

## Executive Summary

This document provides the implementation guide for desktop platform development improvements. For a condensed view see the [Executive Summary](desktop-improvements-executive-summary.md). For the original proposal see the [High-Value Improvements](desktop-devex-high-value-improvements.md) (superseded).

### Current State Assessment (February 2026)

✅ **Completed Infrastructure** (Priority 1 items from original plan):
- Desktop development bootstrap script (`scripts/dev/desktop-dev.ps1`)
- Focused desktop Make targets (`build-wpf`, `build-uwp`, `test-desktop-services`)
- UWP XAML diagnostics helper (`scripts/dev/diagnose-uwp-xaml.ps1`)
- Desktop support policy (`docs/development/policies/desktop-support-policy.md`)
- Desktop PR checklist template (`.github/pull_request_template_desktop.md`)
- Desktop workflow documentation (`docs/development/desktop-dev-workflow.md`)

❌ **Critical Gaps Identified**:
1. **No unit tests** for 30+ desktop services (Navigation, Config, Status, Connection, etc.)
2. **100% code duplication** of services between WPF and UWP
3. **No test fixtures** for UI development without backend dependency
4. **Missing architecture diagram** for desktop layer boundaries
5. **No DI container** in WPF (uses manual singletons)
6. **Inconsistent patterns** between WPF and UWP implementations

---

## Priority 1: Desktop Services Unit Test Baseline

### Problem
Despite having 30+ services shared between WPF and UWP, there are **zero unit tests** for desktop-specific services. Changes to services like `NavigationService`, `ConfigService`, and `StatusService` are currently validated only through manual testing, increasing regression risk.

### Solution: Create MarketDataCollector.Ui.Tests Project

#### Step 1: Create Test Project

```bash
cd tests/
dotnet new xunit -n MarketDataCollector.Ui.Tests
cd MarketDataCollector.Ui.Tests
```

#### Step 2: Add Required Dependencies

Edit `MarketDataCollector.Ui.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/MarketDataCollector.Ui.Services/MarketDataCollector.Ui.Services.csproj" />
  </ItemGroup>
</Project>
```

#### Step 3: Add to Solution

```bash
cd ../..
dotnet sln add tests/MarketDataCollector.Ui.Tests/MarketDataCollector.Ui.Tests.csproj
```

#### Step 4: Create Example Tests

**Tests/Services/ApiClientServiceTests.cs**:

```csharp
using FluentAssertions;
using MarketDataCollector.Ui.Services.Services;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using Xunit;

namespace MarketDataCollector.Ui.Tests.Services;

public sealed class ApiClientServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;

    public ApiClientServiceTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
    }

    [Fact]
    public async Task GetStatusAsync_WhenServerReturns200_ReturnsStatusResponse()
    {
        // Arrange
        var expectedJson = """
        {
            "status": "running",
            "uptime": "00:15:30",
            "eventsProcessed": 1000,
            "providerStatus": "connected"
        }
        """;

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedJson)
            });

        var service = new ApiClientService(_httpClient);

        // Act
        var result = await service.GetStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("running");
        result.EventsProcessed.Should().Be(1000);
    }

    [Fact]
    public async Task GetStatusAsync_WhenServerUnavailable_ThrowsHttpRequestException()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = new ApiClientService(_httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await service.GetStatusAsync()
        );
    }
}
```

**Tests/Services/FormValidationServiceTests.cs**:

```csharp
using FluentAssertions;
using MarketDataCollector.Ui.Services.Services;
using Xunit;

namespace MarketDataCollector.Ui.Tests.Services;

public sealed class FormValidationServiceTests
{
    [Theory]
    [InlineData("SPY", true)]
    [InlineData("AAPL", true)]
    [InlineData("MSFT", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("SP Y", false)]
    [InlineData("123", false)]
    public void ValidateSymbol_ValidatesSymbolFormat(string symbol, bool expectedValid)
    {
        // Act
        var result = FormValidationRules.ValidateSymbol(symbol);

        // Assert
        result.IsValid.Should().Be(expectedValid);
        if (!expectedValid)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("2024-01-01", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void ValidateDate_ValidatesDateFormat(string dateStr, bool expectedValid)
    {
        // Act
        var result = FormValidationRules.ValidateDate(dateStr);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("config.json", true)]
    [InlineData("C:\\data\\config.json", true)]
    [InlineData("/var/data/config.json", true)]
    [InlineData("", false)]
    [InlineData("con:", false)] // Invalid Windows path
    public void ValidateFilePath_ValidatesPathFormat(string path, bool expectedValid)
    {
        // Act
        var result = FormValidationRules.ValidateFilePath(path);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }
}
```

**Tests/Collections/BoundedObservableCollectionTests.cs**:

```csharp
using FluentAssertions;
using MarketDataCollector.Ui.Services.Collections;
using Xunit;

namespace MarketDataCollector.Ui.Tests.Collections;

public sealed class BoundedObservableCollectionTests
{
    [Fact]
    public void Add_WhenUnderCapacity_AddsItem()
    {
        // Arrange
        var collection = new BoundedObservableCollection<int>(capacity: 5);

        // Act
        collection.Add(1);
        collection.Add(2);
        collection.Add(3);

        // Assert
        collection.Should().HaveCount(3);
        collection.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Add_WhenAtCapacity_RemovesOldestAndAddsNew()
    {
        // Arrange
        var collection = new BoundedObservableCollection<int>(capacity: 3);
        collection.Add(1);
        collection.Add(2);
        collection.Add(3);

        // Act
        collection.Add(4);

        // Assert
        collection.Should().HaveCount(3);
        collection.Should().ContainInOrder(2, 3, 4);
        collection.Should().NotContain(1);
    }

    [Fact]
    public void CollectionChanged_FiresWhenItemAdded()
    {
        // Arrange
        var collection = new BoundedObservableCollection<int>(capacity: 5);
        var eventFired = false;
        collection.CollectionChanged += (_, _) => eventFired = true;

        // Act
        collection.Add(1);

        // Assert
        eventFired.Should().BeTrue();
    }
}
```

#### Step 5: Add to CI Pipeline

Update `.github/workflows/test-matrix.yml` to include desktop tests:

```yaml
- name: Test Desktop Services
  if: runner.os == 'Windows'
  run: dotnet test tests/MarketDataCollector.Ui.Tests --configuration Release --no-build --verbosity normal
```

#### Step 6: Update Makefile

Add test target:

```makefile
test-desktop-unit: ## Run desktop unit tests
	@echo "Running desktop unit tests..."
	dotnet test tests/MarketDataCollector.Ui.Tests --configuration Release --verbosity normal
```

### Expected Outcomes

1. **Faster feedback loop**: Developers can validate service logic in <5 seconds vs launching full UI
2. **Regression prevention**: Services like `FormValidationRules` have 100% coverage
3. **Refactoring confidence**: Services can be safely refactored with test safety net
4. **Documentation**: Tests serve as executable examples of how to use services

### Success Metrics

- [ ] At least 15 unit test files created
- [ ] 60%+ code coverage for `MarketDataCollector.Ui.Services` project
- [ ] All new desktop service PRs include unit tests
- [ ] CI runs desktop tests on every PR

---

## Priority 2: UI Fixture Mode for Offline Development

### Problem
Desktop developers must run the backend collector service (`http://localhost:8080`) to see any data in the UI. This blocks offline development, makes debugging harder, and couples UI work to backend availability.

### Solution: Create FixtureDataService

#### Implementation

**src/MarketDataCollector.Ui.Services/Services/FixtureDataService.cs**:

```csharp
using MarketDataCollector.Contracts.Api;
using System.Text.Json;

namespace MarketDataCollector.Ui.Services.Services;

/// <summary>
/// Provides canned fixture data for UI development without backend dependency.
/// </summary>
public sealed class FixtureDataService
{
    private static readonly Lazy<FixtureDataService> _instance = new(() => new());
    public static FixtureDataService Instance => _instance.Value;

    private FixtureDataService() { }

    public StatusResponse GetMockStatusResponse() => new()
    {
        Status = "running",
        Uptime = "02:15:30",
        EventsProcessed = 45678,
        ProviderStatus = "connected",
        CurrentSymbols = new[] { "SPY", "AAPL", "MSFT", "TSLA" },
        ActiveProvider = "Alpaca",
        ConnectionLatencyMs = 45,
        MemoryUsageMb = 256,
        CpuUsagePercent = 12.5
    };

    public LiveDataUpdate GetMockLiveDataUpdate(string symbol) => new()
    {
        Symbol = symbol,
        LastPrice = 450.25m,
        BidPrice = 450.20m,
        AskPrice = 450.30m,
        Volume = 1_234_567,
        Timestamp = DateTimeOffset.UtcNow
    };

    public BackfillStatusResponse GetMockBackfillStatus() => new()
    {
        IsRunning = true,
        Progress = 67.5,
        CurrentSymbol = "AAPL",
        SymbolsCompleted = 135,
        SymbolsTotal = 200,
        StartTime = DateTimeOffset.UtcNow.AddMinutes(-30),
        EstimatedCompletion = DateTimeOffset.UtcNow.AddMinutes(15)
    };

    public QualityMetricsResponse GetMockQualityMetrics() => new()
    {
        CompletenessScore = 98.7,
        GapCount = 3,
        SequenceErrors = 0,
        AverageLatencyMs = 42,
        P99LatencyMs = 125,
        LastUpdated = DateTimeOffset.UtcNow
    };
}
```

#### WPF Integration

Update `StatusService` to support fixture mode:

```csharp
public sealed class StatusService
{
    private static readonly Lazy<StatusService> _instance = new(() => new());
    public static StatusService Instance => _instance.Value;

    // Add fixture mode flag
    public bool UseFixtureMode { get; set; }

    public async Task<StatusResponse> GetStatusAsync()
    {
        if (UseFixtureMode)
        {
            await Task.Delay(100); // Simulate network delay
            return FixtureDataService.Instance.GetMockStatusResponse();
        }

        return await ApiClientService.Instance.GetStatusAsync();
    }
}
```

#### Enable Fixture Mode

Add command-line argument or environment variable:

```csharp
// In App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // Enable fixture mode for development
    if (e.Args.Contains("--fixture") || 
        Environment.GetEnvironmentVariable("MDC_FIXTURE_MODE") == "1")
    {
        StatusService.Instance.UseFixtureMode = true;
        // ... enable for other services
    }
}
```

#### Usage

```bash
# Run WPF with fixture data
dotnet run --project src/MarketDataCollector.Wpf -- --fixture

# Or set environment variable
$env:MDC_FIXTURE_MODE = "1"
dotnet run --project src/MarketDataCollector.Wpf
```

### Expected Outcomes

1. **Offline development**: Work on UI without running backend
2. **Deterministic debugging**: Same fixture data every time
3. **Faster iteration**: No waiting for real backend responses
4. **Demo mode**: Show UI features without live data

---

## Priority 3: Desktop Architecture Diagram

### Problem
No visual reference for desktop layer boundaries, making it easy for developers to introduce unwanted coupling.

### Solution: Create Architecture Diagram

Create `docs/architecture/desktop-layers.md`:

```markdown
# Desktop Application Architecture

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Platform UI Layer                        │
│  ┌──────────────────┐              ┌──────────────────┐    │
│  │   WPF Desktop    │              │   UWP Desktop    │    │
│  │   (Primary)      │              │   (Legacy)       │    │
│  │                  │              │                  │    │
│  │ - Views/Pages    │              │ - Views/Pages    │    │
│  │ - App.xaml       │              │ - App.xaml       │    │
│  │ - MainWindow     │              │ - MainWindow     │    │
│  └─────────┬────────┘              └────────┬─────────┘    │
│            │                                 │              │
│            └─────────────┬───────────────────┘              │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│              Platform-Specific Services Layer               │
│  ┌───────────────────────▼──────────────────────────────┐  │
│  │                                                       │  │
│  │  NavigationService  ThemeService  StorageService     │  │
│  │  (Platform-specific implementations)                 │  │
│  │                                                       │  │
│  └───────────────────────┬──────────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│              Shared UI Services Layer                       │
│  ┌───────────────────────▼──────────────────────────────┐  │
│  │    MarketDataCollector.Ui.Services                   │  │
│  │                                                       │  │
│  │  - ApiClientService                                  │  │
│  │  - BackfillService                                   │  │
│  │  - ChartingService                                   │  │
│  │  - SystemHealthService                               │  │
│  │  - 50+ shared services                               │  │
│  │                                                       │  │
│  └───────────────────────┬──────────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│              Contracts & Models Layer                       │
│  ┌───────────────────────▼──────────────────────────────┐  │
│  │    MarketDataCollector.Contracts                     │  │
│  │                                                       │  │
│  │  - StatusResponse                                    │  │
│  │  - BackfillRequest/Response                          │  │
│  │  - ProviderCatalog                                   │  │
│  │  - API models                                        │  │
│  │                                                       │  │
│  └───────────────────────┬──────────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────┘
                           │
                           ▼
                    Backend HTTP API
                  (http://localhost:8080)
```

## Dependency Rules

### ✅ Allowed Dependencies

1. **Platform UI → Platform Services**: WPF/UWP can use their own services
2. **Platform UI → Shared Services**: Both platforms can use `Ui.Services`
3. **Platform Services → Shared Services**: Platform services can delegate to shared logic
4. **Shared Services → Contracts**: All services use shared contracts
5. **All Layers → Contracts**: Everyone can reference contracts

### ❌ Forbidden Dependencies

1. **Shared Services → Platform UI**: Shared code CANNOT reference WPF/UWP
2. **Shared Services → Platform Services**: Shared code CANNOT reference platform-specific services
3. **WPF → UWP** or **UWP → WPF**: Platforms CANNOT reference each other
4. **Contracts → Any other layer**: Contracts must remain pure POCOs

## Service Duplication Strategy

### Current State (Code Duplication)

Both WPF and UWP have nearly identical implementations of:
- NavigationService
- ThemeService
- StorageService
- ConfigService
- LoggingService
- and 25+ more...

### Target State (Shared Base with Platform Adapters)

```csharp
// Shared base in Ui.Services
public abstract class NavigationServiceBase
{
    protected abstract void NavigateCore(string pageTag);
    
    public void Navigate(string pageTag)
    {
        ValidatePageTag(pageTag);
        NavigateCore(pageTag);
    }
}

// WPF-specific implementation
public sealed class WpfNavigationService : NavigationServiceBase
{
    protected override void NavigateCore(string pageTag)
    {
        // WPF-specific Frame navigation
    }
}

// UWP-specific implementation
public sealed class UwpNavigationService : NavigationServiceBase
{
    protected override void NavigateCore(string pageTag)
    {
        // UWP-specific Frame navigation
    }
}
```

This reduces duplication from 100% to ~20% (only platform-specific parts).
```

---

## Priority 4: Dependency Injection Modernization

### Problem

**WPF**: Uses manual singleton pattern everywhere, making testing difficult
**UWP**: Has `ServiceLocator` but inconsistently used

### Solution: Standardize on Microsoft.Extensions.DependencyInjection

#### WPF Implementation

Update `App.xaml.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MarketDataCollector.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register services
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IConfigService, ConfigService>();
                services.AddSingleton<IStatusService, StatusService>();
                services.AddSingleton<IConnectionService, ConnectionService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<INotificationService, NotificationService>();
                
                // Register shared services
                services.AddSingleton<ApiClientService>();
                services.AddSingleton<BackfillService>();
                
                // Register main window
                services.AddSingleton<MainWindow>();
            })
            .Build();

        // Start the host
        _host.Start();

        // Show main window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
```

Update `MainWindow.xaml.cs` to use DI:

```csharp
public partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly IStatusService _statusService;

    // Constructor injection
    public MainWindow(
        INavigationService navigationService,
        IStatusService statusService)
    {
        _navigationService = navigationService;
        _statusService = statusService;
        
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Navigate("Dashboard");
    }
}
```

#### Benefits

1. **Testability**: Services can be mocked in tests
2. **Lifetime management**: Automatic disposal
3. **Configuration**: Can inject different implementations
4. **Consistency**: Same pattern as backend services

---

## Priority 5: Code Duplication Elimination Roadmap

### Analysis

Current duplication count:
- **31 services** duplicated between WPF and UWP
- **~15,000 lines** of duplicated code
- **2x maintenance burden** for every service change

### Consolidation Strategy

#### Phase 1: Extract Shared Interfaces (Week 1)

Move all service interfaces to `MarketDataCollector.Ui.Services/Contracts/`:

```
Contracts/
├── INavigationService.cs
├── IConfigService.cs
├── IStatusService.cs
├── IConnectionService.cs
├── IThemeService.cs
└── ... (31 interfaces total)
```

#### Phase 2: Extract Shared Logic (Week 2-3)

Create abstract base classes in `Ui.Services/Services/Base/`:

```csharp
// Base/ConfigServiceBase.cs
public abstract class ConfigServiceBase : IConfigService
{
    protected abstract Task<string> LoadConfigCoreAsync();
    protected abstract Task SaveConfigCoreAsync(string json);

    public async Task<AppConfig> LoadConfigAsync()
    {
        var json = await LoadConfigCoreAsync();
        return JsonSerializer.Deserialize<AppConfig>(json) 
            ?? throw new InvalidOperationException("Config is null");
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, _options);
        await SaveConfigCoreAsync(json);
    }
}
```

#### Phase 3: Migrate Platform Implementations (Week 4)

```csharp
// WPF/Services/ConfigService.cs
public sealed class ConfigService : ConfigServiceBase
{
    protected override Task<string> LoadConfigCoreAsync()
    {
        // WPF-specific file loading
        return File.ReadAllTextAsync(_configPath);
    }

    protected override Task SaveConfigCoreAsync(string json)
    {
        // WPF-specific file saving
        return File.WriteAllTextAsync(_configPath, json);
    }
}
```

#### Phase 4: Deprecate Duplicates (Week 5)

Mark old implementations as `[Obsolete]` and add migration guide.

### Expected Outcomes

- **50% less code** to maintain
- **Single source of truth** for business logic
- **Easier testing** with shared test base classes
- **Faster feature development** (write once, works in both platforms)

---

## Priority 6: Enhanced Developer Documentation

### Create Quick Start Guide

**docs/development/desktop-quick-start.md**:

````markdown
# Desktop Development Quick Start

## Prerequisites

- .NET 9.0 SDK
- Windows 10+ (for WPF/UWP)
- Visual Studio 2022+ or VS Code + C# extension

## 5-Minute Setup

```bash
# Clone repository
git clone https://github.com/rodoHasArrived/Market-Data-Collector.git
cd Market-Data-Collector

# Run bootstrap
make desktop-dev-bootstrap

# Build WPF app
make build-wpf

# Run WPF app with fixtures
dotnet run --project src/MarketDataCollector.Wpf -- --fixture
```

## Your First Change

### 1. Add a New Page

Create `src/MarketDataCollector.Wpf/Views/MyPage.xaml`:

```xml
<Page x:Class="MarketDataCollector.Wpf.Views.MyPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <TextBlock Text="Hello from MyPage!" />
</Page>
```

Create `src/MarketDataCollector.Wpf/Views/MyPage.xaml.cs`:

```csharp
public partial class MyPage : Page
{
    public MyPage()
    {
        InitializeComponent();
    }
}
```

Register in `NavigationService.cs`:

```csharp
_pages["MyPage"] = typeof(MyPage);
```

### 2. Add a Unit Test

Create `tests/MarketDataCollector.Ui.Tests/Services/MyServiceTests.cs`:

```csharp
public class MyServiceTests
{
    [Fact]
    public void MyMethod_WhenCalled_ReturnsExpectedResult()
    {
        // Arrange
        var service = new MyService();
        
        // Act
        var result = service.MyMethod();
        
        // Assert
        result.Should().Be("expected");
    }
}
```

Run tests:

```bash
dotnet test tests/MarketDataCollector.Ui.Tests
```

### 3. Submit PR

```bash
git checkout -b feature/my-feature
git add .
git commit -m "Add MyPage and tests"
git push origin feature/my-feature
```

Use the desktop PR checklist template!

## Common Tasks

### Run with Live Backend

```bash
# Terminal 1: Start backend
dotnet run --project src/MarketDataCollector -- --ui --http-port 8080

# Terminal 2: Start WPF
dotnet run --project src/MarketDataCollector.Wpf
```

### Debug XAML Issues (UWP)

```bash
make uwp-xaml-diagnose
```

### Run Only Desktop Tests

```bash
make test-desktop-services
```
````

---

## Implementation Timeline

### Week 1: Foundation
- [ ] Create `MarketDataCollector.Ui.Tests` project
- [ ] Add 5 initial test files (ApiClient, FormValidation, Collections)
- [ ] Add FixtureDataService
- [ ] Update CI to run desktop unit tests

### Week 2: Testing Expansion
- [ ] Add 10 more test files (all core services)
- [ ] Reach 50% code coverage on Ui.Services
- [ ] Document fixture mode usage

### Week 3: Architecture Documentation
- [ ] Create desktop architecture diagram
- [ ] Write desktop quick start guide
- [ ] Update CLAUDE.md with testing guidelines

### Week 4-5: Code Consolidation (Optional)
- [ ] Extract shared service interfaces
- [ ] Create base service classes
- [ ] Migrate 5 services to shared base (pilot)

---

## Success Metrics

### Quantitative
- [ ] **Test Coverage**: 60%+ for Ui.Services project
- [ ] **Test Count**: 50+ unit tests for desktop services
- [ ] **CI Time**: Desktop tests complete in <2 minutes
- [ ] **Code Duplication**: Reduced from 100% to <30%

### Qualitative
- [ ] **Developer Feedback**: "Faster to develop desktop features"
- [ ] **Bug Detection**: 80%+ of regressions caught by tests before merge
- [ ] **Onboarding Time**: New contributors productive in <1 day
- [ ] **Documentation**: "Clear and easy to follow"

---

## Risks and Mitigation

### Risk: Breaking Existing Functionality
**Mitigation**: Incremental changes with comprehensive testing at each step

### Risk: Test Maintenance Burden
**Mitigation**: Focus on testing stable interfaces, avoid testing implementation details

### Risk: Platform Divergence
**Mitigation**: Shared test base classes ensure consistent behavior

---

## References

- Original Plan: `docs/development/desktop-devex-high-value-improvements.md`
- WPF Implementation: `docs/development/wpf-implementation-notes.md`
- Support Policy: `docs/development/policies/desktop-support-policy.md`
- Workflow Guide: `docs/development/desktop-dev-workflow.md`

---

**Questions or Suggestions?**
Open an issue with label `desktop-development` or discuss in #desktop-dev channel.
