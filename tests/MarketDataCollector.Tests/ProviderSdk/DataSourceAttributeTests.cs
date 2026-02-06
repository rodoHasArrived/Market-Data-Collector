using System.Reflection;
using FluentAssertions;
using MarketDataCollector.Providers.FreeData.Stooq;
using MarketDataCollector.Providers.FreeData.Tiingo;
using MarketDataCollector.Providers.FreeData.YahooFinance;
using MarketDataCollector.ProviderSdk.Attributes;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for the SDK <see cref="DataSourceAttribute"/> and <see cref="ImplementsAdrAttribute"/>
/// discovery attributes on plugin provider classes.
/// </summary>
public sealed class DataSourceAttributeTests
{
    [Theory]
    [InlineData(typeof(StooqProvider), "stooq-plugin", DataSourceType.Historical, DataSourceCategory.FreeApi)]
    [InlineData(typeof(TiingoProvider), "tiingo-plugin", DataSourceType.Historical, DataSourceCategory.FreeApi)]
    [InlineData(typeof(YahooFinanceProvider), "yahoo-plugin", DataSourceType.Historical, DataSourceCategory.FreeApi)]
    public void FreeDataProviders_HaveCorrectDataSourceAttribute(
        Type providerType, string expectedId, DataSourceType expectedType, DataSourceCategory expectedCategory)
    {
        // Act
        var attr = providerType.GetCustomAttribute<DataSourceAttribute>();

        // Assert
        attr.Should().NotBeNull();
        attr!.Id.Should().Be(expectedId);
        attr.Type.Should().Be(expectedType);
        attr.Category.Should().Be(expectedCategory);
    }

    [Theory]
    [InlineData(typeof(StooqProvider))]
    [InlineData(typeof(TiingoProvider))]
    [InlineData(typeof(YahooFinanceProvider))]
    public void FreeDataProviders_HaveImplementsAdrAttribute(Type providerType)
    {
        // Act
        var attrs = providerType.GetCustomAttributes<ImplementsAdrAttribute>().ToList();

        // Assert
        attrs.Should().NotBeEmpty();
        attrs.Should().Contain(a => a.AdrId == "ADR-001");
    }

    [Fact]
    public void DataSourceAttribute_StoresAllProperties()
    {
        // Arrange & Act
        var attr = new DataSourceAttribute("test", "Test Source", DataSourceType.Hybrid, DataSourceCategory.Broker)
        {
            Priority = 42,
            EnabledByDefault = false,
            Description = "A test source"
        };

        // Assert
        attr.Id.Should().Be("test");
        attr.DisplayName.Should().Be("Test Source");
        attr.Type.Should().Be(DataSourceType.Hybrid);
        attr.Category.Should().Be(DataSourceCategory.Broker);
        attr.Priority.Should().Be(42);
        attr.EnabledByDefault.Should().BeFalse();
        attr.Description.Should().Be("A test source");
    }

    [Fact]
    public void DataSourceAttribute_ThrowsOnNullId()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DataSourceAttribute(null!, "name", DataSourceType.Historical, DataSourceCategory.FreeApi));
    }

    [Fact]
    public void DataSourceAttribute_ThrowsOnNullDisplayName()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DataSourceAttribute("id", null!, DataSourceType.Historical, DataSourceCategory.FreeApi));
    }
}
