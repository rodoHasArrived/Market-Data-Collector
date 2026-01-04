using System.Reactive.Linq;
using FluentAssertions;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.DataSources.Plugins;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace MarketDataCollector.Tests.Plugins;

public class PluginManagerTests
{
    private readonly IDataSourcePluginLoader _mockLoader;
    private readonly PluginManagerOptions _options;
    private readonly DataSourcePluginManager _manager;
    private readonly string _testPluginDir;

    public PluginManagerTests()
    {
        _mockLoader = Substitute.For<IDataSourcePluginLoader>();
        _mockLoader.HostVersion.Returns(new Version(1, 0, 0));
        _mockLoader.LoadedPlugins.Returns(new List<LoadedPlugin>());

        _testPluginDir = Path.Combine(Path.GetTempPath(), $"test-plugins-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testPluginDir);

        _options = new PluginManagerOptions
        {
            PluginDirectory = _testPluginDir,
            PluginDirectories = [_testPluginDir],
            EnableDirectoryWatching = false, // Disable for tests
            EnableHotReload = false,
            AutoLoadNewPlugins = false
        };

        _manager = new DataSourcePluginManager(_mockLoader, _options);
    }

    [Fact]
    public async Task InitializeAsync_ShouldScanPluginDirectories()
    {
        // Arrange
        _mockLoader.LoadPluginsFromDirectoryAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PluginLoadResult>>([]));

        // Act
        await _manager.InitializeAsync();

        // Assert
        await _mockLoader.Received(1).LoadPluginsFromDirectoryAsync(
            _testPluginDir,
            "*.dll",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadPluginAsync_ShouldAddPluginToManagedPlugins()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        // Act
        var result = await _manager.LoadPluginAsync(testPluginPath);

        // Assert
        result.Success.Should().BeTrue();
        result.Plugin.Should().NotBeNull();
        result.Plugin!.PluginId.Should().Be("test-plugin");
        _manager.AllPlugins.Should().Contain(p => p.PluginId == "test-plugin");
    }

    [Fact]
    public async Task LoadPluginAsync_ShouldReturnFailedResult_WhenLoaderFails()
    {
        // Arrange
        var testPluginPath = "/path/to/bad.dll";

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Failed("Plugin not found")));

        // Act
        var result = await _manager.LoadPluginAsync(testPluginPath);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Plugin not found");
    }

    [Fact]
    public async Task UnloadPluginAsync_ShouldRemovePluginFromManagedPlugins()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        _mockLoader.UnloadPluginAsync("test-plugin", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        await _manager.LoadPluginAsync(testPluginPath);
        _manager.AllPlugins.Should().HaveCount(1);

        // Act
        var result = await _manager.UnloadPluginAsync("test-plugin");

        // Assert
        result.Success.Should().BeTrue();
        _manager.AllPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task EnablePluginAsync_ShouldEnableDisabledPlugin()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");
        loadedPlugin.State = PluginState.Paused;

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        await _manager.LoadPluginAsync(testPluginPath);
        await _manager.DisablePluginAsync("test-plugin");

        // Act
        var result = await _manager.EnablePluginAsync("test-plugin");

        // Assert
        result.Success.Should().BeTrue();
        result.Plugin!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DisablePluginAsync_ShouldDisablePlugin()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        await _manager.LoadPluginAsync(testPluginPath);

        // Act
        var result = await _manager.DisablePluginAsync("test-plugin");

        // Assert
        result.Success.Should().BeTrue();
        result.Plugin!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetPlugin_ShouldReturnPlugin_WhenExists()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        await _manager.LoadPluginAsync(testPluginPath);

        // Act
        var plugin = _manager.GetPlugin("test-plugin");

        // Assert
        plugin.Should().NotBeNull();
        plugin!.PluginId.Should().Be("test-plugin");
    }

    [Fact]
    public void GetPlugin_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var plugin = _manager.GetPlugin("nonexistent");

        // Assert
        plugin.Should().BeNull();
    }

    [Fact]
    public void GetStatus_ShouldReturnCorrectStatus()
    {
        // Act
        var status = _manager.GetStatus();

        // Assert
        status.TotalPlugins.Should().Be(0);
        status.PluginDirectory.Should().Be(_testPluginDir);
        status.HostVersion.Should().Be(new Version(1, 0, 0));
        status.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task StateChanges_ShouldEmitOnPluginLoad()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        var stateChanges = new List<PluginStateChange>();
        using var subscription = _manager.StateChanges.Subscribe(change => stateChanges.Add(change));

        // Act
        await _manager.LoadPluginAsync(testPluginPath);

        // Assert
        stateChanges.Should().Contain(c => c.PluginId == "test-plugin" && c.NewState == PluginState.Loaded);
    }

    [Fact]
    public async Task PluginDataSources_ShouldReturnOnlyActivePlugins()
    {
        // Arrange
        var path1 = "/path/to/active.dll";
        var path2 = "/path/to/disabled.dll";

        var activePlugin = CreateTestLoadedPlugin("active-plugin");
        activePlugin.State = PluginState.Active;

        var disabledPlugin = CreateTestLoadedPlugin("disabled-plugin");
        disabledPlugin.State = PluginState.Paused;

        _mockLoader.LoadPluginAsync(
            path1,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(activePlugin, path1)));

        _mockLoader.LoadPluginAsync(
            path2,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(disabledPlugin, path2)));

        await _manager.LoadPluginAsync(path1);
        await _manager.LoadPluginAsync(path2);
        await _manager.DisablePluginAsync("disabled-plugin");

        // Act
        var dataSources = _manager.PluginDataSources;

        // Assert
        dataSources.Should().HaveCount(1);
        dataSources.First().Id.Should().Be("active-plugin");
    }

    [Fact]
    public async Task DisposeAsync_ShouldUnloadAllPlugins()
    {
        // Arrange
        var testPluginPath = "/path/to/test.dll";
        var loadedPlugin = CreateTestLoadedPlugin("test-plugin");

        _mockLoader.LoadPluginAsync(
            testPluginPath,
            Arg.Any<PluginConfiguration?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PluginLoadResult.Succeeded(loadedPlugin, testPluginPath)));

        _mockLoader.UnloadPluginAsync("test-plugin", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        await _manager.LoadPluginAsync(testPluginPath);

        // Act
        await _manager.DisposeAsync();

        // Assert
        await _mockLoader.Received(1).UnloadPluginAsync("test-plugin", Arg.Any<CancellationToken>());
    }

    private static LoadedPlugin CreateTestLoadedPlugin(string pluginId)
    {
        var instance = Substitute.For<IDataSourcePlugin>();
        instance.Id.Returns(pluginId);
        instance.DisplayName.Returns(pluginId);
        instance.PluginInfo.Returns(new PluginMetadata
        {
            PluginId = pluginId,
            Name = pluginId,
            Version = new Version(1, 0, 0)
        });

        var context = new PluginContext
        {
            Services = new ServiceCollection().BuildServiceProvider(),
            Configuration = new PluginConfiguration(),
            PluginPath = $"/path/to/{pluginId}.dll",
            DataDirectory = $"/data/{pluginId}",
            Loader = Substitute.For<IDataSourcePluginLoader>(),
            HostVersion = new Version(1, 0, 0)
        };

        return new LoadedPlugin
        {
            Instance = instance,
            Metadata = instance.PluginInfo,
            Context = context,
            Assembly = typeof(PluginManagerTests).Assembly,
            AssemblyPath = $"/path/to/{pluginId}.dll",
            State = PluginState.Active
        };
    }

    public void Dispose()
    {
        _manager.DisposeAsync().AsTask().Wait();
        if (Directory.Exists(_testPluginDir))
        {
            try { Directory.Delete(_testPluginDir, true); }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
