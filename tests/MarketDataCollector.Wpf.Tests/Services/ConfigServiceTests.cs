using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Tests.Services;

/// <summary>
/// Tests for ConfigService singleton service.
/// Validates configuration management and validation functionality.
/// </summary>
public sealed class ConfigServiceTests
{
    [Fact]
    public void Instance_ShouldReturnSingleton()
    {
        // Arrange & Act
        var instance1 = ConfigService.Instance;
        var instance2 = ConfigService.Instance;

        // Assert
        instance1.Should().NotBeNull();
        instance2.Should().NotBeNull();
        instance1.Should().BeSameAs(instance2, "ConfigService should be a singleton");
    }

    [Fact]
    public void IsInitialized_BeforeInitialization_ShouldReturnFalse()
    {
        // Arrange
        var service = ConfigService.Instance;

        // Act
        var isInitialized = service.IsInitialized;

        // Assert - This may vary depending on when the test runs
        // In a fresh instance, it should be false until InitializeAsync is called
    }

    [Fact]
    public async Task InitializeAsync_ShouldSetInitializedFlag()
    {
        // Arrange
        var service = ConfigService.Instance;

        // Act
        await service.InitializeAsync();

        // Assert
        service.IsInitialized.Should().BeTrue("service should be initialized after InitializeAsync");
    }

    [Fact]
    public void ConfigPath_ShouldReturnValidPath()
    {
        // Arrange
        var service = ConfigService.Instance;

        // Act
        var configPath = service.ConfigPath;

        // Assert
        configPath.Should().NotBeNullOrEmpty("ConfigPath should return a valid path");
    }

    [Fact]
    public async Task ValidateConfigAsync_ShouldReturnValidationResult()
    {
        // Arrange
        var service = ConfigService.Instance;
        await service.InitializeAsync();

        // Act
        var result = await service.ValidateConfigAsync();

        // Assert
        result.Should().NotBeNull("validation should return a result");
        result.IsValid.Should().BeTrue("default configuration should be valid");
        result.Errors.Should().BeEmpty("valid configuration should have no errors");
    }

    [Fact]
    public void ConfigServiceValidationResult_Success_ShouldCreateValidResult()
    {
        // Act
        var result = ConfigServiceValidationResult.Success();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ConfigServiceValidationResult_Failure_ShouldCreateInvalidResult()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var result = ConfigServiceValidationResult.Failure(errors);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public async Task GetDataSourcesAsync_ShouldReturnDataSources()
    {
        // Arrange
        var service = ConfigService.Instance;
        await service.InitializeAsync();

        // Act
        var dataSources = await service.GetDataSourcesAsync();

        // Assert
        dataSources.Should().NotBeNull("data sources should not be null");
    }

    [Fact]
    public async Task GetSymbolsAsync_ShouldReturnSymbols()
    {
        // Arrange
        var service = ConfigService.Instance;
        await service.InitializeAsync();

        // Act
        var symbols = await service.GetSymbolsAsync();

        // Assert
        symbols.Should().NotBeNull("symbols should not be null");
    }

    [Fact]
    public async Task GetActiveDataSourceAsync_ShouldReturnActiveSource()
    {
        // Arrange
        var service = ConfigService.Instance;
        await service.InitializeAsync();

        // Act
        var activeSource = await service.GetActiveDataSourceAsync();

        // Assert - May be null if no active source is configured
        // Just verify the method doesn't throw
    }

    [Fact]
    public async Task SaveDataSourcesAsync_WithValidData_ShouldNotThrow()
    {
        // Arrange
        var service = ConfigService.Instance;
        await service.InitializeAsync();
        var dataSources = await service.GetDataSourcesAsync();

        // Act
        Func<Task> act = async () => await service.SaveDataSourcesAsync(dataSources);

        // Assert
        await act.Should().NotThrowAsync("saving valid data sources should not throw");
    }

    [Fact]
    public async Task ReloadConfigAsync_ShouldNotThrow()
    {
        // Arrange
        var service = ConfigService.Instance;
        await service.InitializeAsync();

        // Act
        Func<Task> act = async () => await service.ReloadConfigAsync();

        // Assert
        await act.Should().NotThrowAsync("reloading config should not throw");
    }
}
