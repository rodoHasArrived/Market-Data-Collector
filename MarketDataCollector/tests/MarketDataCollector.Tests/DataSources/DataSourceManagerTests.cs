using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Xunit;

namespace MarketDataCollector.Tests.DataSources;

public class DataSourceManagerTests
{
    [Fact]
    public void AllSources_ReturnsAllRegisteredSources()
    {
        // Arrange
        var sources = new IDataSource[]
        {
            new TestHistoricalDataSource("yahoo", 10),
            new TestHistoricalDataSource("stooq", 20),
            new TestRealtimeDataSource("alpaca", 5)
        };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.AllSources;

        // Assert
        result.Should().HaveCount(3);
        result.Select(s => s.Id).Should().Contain(new[] { "yahoo", "stooq", "alpaca" });
    }

    [Fact]
    public void AllSources_OrderedByPriority()
    {
        // Arrange
        var sources = new IDataSource[]
        {
            new TestHistoricalDataSource("yahoo", 20),
            new TestHistoricalDataSource("stooq", 10),
            new TestRealtimeDataSource("alpaca", 5)
        };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.AllSources;

        // Assert
        result[0].Id.Should().Be("alpaca");
        result[1].Id.Should().Be("stooq");
        result[2].Id.Should().Be("yahoo");
    }

    [Fact]
    public void RealtimeSources_ReturnsOnlyRealtimeSources()
    {
        // Arrange
        var sources = new IDataSource[]
        {
            new TestHistoricalDataSource("yahoo", 10),
            new TestRealtimeDataSource("alpaca", 5)
        };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.RealtimeSources;

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("alpaca");
    }

    [Fact]
    public void HistoricalSources_ReturnsOnlyHistoricalSources()
    {
        // Arrange
        var sources = new IDataSource[]
        {
            new TestHistoricalDataSource("yahoo", 10),
            new TestRealtimeDataSource("alpaca", 5)
        };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.HistoricalSources;

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("yahoo");
    }

    [Fact]
    public void GetSource_ReturnsSourceById()
    {
        // Arrange
        var sources = new IDataSource[]
        {
            new TestHistoricalDataSource("yahoo", 10),
            new TestHistoricalDataSource("stooq", 20)
        };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.GetSource("yahoo");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("yahoo");
    }

    [Fact]
    public void GetSource_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var sources = new IDataSource[] { new TestHistoricalDataSource("yahoo", 10) };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.GetSource("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSource_CaseInsensitive()
    {
        // Arrange
        var sources = new IDataSource[] { new TestHistoricalDataSource("Yahoo", 10) };
        var manager = new DataSourceManager(sources);

        // Act
        var result = manager.GetSource("YAHOO");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetSourcesWithCapability_ReturnsMatchingSources()
    {
        // Arrange
        var yahooSource = new TestHistoricalDataSource("yahoo", 10,
            DataSourceCapabilities.HistoricalDailyBars | DataSourceCapabilities.HistoricalAdjustedPrices);
        var stooqSource = new TestHistoricalDataSource("stooq", 20,
            DataSourceCapabilities.HistoricalDailyBars);

        var manager = new DataSourceManager(new IDataSource[] { yahooSource, stooqSource });

        // Act
        var result = manager.GetSourcesWithCapability(DataSourceCapabilities.HistoricalAdjustedPrices);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be("yahoo");
    }

    [Fact]
    public void GetSourcesForMarket_ReturnsMatchingSources()
    {
        // Arrange
        var usSource = new TestHistoricalDataSource("yahoo", 10, markets: new[] { "US", "UK" });
        var deSource = new TestHistoricalDataSource("xetra", 20, markets: new[] { "DE" });

        var manager = new DataSourceManager(new IDataSource[] { usSource, deSource });

        // Act
        var result = manager.GetSourcesForMarket("US");

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be("yahoo");
    }

    [Fact]
    public void GetSourcesForAssetClass_ReturnsMatchingSources()
    {
        // Arrange
        var equitySource = new TestHistoricalDataSource("yahoo", 10,
            assetClasses: new[] { AssetClass.Equity, AssetClass.ETF });
        var cryptoSource = new TestHistoricalDataSource("crypto", 20,
            assetClasses: new[] { AssetClass.Crypto });

        var manager = new DataSourceManager(new IDataSource[] { equitySource, cryptoSource });

        // Act
        var result = manager.GetSourcesForAssetClass(AssetClass.Equity);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be("yahoo");
    }

    [Fact]
    public void GetBestHistoricalSource_ReturnsHealthySourceWithLowestPriority()
    {
        // Arrange
        var source1 = new TestHistoricalDataSource("yahoo", 10, isHealthy: true);
        var source2 = new TestHistoricalDataSource("stooq", 5, isHealthy: true);

        var manager = new DataSourceManager(new IDataSource[] { source1, source2 });

        // Act
        var result = manager.GetBestHistoricalSource("SPY");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("stooq"); // Lower priority value = higher priority
    }

    [Fact]
    public void GetBestHistoricalSource_SkipsUnhealthySources()
    {
        // Arrange
        var unhealthySource = new TestHistoricalDataSource("stooq", 5, isHealthy: false);
        var healthySource = new TestHistoricalDataSource("yahoo", 10, isHealthy: true);

        var manager = new DataSourceManager(new IDataSource[] { unhealthySource, healthySource });

        // Act
        var result = manager.GetBestHistoricalSource("SPY");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("yahoo");
    }

    [Fact]
    public void GetAggregatedHealth_ReturnsCorrectSummary()
    {
        // Arrange
        var healthySource = new TestHistoricalDataSource("yahoo", 10, isHealthy: true);
        var unhealthySource = new TestHistoricalDataSource("stooq", 20, isHealthy: false);

        var manager = new DataSourceManager(new IDataSource[] { healthySource, unhealthySource });

        // Act
        var result = manager.GetAggregatedHealth();

        // Assert
        result.TotalSources.Should().Be(2);
        result.HealthySources.Should().Be(1);
        result.UnhealthySources.Should().Be(1);
        result.IsHealthy.Should().BeTrue(); // At least one source healthy
    }

    [Fact]
    public void GetAggregatedHealth_UnhealthyWhenNoSources()
    {
        // Arrange
        var manager = new DataSourceManager(Array.Empty<IDataSource>());

        // Act
        var result = manager.GetAggregatedHealth();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.TotalSources.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAllAsync_ReturnsValidationResults()
    {
        // Arrange
        var validSource = new TestHistoricalDataSource("yahoo", 10, credentialsValid: true);
        var invalidSource = new TestHistoricalDataSource("stooq", 20, credentialsValid: false);

        var manager = new DataSourceManager(new IDataSource[] { validSource, invalidSource });

        // Act
        var result = await manager.ValidateAllAsync();

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().Contain(e => e.SourceId == "yahoo" && e.IsValid);
        result.Entries.Should().Contain(e => e.SourceId == "stooq" && !e.IsValid);
        result.AllValid.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectivityAsync_ReturnsConnectivityResults()
    {
        // Arrange
        var connectedSource = new TestHistoricalDataSource("yahoo", 10, isConnected: true);
        var disconnectedSource = new TestHistoricalDataSource("stooq", 20, isConnected: false);

        var manager = new DataSourceManager(new IDataSource[] { connectedSource, disconnectedSource });

        // Act
        var result = await manager.TestConnectivityAsync();

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().Contain(e => e.SourceId == "yahoo" && e.IsConnected);
        result.Entries.Should().Contain(e => e.SourceId == "stooq" && !e.IsConnected);
        result.AllConnected.Should().BeFalse();
    }
}

#region Test Implementations

internal class TestHistoricalDataSource : IHistoricalDataSource
{
    private readonly bool _isHealthy;
    private readonly bool _credentialsValid;
    private readonly bool _isConnected;
    private readonly DataSourceCapabilities _capabilities;

    public TestHistoricalDataSource(
        string id,
        int priority,
        DataSourceCapabilities capabilities = DataSourceCapabilities.HistoricalDailyBars,
        string[]? markets = null,
        AssetClass[]? assetClasses = null,
        bool isHealthy = true,
        bool credentialsValid = true,
        bool isConnected = true)
    {
        Id = id;
        Priority = priority;
        _capabilities = capabilities;
        SupportedMarkets = (markets ?? new[] { "US" }).ToHashSet();
        SupportedAssetClasses = (assetClasses ?? new[] { AssetClass.Equity }).ToHashSet();
        _isHealthy = isHealthy;
        _credentialsValid = credentialsValid;
        _isConnected = isConnected;
    }

    public string Id { get; }
    public string DisplayName => Id;
    public string Description => $"Test source: {Id}";
    public DataSourceType Type => DataSourceType.Historical;
    public DataSourceCategory Category => DataSourceCategory.Free;
    public int Priority { get; }
    public DataSourceCapabilities Capabilities => _capabilities;
    public DataSourceCapabilityInfo CapabilityInfo => DataSourceCapabilityInfo.Default(_capabilities);
    public IReadOnlySet<string> SupportedMarkets { get; }
    public IReadOnlySet<AssetClass> SupportedAssetClasses { get; }
    public DataSourceHealth Health => _isHealthy ? DataSourceHealth.Healthy() : DataSourceHealth.Unhealthy("Test failure");
    public DataSourceStatus Status => DataSourceStatus.Connected;
    public RateLimitState RateLimitState => RateLimitState.Available;
    public IObservable<DataSourceHealthChanged> HealthChanges => System.Reactive.Linq.Observable.Empty<DataSourceHealthChanged>();
    public bool SupportsIntraday => false;
    public IReadOnlyList<string> SupportedBarIntervals => Array.Empty<string>();
    public bool SupportsDividends => false;
    public bool SupportsSplits => false;

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ValidateCredentialsAsync(CancellationToken ct = default) => Task.FromResult(_credentialsValid);
    public Task<bool> TestConnectivityAsync(CancellationToken ct = default) => Task.FromResult(_isConnected);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<HistoricalBar>>(Array.Empty<HistoricalBar>());

    public Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(string symbol, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AdjustedHistoricalBar>>(Array.Empty<AdjustedHistoricalBar>());

    public Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(string symbol, string interval, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<IntradayBar>>(Array.Empty<IntradayBar>());

    public Task<IReadOnlyList<DividendInfo>> GetDividendsAsync(string symbol, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DividendInfo>>(Array.Empty<DividendInfo>());

    public Task<IReadOnlyList<SplitInfo>> GetSplitsAsync(string symbol, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SplitInfo>>(Array.Empty<SplitInfo>());
}

internal class TestRealtimeDataSource : IRealtimeDataSource
{
    public TestRealtimeDataSource(string id, int priority)
    {
        Id = id;
        Priority = priority;
    }

    public string Id { get; }
    public string DisplayName => Id;
    public string Description => $"Test realtime source: {Id}";
    public DataSourceType Type => DataSourceType.Realtime;
    public DataSourceCategory Category => DataSourceCategory.Broker;
    public int Priority { get; }
    public DataSourceCapabilities Capabilities => DataSourceCapabilities.RealtimeTrades | DataSourceCapabilities.RealtimeQuotes;
    public DataSourceCapabilityInfo CapabilityInfo => DataSourceCapabilityInfo.Default(Capabilities);
    public IReadOnlySet<string> SupportedMarkets => new HashSet<string> { "US" };
    public IReadOnlySet<AssetClass> SupportedAssetClasses => new HashSet<AssetClass> { AssetClass.Equity };
    public DataSourceHealth Health => DataSourceHealth.Healthy();
    public DataSourceStatus Status => DataSourceStatus.Connected;
    public RateLimitState RateLimitState => RateLimitState.Available;
    public IObservable<DataSourceHealthChanged> HealthChanges => System.Reactive.Linq.Observable.Empty<DataSourceHealthChanged>();
    public bool IsConnected => true;
    public IObservable<RealtimeTrade> Trades => System.Reactive.Linq.Observable.Empty<RealtimeTrade>();
    public IObservable<RealtimeQuote> Quotes => System.Reactive.Linq.Observable.Empty<RealtimeQuote>();
    public IObservable<RealtimeDepthUpdate> DepthUpdates => System.Reactive.Linq.Observable.Empty<RealtimeDepthUpdate>();
    public IReadOnlySet<int> ActiveSubscriptions => new HashSet<int>();
    public IReadOnlySet<string> SubscribedSymbols => new HashSet<string>();

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ValidateCredentialsAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> TestConnectivityAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public int SubscribeTrades(SymbolConfig config) => 1;
    public void UnsubscribeTrades(int subscriptionId) { }
    public int SubscribeQuotes(SymbolConfig config) => 1;
    public void UnsubscribeQuotes(int subscriptionId) { }
    public int SubscribeMarketDepth(SymbolConfig config) => 1;
    public void UnsubscribeMarketDepth(int subscriptionId) { }
    public void UnsubscribeAll() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#endregion
