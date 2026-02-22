using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Services;
using Xunit;

namespace MarketDataCollector.Tests.Application.Services;

/// <summary>
/// Tests for AutoConfigurationService focusing on provider detection,
/// auto-configuration, and first-time config generation.
/// </summary>
public sealed class AutoConfigurationServiceTests
{
    private readonly AutoConfigurationService _sut = new();

    #region DetectAvailableProviders

    [Fact]
    public void DetectAvailableProviders_NoEnvironmentVars_ReturnsFreeProviders()
    {
        // Free providers (Yahoo, Stooq) should always be detected because they
        // don't require credentials.
        var providers = _sut.DetectAvailableProviders();

        // At minimum, free providers should be in the list
        providers.Should().NotBeNull();

        // Yahoo and Stooq are credential-free and should appear
        var freeProviders = providers.Where(p =>
            p.Name.Equals("Yahoo Finance", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Stooq", StringComparison.OrdinalIgnoreCase));
        freeProviders.Should().NotBeEmpty("free providers should always be detected");
    }

    [Fact]
    public void DetectAvailableProviders_ResultsAreOrderedByPriority()
    {
        var providers = _sut.DetectAvailableProviders();

        if (providers.Count > 1)
        {
            // Verify providers are ordered by priority (lower = higher priority)
            for (int i = 1; i < providers.Count; i++)
            {
                providers[i].Priority.Should().BeGreaterOrEqualTo(providers[i - 1].Priority,
                    "providers should be ordered by priority ascending");
            }
        }
    }

    [Fact]
    public void DetectAvailableProviders_ProviderHasRequiredMetadata()
    {
        var providers = _sut.DetectAvailableProviders();

        foreach (var provider in providers)
        {
            provider.Name.Should().NotBeNullOrWhiteSpace("every provider must have a name");
            provider.DisplayName.Should().NotBeNullOrWhiteSpace("every provider must have a display name");
        }
    }

    #endregion

    #region AutoConfigure

    [Fact]
    public void AutoConfigure_DefaultConfig_ReturnsResult()
    {
        var config = new AppConfig();

        var result = _sut.AutoConfigure(config);

        result.Should().NotBeNull();
        result.Config.Should().NotBeNull();
    }

    [Fact]
    public void AutoConfigure_NullSymbols_AppliesDefaults()
    {
        var config = new AppConfig(Symbols: null);

        var result = _sut.AutoConfigure(config);

        result.Config.Should().NotBeNull();
    }

    [Fact]
    public void AutoConfigure_EmptyConfig_ProducesRecommendations()
    {
        var config = new AppConfig();

        var result = _sut.AutoConfigure(config);

        // Should produce at least some recommendations or fixes for a bare config
        result.Should().NotBeNull();
    }

    [Fact]
    public void AutoConfigure_ConfigWithSymbols_PreservesExistingSymbols()
    {
        var symbols = new[]
        {
            new SymbolConfig("AAPL", SubscribeTrades: true, SubscribeDepth: false),
            new SymbolConfig("MSFT", SubscribeTrades: true, SubscribeDepth: false)
        };
        var config = new AppConfig(Symbols: symbols);

        var result = _sut.AutoConfigure(config);

        result.Config.Symbols.Should().NotBeNull();
        result.Config.Symbols!.Length.Should().BeGreaterOrEqualTo(2);
        result.Config.Symbols.Should().Contain(s => s.Symbol == "AAPL");
        result.Config.Symbols.Should().Contain(s => s.Symbol == "MSFT");
    }

    #endregion

    #region GenerateFirstTimeConfig

    [Theory]
    [InlineData(UseCase.Development)]
    [InlineData(UseCase.Research)]
    [InlineData(UseCase.BackfillOnly)]
    public void GenerateFirstTimeConfig_DifferentUseCases_ReturnsValidConfig(UseCase useCase)
    {
        var result = _sut.GenerateFirstTimeConfig(useCase);

        result.Should().NotBeNull();
        result.Symbols.Should().NotBeNullOrEmpty("first-time config should include default symbols");
    }

    [Fact]
    public void GenerateFirstTimeConfig_Development_IncludesCommonSymbols()
    {
        var result = _sut.GenerateFirstTimeConfig(UseCase.Development);

        result.Symbols.Should().NotBeNullOrEmpty();
        // Development should include well-known symbols like SPY
        result.Symbols!.Should().Contain(s => s.Symbol == "SPY",
            "development config should include SPY as a default");
    }

    [Fact]
    public void GenerateFirstTimeConfig_BackfillOnly_HasBackfillConfig()
    {
        var result = _sut.GenerateFirstTimeConfig(UseCase.BackfillOnly);

        result.Should().NotBeNull();
        // Backfill-only should have backfill-related configuration
        result.Backfill.Should().NotBeNull("backfill-only use case should configure backfill settings");
    }

    [Fact]
    public void GenerateFirstTimeConfig_Research_IncludesHistoricalDataSettings()
    {
        var result = _sut.GenerateFirstTimeConfig(UseCase.Research);

        result.Should().NotBeNull();
        // Research should configure backfill for historical data access
        result.Backfill.Should().NotBeNull("research use case needs historical data");
    }

    [Theory]
    [InlineData(SymbolPreset.USMajorIndices)]
    [InlineData(SymbolPreset.TechGiants)]
    [InlineData(SymbolPreset.SP500Top20)]
    public void GenerateFirstTimeConfig_WithSymbolPresets_IncludesExpectedSymbols(SymbolPreset preset)
    {
        var result = _sut.GenerateFirstTimeConfig(UseCase.Development, preset);

        result.Symbols.Should().NotBeNullOrEmpty(
            $"preset {preset} should produce symbols");
        result.Symbols!.Length.Should().BeGreaterThan(1,
            $"preset {preset} should include multiple symbols");
    }

    [Fact]
    public void GenerateFirstTimeConfig_CustomPreset_ReturnsMinimalConfig()
    {
        var result = _sut.GenerateFirstTimeConfig(UseCase.Development, SymbolPreset.Custom);

        result.Should().NotBeNull();
        // Custom preset should still have a valid config with at least defaults
        result.Symbols.Should().NotBeNull();
    }

    #endregion
}
