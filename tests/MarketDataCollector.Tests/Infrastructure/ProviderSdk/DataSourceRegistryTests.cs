using System.Collections.Immutable;
using FluentAssertions;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.ProviderSdk;

/// <summary>
/// Tests for <see cref="DataSourceRegistry"/> — provider discovery and registration via reflection.
/// </summary>
public sealed class DataSourceRegistryTests
{
    #region DiscoverFromAssemblies

    [Fact]
    public void DiscoverFromAssemblies_WithNullAssemblies_ThrowsArgumentException()
    {
        var registry = new DataSourceRegistry();
        var action = () => registry.DiscoverFromAssemblies(null!);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DiscoverFromAssemblies_WithEmptyAssemblies_ThrowsArgumentException()
    {
        var registry = new DataSourceRegistry();
        var action = () => registry.DiscoverFromAssemblies();
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DiscoverFromAssemblies_DiscoversDecoratedDataSources()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);

        registry.Sources.Should().Contain(s => s.Id == "test-source");
    }

    [Fact]
    public void DiscoverFromAssemblies_IgnoresClassesWithoutAttribute()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(UnattributedDataSource).Assembly);

        registry.Sources.Should().NotContain(s => s.ImplementationType == typeof(UnattributedDataSource));
    }

    [Fact]
    public void DiscoverFromAssemblies_IgnoresAbstractClasses()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(AbstractDataSource).Assembly);

        registry.Sources.Should().NotContain(s => s.ImplementationType == typeof(AbstractDataSource));
    }

    [Fact]
    public void DiscoverFromAssemblies_PreventsDuplicateRegistration()
    {
        var registry = new DataSourceRegistry();

        // Discover from same assembly twice
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);

        registry.Sources.Count(s => s.Id == "test-source").Should().Be(1);
    }

    [Fact]
    public void DiscoverFromAssemblies_CapturesMetadataCorrectly()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);

        var metadata = registry.Sources.First(s => s.Id == "test-source");
        metadata.DisplayName.Should().Be("Test Source");
        metadata.Type.Should().Be(DataSourceType.Historical);
        metadata.Category.Should().Be(DataSourceCategory.Free);
        metadata.ImplementationType.Should().Be(typeof(TestDataSource));
    }

    #endregion

    #region RegisterServices

    [Fact]
    public void RegisterServices_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        var registry = new DataSourceRegistry();
        var action = () => registry.RegisterServices(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterServices_RegistersDiscoveredSourcesInDI()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);

        var services = new ServiceCollection();
        registry.RegisterServices(services);

        // Should register both the concrete type and IDataSource
        services.Should().Contain(sd => sd.ServiceType == typeof(TestDataSource));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDataSource));
    }

    [Fact]
    public void RegisterServices_UsesSpecifiedLifetime()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);

        var services = new ServiceCollection();
        registry.RegisterServices(services, ServiceLifetime.Transient);

        services.Where(sd => sd.ServiceType == typeof(TestDataSource))
            .Should().AllSatisfy(sd => sd.Lifetime.Should().Be(ServiceLifetime.Transient));
    }

    [Fact]
    public void RegisterServices_DefaultsToSingletonLifetime()
    {
        var registry = new DataSourceRegistry();
        registry.DiscoverFromAssemblies(typeof(TestDataSource).Assembly);

        var services = new ServiceCollection();
        registry.RegisterServices(services);

        services.Where(sd => sd.ServiceType == typeof(TestDataSource))
            .Should().AllSatisfy(sd => sd.Lifetime.Should().Be(ServiceLifetime.Singleton));
    }

    #endregion

    #region RegisterModules

    [Fact]
    public void RegisterModules_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        var registry = new DataSourceRegistry();
        var action = () => registry.RegisterModules(null!, typeof(TestProviderModule).Assembly);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterModules_DiscoverAndExecutesProviderModules()
    {
        var registry = new DataSourceRegistry();
        var services = new ServiceCollection();

        registry.RegisterModules(services, typeof(TestProviderModule).Assembly);

        // TestProviderModule registers a string service
        services.Should().Contain(sd => sd.ServiceType == typeof(string));
    }

    [Fact]
    public void RegisterModules_IgnoresAbstractModules()
    {
        var registry = new DataSourceRegistry();
        var services = new ServiceCollection();

        // Should not throw and should not register from abstract module
        var action = () => registry.RegisterModules(services, typeof(AbstractProviderModule).Assembly);
        action.Should().NotThrow();
    }

    #endregion

    #region Sources property

    [Fact]
    public void Sources_InitiallyEmpty()
    {
        var registry = new DataSourceRegistry();
        registry.Sources.Should().BeEmpty();
    }

    [Fact]
    public void Sources_ReturnsReadOnlyList()
    {
        var registry = new DataSourceRegistry();
        registry.Sources.Should().BeAssignableTo<IReadOnlyList<DataSourceMetadata>>();
    }

    #endregion

    #region DataSourceMetadata

    [Fact]
    public void DataSourceMetadata_IsRealtime_ForRealtimeType()
    {
        var metadata = new DataSourceMetadata(
            "rt", "Realtime", null, DataSourceType.Realtime, DataSourceCategory.Exchange,
            1, true, "rt", typeof(TestDataSource));
        metadata.IsRealtime.Should().BeTrue();
        metadata.IsHistorical.Should().BeFalse();
    }

    [Fact]
    public void DataSourceMetadata_IsHistorical_ForHistoricalType()
    {
        var metadata = new DataSourceMetadata(
            "hist", "Historical", null, DataSourceType.Historical, DataSourceCategory.Free,
            100, true, "hist", typeof(TestDataSource));
        metadata.IsRealtime.Should().BeFalse();
        metadata.IsHistorical.Should().BeTrue();
    }

    [Fact]
    public void DataSourceMetadata_IsBoth_ForHybridType()
    {
        var metadata = new DataSourceMetadata(
            "hybrid", "Hybrid", null, DataSourceType.Hybrid, DataSourceCategory.Broker,
            50, true, "hybrid", typeof(TestDataSource));
        metadata.IsRealtime.Should().BeTrue();
        metadata.IsHistorical.Should().BeTrue();
    }

    [Fact]
    public void DataSourceMetadata_FromAttribute_MapsCorrectly()
    {
        var attr = new DataSourceAttribute("test", "Test", DataSourceType.Historical, DataSourceCategory.Free)
        {
            Priority = 42,
            EnabledByDefault = false,
            Description = "A test source",
            ConfigSection = "custom-section"
        };

        var metadata = DataSourceMetadata.FromAttribute(attr, typeof(TestDataSource));

        metadata.Id.Should().Be("test");
        metadata.DisplayName.Should().Be("Test");
        metadata.Description.Should().Be("A test source");
        metadata.Type.Should().Be(DataSourceType.Historical);
        metadata.Category.Should().Be(DataSourceCategory.Free);
        metadata.Priority.Should().Be(42);
        metadata.EnabledByDefault.Should().BeFalse();
        metadata.ConfigSection.Should().Be("custom-section");
        metadata.ImplementationType.Should().Be(typeof(TestDataSource));
    }

    [Fact]
    public void DataSourceMetadata_FromAttribute_DefaultsConfigSectionToId()
    {
        var attr = new DataSourceAttribute("my-id", "My Source", DataSourceType.Realtime, DataSourceCategory.Premium);
        var metadata = DataSourceMetadata.FromAttribute(attr, typeof(TestDataSource));

        metadata.ConfigSection.Should().Be("my-id");
    }

    #endregion

    #region DataSourceAttributeExtensions

    [Fact]
    public void IsDataSource_ReturnsTrueForDecoratedConcreteClass()
    {
        typeof(TestDataSource).IsDataSource().Should().BeTrue();
    }

    [Fact]
    public void IsDataSource_ReturnsFalseForUnattributedClass()
    {
        typeof(UnattributedDataSource).IsDataSource().Should().BeFalse();
    }

    [Fact]
    public void IsDataSource_ReturnsFalseForAbstractClass()
    {
        typeof(AbstractDataSource).IsDataSource().Should().BeFalse();
    }

    [Fact]
    public void IsDataSource_ReturnsFalseForInterface()
    {
        typeof(IDataSource).IsDataSource().Should().BeFalse();
    }

    [Fact]
    public void GetDataSourceMetadata_ReturnsNullForUnattributedType()
    {
        typeof(string).GetDataSourceMetadata().Should().BeNull();
    }

    [Fact]
    public void GetDataSourceMetadata_ReturnsMetadataForDecoratedType()
    {
        var metadata = typeof(TestDataSource).GetDataSourceMetadata();
        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be("test-source");
    }

    #endregion

    #region Test fixtures

    [DataSource("test-source", "Test Source", DataSourceType.Historical, DataSourceCategory.Free)]
    private sealed class TestDataSource : IDataSource
    {
        public string Id => "test-source";
        public string DisplayName => "Test Source";
        public string Description => "A test data source for unit testing";
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

    /// <summary>
    /// A class implementing IDataSource but without the [DataSource] attribute.
    /// Should NOT be discovered by the registry.
    /// </summary>
    private sealed class UnattributedDataSource : IDataSource
    {
        public string Id => "unattributed";
        public string DisplayName => "Unattributed";
        public string Description => "No attribute";
        public DataSourceType Type => DataSourceType.Historical;
        public DataSourceCategory Category => DataSourceCategory.Free;
        public int Priority => 999;
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

    [DataSource("abstract-source", "Abstract", DataSourceType.Realtime, DataSourceCategory.Exchange)]
    private abstract class AbstractDataSource : IDataSource
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public DataSourceType Type => DataSourceType.Realtime;
        public DataSourceCategory Category => DataSourceCategory.Exchange;
        public int Priority => 1;
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

    private sealed class TestProviderModule : IProviderModule
    {
        public void Register(IServiceCollection services, DataSourceRegistry registry)
        {
            // Register a simple string service as evidence that this module was called
            services.AddSingleton("test-module-registered");
        }
    }

    private abstract class AbstractProviderModule : IProviderModule
    {
        public abstract void Register(IServiceCollection services, DataSourceRegistry registry);
    }

    #endregion
}
