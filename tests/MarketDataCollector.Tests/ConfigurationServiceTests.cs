using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Services;
using Xunit;

namespace MarketDataCollector.Tests;

/// <summary>
/// Tests for ConfigurationService - the unified configuration entry point.
/// </summary>
public class ConfigurationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempConfigPath;
    private readonly ConfigurationService _service;

    public ConfigurationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"mdc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempConfigPath = Path.Combine(_tempDir, "appsettings.json");
        _service = new ConfigurationService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    #region Configuration Loading Tests

    [Fact]
    public void Load_NonExistentFile_ReturnsDefaultConfig()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.json");

        // Act
        var config = _service.Load(nonExistentPath);

        // Assert
        config.Should().NotBeNull();
        config.DataRoot.Should().Be("data");
    }

    [Fact]
    public void Load_ValidConfigFile_ReturnsConfig()
    {
        // Arrange
        var json = """
            {
                "dataRoot": "test-data",
                "dataSource": "Alpaca",
                "symbols": [
                    { "symbol": "SPY", "subscribeTrades": true }
                ]
            }
            """;
        File.WriteAllText(_tempConfigPath, json);

        // Act
        var config = _service.Load(_tempConfigPath);

        // Assert
        config.DataRoot.Should().Be("test-data");
        config.DataSource.Should().Be(DataSourceKind.Alpaca);
        config.Symbols.Should().HaveCount(1);
        config.Symbols![0].Symbol.Should().Be("SPY");
    }

    [Fact]
    public void Load_StoresCurrentConfigAndPath()
    {
        // Arrange
        var json = """{ "dataRoot": "stored-data" }""";
        File.WriteAllText(_tempConfigPath, json);

        // Act
        _service.Load(_tempConfigPath);

        // Assert
        _service.CurrentConfig.Should().NotBeNull();
        _service.CurrentConfig!.DataRoot.Should().Be("stored-data");
        _service.CurrentConfigPath.Should().Contain("appsettings.json");
    }

    [Fact]
    public void Reload_ReloadsCurrentConfig()
    {
        // Arrange
        var json1 = """{ "dataRoot": "initial-data" }""";
        File.WriteAllText(_tempConfigPath, json1);
        _service.Load(_tempConfigPath);

        // Modify the file
        var json2 = """{ "dataRoot": "updated-data" }""";
        File.WriteAllText(_tempConfigPath, json2);

        // Act
        var reloadedConfig = _service.Reload();

        // Assert
        reloadedConfig.DataRoot.Should().Be("updated-data");
        _service.CurrentConfig!.DataRoot.Should().Be("updated-data");
    }

    [Fact]
    public void Reload_WithoutPriorLoad_ThrowsInvalidOperationException()
    {
        // Arrange
        var freshService = new ConfigurationService();

        // Act & Assert
        Action act = () => freshService.Reload();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No configuration has been loaded*");
    }

    #endregion

    #region Configuration Saving Tests

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        // Arrange
        var config = new AppConfig(DataRoot: "saved-data");

        // Act
        await _service.SaveAsync(config, _tempConfigPath);

        // Assert
        File.Exists(_tempConfigPath).Should().BeTrue();
        var savedJson = await File.ReadAllTextAsync(_tempConfigPath);
        savedJson.Should().Contain("saved-data");
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        // Arrange
        var nestedPath = Path.Combine(_tempDir, "nested", "dir", "appsettings.json");
        var config = new AppConfig(DataRoot: "nested-data");

        // Act
        _service.Save(config, nestedPath);

        // Assert
        File.Exists(nestedPath).Should().BeTrue();
    }

    [Fact]
    public void Save_UpdatesCurrentConfigAndPath()
    {
        // Arrange
        var config = new AppConfig(DataRoot: "new-data");

        // Act
        _service.Save(config, _tempConfigPath);

        // Assert
        _service.CurrentConfig.Should().NotBeNull();
        _service.CurrentConfig!.DataRoot.Should().Be("new-data");
        _service.CurrentConfigPath.Should().Contain("appsettings.json");
    }

    #endregion

    #region Config Path Resolution Tests

    [Fact]
    public void ResolveConfigPath_WithNoArgs_ReturnsDefault()
    {
        // Act
        var path = _service.ResolveConfigPath(null);

        // Assert
        path.Should().Be("appsettings.json");
    }

    [Fact]
    public void ResolveConfigPath_WithConfigArg_ReturnsArgValue()
    {
        // Arrange
        var args = new[] { "--config", "/custom/path/config.json" };

        // Act
        var path = _service.ResolveConfigPath(args);

        // Assert
        path.Should().Be("/custom/path/config.json");
    }

    [Fact]
    public void ResolveConfigPath_WithOtherArgs_IgnoresThem()
    {
        // Arrange
        var args = new[] { "--ui", "--verbose", "--other", "value" };

        // Act
        var path = _service.ResolveConfigPath(args);

        // Assert
        path.Should().Be("appsettings.json");
    }

    #endregion

    #region Provider Detection Tests

    [Fact]
    public void DetectProviders_ReturnsProviderList()
    {
        // Act
        var providers = _service.DetectProviders();

        // Assert
        providers.Should().NotBeEmpty();
        providers.Should().Contain(p => p.Name == "Yahoo"); // Yahoo is always available (no API key needed)
    }

    [Fact]
    public void DetectProviders_IncludesCapabilities()
    {
        // Act
        var providers = _service.DetectProviders();

        // Assert
        var yahoo = providers.FirstOrDefault(p => p.Name == "Yahoo");
        yahoo.Should().NotBeNull();
        yahoo!.Capabilities.Should().Contain("Historical");
    }

    #endregion

    #region Environment Override Tests

    [Fact]
    public void GetRecognizedEnvironmentVariables_ReturnsVariableList()
    {
        // Act
        var variables = _service.GetRecognizedEnvironmentVariables();

        // Assert
        variables.Should().NotBeEmpty();
        variables.Should().Contain(v => v.EnvironmentVariable == "MDC_DATA_ROOT");
        variables.Should().Contain(v => v.EnvironmentVariable == "ALPACA_KEY_ID");
    }

    [Fact]
    public void GetEnvironmentVariableDocumentation_ReturnsMarkdown()
    {
        // Act
        var docs = _service.GetEnvironmentVariableDocumentation();

        // Assert
        docs.Should().NotBeEmpty();
        docs.Should().Contain("# Environment Variable Configuration");
        docs.Should().Contain("MDC_");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateConfiguration_ValidConfig_ReturnsValid()
    {
        // Arrange
        var config = new AppConfig(DataRoot: "data");

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_EmptyDataRoot_ReturnsErrors()
    {
        // Arrange
        var config = new AppConfig(DataRoot: "");

        // Act
        var result = _service.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion

    #region Auto-Configuration Tests

    [Fact]
    public void AutoConfigure_ReturnsConfigWithDefaults()
    {
        // Act
        var result = _service.AutoConfigure();

        // Assert
        result.Success.Should().BeTrue();
        result.Config.Should().NotBeNull();
        result.DetectedProviders.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateFirstTimeConfig_ReturnsValidConfig()
    {
        // Arrange
        var options = new FirstTimeConfigOptions(
            UseCase: UseCase.Development,
            SymbolPreset: SymbolPreset.USMajorIndices,
            EnableBackfill: true
        );

        // Act
        var config = _service.GenerateFirstTimeConfig(options);

        // Assert
        config.Should().NotBeNull();
        config.Symbols.Should().NotBeEmpty();
    }

    #endregion

    #region Environment Overlay Tests

    [Fact]
    public void Load_WithEnvironmentOverlay_MergesConfigs()
    {
        // Arrange
        var baseJson = """
            {
                "dataRoot": "base-data",
                "dataSource": "IB"
            }
            """;
        File.WriteAllText(_tempConfigPath, baseJson);

        // Create environment-specific overlay
        var envOverlayPath = Path.Combine(_tempDir, "appsettings.Production.json");
        var envJson = """
            {
                "dataRoot": "production-data"
            }
            """;
        File.WriteAllText(envOverlayPath, envJson);

        // Set environment variable
        var originalEnv = Environment.GetEnvironmentVariable("MDC_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("MDC_ENVIRONMENT", "Production");

            // Act
            var config = _service.Load(_tempConfigPath);

            // Assert
            config.DataRoot.Should().Be("production-data");
            config.DataSource.Should().Be(DataSourceKind.IB); // Kept from base
        }
        finally
        {
            Environment.SetEnvironmentVariable("MDC_ENVIRONMENT", originalEnv);
        }
    }

    #endregion
}
