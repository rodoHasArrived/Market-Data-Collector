using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.DataSources.Plugins;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarketDataCollector.Tests.Plugins;

public class PluginArchitectureTests
{
    #region Plugin Metadata Tests

    [Fact]
    public void PluginMetadata_ShouldBeCreatedWithRequiredProperties()
    {
        // Arrange & Act
        var metadata = new PluginMetadata
        {
            PluginId = "test-plugin",
            Name = "Test Plugin",
            Version = new Version(1, 2, 3)
        };

        // Assert
        metadata.PluginId.Should().Be("test-plugin");
        metadata.Name.Should().Be("Test Plugin");
        metadata.Version.Should().Be(new Version(1, 2, 3));
    }

    [Fact]
    public void PluginMetadata_IsCompatibleWith_ShouldReturnTrue_WhenWithinRange()
    {
        // Arrange
        var metadata = new PluginMetadata
        {
            PluginId = "test",
            Name = "Test",
            Version = new Version(1, 0, 0),
            MinHostVersion = new Version(1, 0, 0),
            MaxHostVersion = new Version(2, 0, 0)
        };

        // Act & Assert
        metadata.IsCompatibleWith(new Version(1, 0, 0)).Should().BeTrue();
        metadata.IsCompatibleWith(new Version(1, 5, 0)).Should().BeTrue();
        metadata.IsCompatibleWith(new Version(1, 9, 9)).Should().BeTrue();
    }

    [Fact]
    public void PluginMetadata_IsCompatibleWith_ShouldReturnFalse_WhenBelowMinVersion()
    {
        // Arrange
        var metadata = new PluginMetadata
        {
            PluginId = "test",
            Name = "Test",
            Version = new Version(1, 0, 0),
            MinHostVersion = new Version(2, 0, 0)
        };

        // Act & Assert
        metadata.IsCompatibleWith(new Version(1, 0, 0)).Should().BeFalse();
        metadata.IsCompatibleWith(new Version(1, 9, 9)).Should().BeFalse();
    }

    [Fact]
    public void PluginMetadata_IsCompatibleWith_ShouldReturnFalse_WhenAtOrAboveMaxVersion()
    {
        // Arrange
        var metadata = new PluginMetadata
        {
            PluginId = "test",
            Name = "Test",
            Version = new Version(1, 0, 0),
            MaxHostVersion = new Version(2, 0, 0)
        };

        // Act & Assert
        metadata.IsCompatibleWith(new Version(2, 0, 0)).Should().BeFalse();
        metadata.IsCompatibleWith(new Version(3, 0, 0)).Should().BeFalse();
    }

    #endregion

    #region Plugin Configuration Tests

    [Fact]
    public void PluginConfiguration_GetSetting_ShouldReturnValue_WhenExists()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            Settings = new Dictionary<string, object?>
            {
                ["apiKey"] = "test-key",
                ["timeout"] = 30
            }
        };

        // Act & Assert
        config.GetSetting<string>("apiKey").Should().Be("test-key");
        config.GetSetting<int>("timeout").Should().Be(30);
    }

    [Fact]
    public void PluginConfiguration_GetSetting_ShouldReturnDefault_WhenNotExists()
    {
        // Arrange
        var config = new PluginConfiguration();

        // Act & Assert
        config.GetSetting<string>("missing", "default").Should().Be("default");
        config.GetSetting<int>("missing", 42).Should().Be(42);
    }

    #endregion

    #region Plugin Validation Tests

    [Fact]
    public void PluginValidationResult_IsValid_ShouldBeTrue_WhenNoErrors()
    {
        // Arrange
        var result = PluginValidationResult.Valid(
            [],
            [],
            "TestAssembly",
            new Version(1, 0, 0));

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginValidationResult_IsValid_ShouldBeFalse_WhenHasErrors()
    {
        // Arrange
        var result = PluginValidationResult.Invalid(["Error 1", "Error 2"]);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    #endregion

    #region Plugin Load Result Tests

    [Fact]
    public void PluginLoadResult_Succeeded_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var plugin = CreateTestLoadedPlugin();

        // Act
        var result = PluginLoadResult.Succeeded(plugin, "/path/to/plugin.dll");

        // Assert
        result.Success.Should().BeTrue();
        result.Plugin.Should().Be(plugin);
        result.AssemblyPath.Should().Be("/path/to/plugin.dll");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PluginLoadResult_Failed_ShouldCreateFailedResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = PluginLoadResult.Failed("Load failed", exception, "/path/to/plugin.dll");

        // Assert
        result.Success.Should().BeFalse();
        result.Plugin.Should().BeNull();
        result.ErrorMessage.Should().Be("Load failed");
        result.Exception.Should().Be(exception);
    }

    #endregion

    #region Plugin State Tests

    [Fact]
    public void ManagedPlugin_ShouldTrackState()
    {
        // Arrange
        var managedPlugin = new ManagedPlugin
        {
            PluginId = "test-plugin",
            AssemblyPath = "/path/to/plugin.dll",
            IsEnabled = true
        };

        // Assert
        managedPlugin.PluginId.Should().Be("test-plugin");
        managedPlugin.IsEnabled.Should().BeTrue();
        managedPlugin.FirstLoadedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PluginState_ShouldHaveCorrectValues()
    {
        // Assert
        PluginState.Loaded.Should().BeDefined();
        PluginState.Initializing.Should().BeDefined();
        PluginState.Active.Should().BeDefined();
        PluginState.Paused.Should().BeDefined();
        PluginState.Error.Should().BeDefined();
        PluginState.Unloading.Should().BeDefined();
        PluginState.Unloaded.Should().BeDefined();
    }

    #endregion

    #region Plugin Permissions Tests

    [Fact]
    public void PluginPermissions_ShouldSupportFlagOperations()
    {
        // Arrange
        var permissions = PluginPermissions.Network | PluginPermissions.FileSystem;

        // Assert
        permissions.HasFlag(PluginPermissions.Network).Should().BeTrue();
        permissions.HasFlag(PluginPermissions.FileSystem).Should().BeTrue();
        permissions.HasFlag(PluginPermissions.NativeCode).Should().BeFalse();
    }

    [Fact]
    public void PluginPermissions_FullTrust_ShouldIncludeAllPermissions()
    {
        // Arrange
        var fullTrust = PluginPermissions.FullTrust;

        // Assert
        fullTrust.HasFlag(PluginPermissions.Network).Should().BeTrue();
        fullTrust.HasFlag(PluginPermissions.FileSystem).Should().BeTrue();
        fullTrust.HasFlag(PluginPermissions.Environment).Should().BeTrue();
        fullTrust.HasFlag(PluginPermissions.NativeCode).Should().BeTrue();
        fullTrust.HasFlag(PluginPermissions.Credentials).Should().BeTrue();
    }

    #endregion

    #region Plugin System Status Tests

    [Fact]
    public void PluginSystemStatus_ShouldReportCorrectCounts()
    {
        // Arrange
        var status = new PluginSystemStatus
        {
            TotalPlugins = 5,
            EnabledPlugins = 4,
            ActivePlugins = 3,
            ErrorPlugins = 1,
            PluginDirectory = "/plugins",
            HostVersion = new Version(1, 0, 0),
            IsInitialized = true,
            DirectoryWatchingEnabled = true
        };

        // Assert
        status.TotalPlugins.Should().Be(5);
        status.EnabledPlugins.Should().Be(4);
        status.ActivePlugins.Should().Be(3);
        status.ErrorPlugins.Should().Be(1);
        status.IsInitialized.Should().BeTrue();
    }

    #endregion

    #region Plugin State Change Tests

    [Fact]
    public void PluginStateChange_ShouldCaptureStateTransition()
    {
        // Arrange
        var change = new PluginStateChange
        {
            PluginId = "test-plugin",
            NewState = PluginState.Active,
            Reason = "Initialized successfully",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Assert
        change.PluginId.Should().Be("test-plugin");
        change.NewState.Should().Be(PluginState.Active);
        change.Reason.Should().Be("Initialized successfully");
    }

    #endregion

    #region Plugin Load Context Tests

    [Fact]
    public void PluginLoadContext_ShouldBeCollectible()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var context = new PluginLoadContext(tempFile, isCollectible: true);

            // Assert
            context.IsCollectible.Should().BeTrue();
            context.PluginPath.Should().Be(tempFile);

            // Cleanup
            context.Unload();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void PluginLoadContextFactory_ShouldCreateContext()
    {
        // Arrange
        var factory = new PluginLoadContextFactory(["SharedAssembly"]);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var context = factory.Create(tempFile, isCollectible: true);

            // Assert
            context.Should().NotBeNull();
            context.IsCollectible.Should().BeTrue();
            context.SharedAssemblies.Should().Contain("SharedAssembly");

            // Cleanup
            context.Unload();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region Plugin Loader Options Tests

    [Fact]
    public void PluginLoaderOptions_ShouldHaveDefaults()
    {
        // Arrange
        var options = new PluginLoaderOptions();

        // Assert
        options.PluginDataDirectory.Should().NotBeNullOrEmpty();
        options.GrantedPermissions.Should().Be(PluginPermissions.Network | PluginPermissions.Environment);
        options.RequireExplicitPermissions.Should().BeFalse();
        options.EnableHotReload.Should().BeTrue();
        options.OperationTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region Plugin Manager Options Tests

    [Fact]
    public void PluginManagerOptions_ShouldHaveDefaults()
    {
        // Arrange
        var options = new PluginManagerOptions();

        // Assert
        options.PluginDirectory.Should().Contain("plugins");
        options.EnableDirectoryWatching.Should().BeTrue();
        options.EnableHotReload.Should().BeTrue();
        options.AutoLoadNewPlugins.Should().BeTrue();
        options.HotReloadDebounce.Should().Be(TimeSpan.FromSeconds(2));
    }

    #endregion

    #region Plugin Management Result Tests

    [Fact]
    public void PluginManagementResult_Succeeded_ShouldCreateSuccessResult()
    {
        // Arrange
        var plugin = new ManagedPlugin
        {
            PluginId = "test",
            AssemblyPath = "/path/to/test.dll"
        };

        // Act
        var result = PluginManagementResult.Succeeded(plugin);

        // Assert
        result.Success.Should().BeTrue();
        result.Plugin.Should().Be(plugin);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PluginManagementResult_Failed_ShouldCreateFailedResult()
    {
        // Act
        var result = PluginManagementResult.Failed("Plugin not found");

        // Assert
        result.Success.Should().BeFalse();
        result.Plugin.Should().BeNull();
        result.ErrorMessage.Should().Be("Plugin not found");
    }

    #endregion

    #region Plugin Configuration Section Tests

    [Fact]
    public void PluginSystemConfig_ShouldHaveDefaults()
    {
        // Arrange
        var config = new PluginSystemConfig();

        // Assert
        config.Enabled.Should().BeTrue();
        config.PluginDirectory.Should().Be("plugins");
        config.EnableHotReload.Should().BeTrue();
        config.EnableDirectoryWatching.Should().BeTrue();
        config.AutoLoadNewPlugins.Should().BeTrue();
        config.HotReloadDebounceMs.Should().Be(2000);
        config.RequireExplicitPermissions.Should().BeFalse();
        config.DefaultPermissions.Should().Contain("Network");
        config.DefaultPermissions.Should().Contain("Environment");
    }

    [Fact]
    public void PluginInstanceConfig_ShouldHaveDefaults()
    {
        // Arrange
        var config = new PluginInstanceConfig();

        // Assert
        config.Enabled.Should().BeTrue();
        config.Priority.Should().BeNull();
        config.Settings.Should().BeEmpty();
    }

    #endregion

    #region Test Helpers

    private static LoadedPlugin CreateTestLoadedPlugin()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "test-plugin",
            Name = "Test Plugin",
            Version = new Version(1, 0, 0)
        };

        var context = new PluginContext
        {
            Services = new ServiceCollection().BuildServiceProvider(),
            Configuration = new PluginConfiguration(),
            PluginPath = "/path/to/plugin.dll",
            DataDirectory = "/data/plugins/test",
            Loader = null!,
            HostVersion = new Version(1, 0, 0)
        };

        return new LoadedPlugin
        {
            Instance = new TestPlugin(),
            Metadata = metadata,
            Context = context,
            Assembly = typeof(PluginArchitectureTests).Assembly,
            AssemblyPath = "/path/to/plugin.dll",
            State = PluginState.Active
        };
    }

    #endregion
}

#region Test Plugin Implementation

/// <summary>
/// A test plugin implementation for unit testing.
/// </summary>
internal class TestPlugin : IDataSourcePlugin
{
    public string Id => "test-plugin";
    public string DisplayName => "Test Plugin";
    public string Description => "A test plugin for unit testing";
    public DataSourceType Type => DataSourceType.Historical;
    public DataSourceCategory Category => DataSourceCategory.Free;
    public int Priority => 100;
    public DataSourceCapabilities Capabilities => DataSourceCapabilities.HistoricalDailyBars;
    public DataSourceCapabilityInfo CapabilityInfo => DataSourceCapabilityInfo.Default(Capabilities);
    public IReadOnlySet<string> SupportedMarkets => new HashSet<string> { "US" };
    public IReadOnlySet<AssetClass> SupportedAssetClasses => new HashSet<AssetClass> { AssetClass.Equity };
    public DataSourceHealth Health => DataSourceHealth.Healthy();
    public DataSourceStatus Status => DataSourceStatus.Connected;
    public RateLimitState RateLimitState => RateLimitState.Available;
    public IObservable<DataSourceHealthChanged> HealthChanges => Observable.Empty<DataSourceHealthChanged>();

    public PluginMetadata PluginInfo => new()
    {
        PluginId = "test-plugin",
        Name = "Test Plugin",
        Version = new Version(1, 0, 0)
    };

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ValidateCredentialsAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> TestConnectivityAsync(CancellationToken ct = default) => Task.FromResult(true);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task OnLoadAsync(PluginContext context, CancellationToken ct = default) => Task.CompletedTask;
    public Task OnUnloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task OnConfigurationChangedAsync(PluginConfiguration newConfig, CancellationToken ct = default) => Task.CompletedTask;
}

#endregion
