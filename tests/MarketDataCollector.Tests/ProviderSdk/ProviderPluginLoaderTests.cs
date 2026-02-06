using FluentAssertions;
using MarketDataCollector.ProviderSdk;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for <see cref="ProviderPluginLoader"/> discovery and loading mechanisms.
/// </summary>
public sealed class ProviderPluginLoaderTests
{
    [Fact]
    public void DiscoverFromLoadedAssemblies_FindsFreeDataPlugin()
    {
        // Act - discovers from all currently loaded assemblies
        var plugins = ProviderPluginLoader.DiscoverFromLoadedAssemblies();

        // Assert - the FreeData plugin is compiled into this test project's dependency graph
        plugins.Should().ContainSingle(p => p.Info.PluginId == "free-data");
    }

    [Fact]
    public void DiscoverFromAssembly_FindsPluginInFreeDataAssembly()
    {
        // Arrange
        var assembly = typeof(Providers.FreeData.FreeDataPlugin).Assembly;

        // Act
        var plugins = ProviderPluginLoader.DiscoverFromAssembly(assembly);

        // Assert
        plugins.Should().ContainSingle();
        var plugin = plugins[0];
        plugin.Info.PluginId.Should().Be("free-data");
        plugin.Info.DisplayName.Should().Be("Free Data Providers");
        plugin.Info.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void DiscoverFromAssembly_ReturnsEmptyForAssemblyWithNoPlugins()
    {
        // Arrange - use the Contracts assembly which has no plugins
        var assembly = typeof(Contracts.Domain.Models.HistoricalBar).Assembly;

        // Act
        var plugins = ProviderPluginLoader.DiscoverFromAssembly(assembly);

        // Assert
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromDirectory_ReturnsEmptyForNonExistentDirectory()
    {
        // Act
        var plugins = ProviderPluginLoader.LoadFromDirectory("/nonexistent/path/to/plugins");

        // Assert
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromDirectory_ReturnsEmptyForEmptyDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"mdc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var plugins = ProviderPluginLoader.LoadFromDirectory(tempDir);

            // Assert
            plugins.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
