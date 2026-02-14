using System.Collections.Immutable;
using FluentAssertions;
using MarketDataCollector.Infrastructure.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.DataSources;

public sealed class DataSourceAttributeTests
{
    // ── DataSourceAttribute construction ────────────────────────────

    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var attr = new DataSourceAttribute("test", "Test Provider", DataSourceType.Historical, DataSourceCategory.Free);

        attr.Id.Should().Be("test");
        attr.DisplayName.Should().Be("Test Provider");
        attr.Type.Should().Be(DataSourceType.Historical);
        attr.Category.Should().Be(DataSourceCategory.Free);
    }

    [Fact]
    public void Constructor_SetsDefaultOptionalProperties()
    {
        var attr = new DataSourceAttribute("test", "Test", DataSourceType.Realtime, DataSourceCategory.Broker);

        attr.Priority.Should().Be(100);
        attr.EnabledByDefault.Should().BeTrue();
        attr.Description.Should().BeNull();
        attr.ConfigSection.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        var act = () => new DataSourceAttribute(null!, "Test", DataSourceType.Realtime, DataSourceCategory.Broker);
        act.Should().Throw<ArgumentNullException>().WithParameterName("id");
    }

    [Fact]
    public void Constructor_WithNullDisplayName_ThrowsArgumentNullException()
    {
        var act = () => new DataSourceAttribute("test", null!, DataSourceType.Realtime, DataSourceCategory.Broker);
        act.Should().Throw<ArgumentNullException>().WithParameterName("displayName");
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        var attr = new DataSourceAttribute("alpaca", "Alpaca", DataSourceType.Hybrid, DataSourceCategory.Broker)
        {
            Priority = 10,
            EnabledByDefault = false,
            Description = "WebSocket streaming",
            ConfigSection = "Alpaca"
        };

        attr.Priority.Should().Be(10);
        attr.EnabledByDefault.Should().BeFalse();
        attr.Description.Should().Be("WebSocket streaming");
        attr.ConfigSection.Should().Be("Alpaca");
    }

    // ── DataSourceMetadata ──────────────────────────────────────────

    [Fact]
    public void FromAttribute_MapsAllProperties()
    {
        var attr = new DataSourceAttribute("yahoo", "Yahoo Finance", DataSourceType.Historical, DataSourceCategory.Free)
        {
            Priority = 50,
            EnabledByDefault = true,
            Description = "Free EOD data",
            ConfigSection = "Yahoo"
        };

        var metadata = DataSourceMetadata.FromAttribute(attr, typeof(FakeDataSource));

        metadata.Id.Should().Be("yahoo");
        metadata.DisplayName.Should().Be("Yahoo Finance");
        metadata.Type.Should().Be(DataSourceType.Historical);
        metadata.Category.Should().Be(DataSourceCategory.Free);
        metadata.Priority.Should().Be(50);
        metadata.EnabledByDefault.Should().BeTrue();
        metadata.Description.Should().Be("Free EOD data");
        metadata.ConfigSection.Should().Be("Yahoo");
        metadata.ImplementationType.Should().Be(typeof(FakeDataSource));
    }

    [Fact]
    public void FromAttribute_DefaultsConfigSectionToId()
    {
        var attr = new DataSourceAttribute("stooq", "Stooq", DataSourceType.Historical, DataSourceCategory.Free);

        var metadata = DataSourceMetadata.FromAttribute(attr, typeof(FakeDataSource));

        metadata.ConfigSection.Should().Be("stooq");
    }

    [Theory]
    [InlineData(DataSourceType.Realtime, true, false)]
    [InlineData(DataSourceType.Historical, false, true)]
    [InlineData(DataSourceType.Hybrid, true, true)]
    public void IsRealtime_IsHistorical_CorrectForType(DataSourceType type, bool expectedRealtime, bool expectedHistorical)
    {
        var attr = new DataSourceAttribute("test", "Test", type, DataSourceCategory.Free);
        var metadata = DataSourceMetadata.FromAttribute(attr, typeof(FakeDataSource));

        metadata.IsRealtime.Should().Be(expectedRealtime);
        metadata.IsHistorical.Should().Be(expectedHistorical);
    }

    // ── Extension methods ───────────────────────────────────────────

    [Fact]
    public void GetDataSourceAttribute_OnDecoratedType_ReturnsAttribute()
    {
        var attr = typeof(FakeDataSource).GetDataSourceAttribute();
        attr.Should().NotBeNull();
        attr!.Id.Should().Be("fake");
    }

    [Fact]
    public void GetDataSourceAttribute_OnPlainType_ReturnsNull()
    {
        var attr = typeof(NotADataSource).GetDataSourceAttribute();
        attr.Should().BeNull();
    }

    [Fact]
    public void GetDataSourceMetadata_OnDecoratedType_ReturnsMetadata()
    {
        var metadata = typeof(FakeDataSource).GetDataSourceMetadata();
        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be("fake");
        metadata.ImplementationType.Should().Be(typeof(FakeDataSource));
    }

    [Fact]
    public void IsDataSource_WithValidType_ReturnsTrue()
    {
        typeof(FakeDataSource).IsDataSource().Should().BeTrue();
    }

    [Fact]
    public void IsDataSource_WithInterfaceType_ReturnsFalse()
    {
        typeof(IDataSource).IsDataSource().Should().BeFalse();
    }

    [Fact]
    public void IsDataSource_WithAbstractType_ReturnsFalse()
    {
        typeof(AbstractDataSource).IsDataSource().Should().BeFalse();
    }

    [Fact]
    public void IsDataSource_WithUnattributedType_ReturnsFalse()
    {
        typeof(NotADataSource).IsDataSource().Should().BeFalse();
    }

    [Fact]
    public void IsDataSource_WithAttributeButNoInterface_ReturnsFalse()
    {
        typeof(AttributedButNotDataSource).IsDataSource().Should().BeFalse();
    }

    // ── DataSourceRegistry ──────────────────────────────────────────

    [Fact]
    public void DiscoverFromAssemblies_FindsDecoratedTypes()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(FakeDataSource).Assembly);

        // Should discover at least the FakeDataSource in this test assembly
        registry.Sources.Should().Contain(s => s.Id == "fake");
    }

    [Fact]
    public void DiscoverFromAssemblies_IgnoresDuplicateIds()
    {
        var registry = new DataSourceRegistry();
        // Discover twice from the same assembly
        registry.DiscoverFromAssemblies(typeof(FakeDataSource).Assembly);
        registry.DiscoverFromAssemblies(typeof(FakeDataSource).Assembly);

        registry.Sources.Count(s => s.Id == "fake").Should().Be(1);
    }

    [Fact]
    public void DiscoverFromAssemblies_WithNullAssemblies_Throws()
    {
        var registry = new DataSourceRegistry();
        var act = () => registry.DiscoverFromAssemblies(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DiscoverFromAssemblies_WithEmptyAssemblies_Throws()
    {
        var registry = new DataSourceRegistry();
        var act = () => registry.DiscoverFromAssemblies();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterServices_RegistersDiscoveredTypes()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(FakeDataSource).Assembly);

        var services = new ServiceCollection();
        registry.RegisterServices(services);

        // Should register the concrete type and IDataSource
        services.Should().Contain(sd => sd.ServiceType == typeof(FakeDataSource));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDataSource));
    }

    [Fact]
    public void RegisterServices_WithNullServices_Throws()
    {
        var registry = new DataSourceRegistry();
        var act = () => registry.RegisterServices(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Test Types ──────────────────────────────────────────────────

    [DataSource("fake", "Fake Provider", DataSourceType.Historical, DataSourceCategory.Free,
        Description = "A fake provider for testing")]
    internal sealed class FakeDataSource : IDataSource
    {
        public string Id => "fake";
        public string DisplayName => "Fake Provider";
        public string Description => "A fake provider for testing";
        public DataSourceType Type => DataSourceType.Historical;
        public DataSourceCategory Category => DataSourceCategory.Free;
        public int Priority => 100;
        public DataSourceCapabilities Capabilities => DataSourceCapabilities.HistoricalDailyBars;
        public DataSourceCapabilityInfo CapabilityInfo => DataSourceCapabilityInfo.Default(Capabilities);
        public IReadOnlySet<string> SupportedMarkets => ImmutableHashSet.Create("US");
        public IReadOnlySet<AssetClass> SupportedAssetClasses => ImmutableHashSet.Create(AssetClass.Equity);
        public DataSourceHealth Health => DataSourceHealth.Healthy();
        public DataSourceStatus Status => DataSourceStatus.Connected;
        public RateLimitState RateLimitState => RateLimitState.Available;
        public IObservable<DataSourceHealthChanged> HealthChanges => System.Reactive.Linq.Observable.Empty<DataSourceHealthChanged>();
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ValidateCredentialsAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> TestConnectivityAsync(CancellationToken ct = default) => Task.FromResult(true);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [DataSource("abstract-test", "Abstract", DataSourceType.Realtime, DataSourceCategory.Free)]
    internal abstract class AbstractDataSource : IDataSource
    {
        public string Id => "abstract-test";
        public string DisplayName => "Abstract";
        public string Description => "";
        public DataSourceType Type => DataSourceType.Realtime;
        public DataSourceCategory Category => DataSourceCategory.Free;
        public int Priority => 100;
        public DataSourceCapabilities Capabilities => DataSourceCapabilities.None;
        public DataSourceCapabilityInfo CapabilityInfo => DataSourceCapabilityInfo.Default(Capabilities);
        public IReadOnlySet<string> SupportedMarkets => ImmutableHashSet<string>.Empty;
        public IReadOnlySet<AssetClass> SupportedAssetClasses => ImmutableHashSet<AssetClass>.Empty;
        public DataSourceHealth Health => DataSourceHealth.Healthy();
        public DataSourceStatus Status => DataSourceStatus.Uninitialized;
        public RateLimitState RateLimitState => RateLimitState.Available;
        public IObservable<DataSourceHealthChanged> HealthChanges => System.Reactive.Linq.Observable.Empty<DataSourceHealthChanged>();
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ValidateCredentialsAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> TestConnectivityAsync(CancellationToken ct = default) => Task.FromResult(true);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal sealed class NotADataSource { }

    [DataSource("no-iface", "No Interface", DataSourceType.Historical, DataSourceCategory.Free)]
    internal sealed class AttributedButNotDataSource { }
}
