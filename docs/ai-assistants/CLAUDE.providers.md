# CLAUDE.providers.md - Data Provider Implementation Guide

This document provides guidance for AI assistants working with data providers in Market Data Collector.

---

## Provider Architecture Overview

The system uses a unified abstraction layer supporting both **real-time streaming** and **historical data** providers.

```
┌──────────────────────────────────────────────────────────────┐
│                    Provider Abstraction                       │
│  IDataSource (base) → IRealtimeDataSource / IHistoricalDataSource │
└──────────────────────────────────────────────────────────────┘
           │                                    │
           ▼                                    ▼
┌─────────────────────┐              ┌─────────────────────────┐
│ Streaming Providers │              │ Historical Providers    │
│ ├─ Alpaca           │              │ ├─ Alpaca               │
│ ├─ Interactive Brkrs│              │ ├─ Yahoo Finance        │
│ ├─ StockSharp       │              │ ├─ Stooq                │
│ ├─ NYSE Direct      │              │ ├─ Tiingo               │
│ └─ Polygon (stub)   │              │ ├─ Finnhub              │
└─────────────────────┘              │ ├─ Alpha Vantage        │
                                     │ ├─ Nasdaq Data Link     │
                                     │ └─ Polygon              │
                                     └─────────────────────────┘
```

---

## File Locations

### Core Abstractions
| File | Purpose |
|------|---------|
| `Infrastructure/DataSources/IDataSource.cs` | Base interface for all data sources |
| `Infrastructure/DataSources/IRealtimeDataSource.cs` | Real-time streaming extension |
| `Infrastructure/DataSources/IHistoricalDataSource.cs` | Historical data retrieval |
| `Infrastructure/DataSources/DataSourceAttribute.cs` | Attribute for auto-discovery |
| `Infrastructure/DataSources/DataSourceManager.cs` | Provider lifecycle management |

### Streaming Providers
| Provider | Location |
|----------|----------|
| Alpaca | `Infrastructure/Providers/Alpaca/AlpacaMarketDataClient.cs` |
| Interactive Brokers | `Infrastructure/Providers/InteractiveBrokers/IBMarketDataClient.cs` |
| StockSharp | `Infrastructure/Providers/StockSharp/StockSharpMarketDataClient.cs` |
| NYSE | `Infrastructure/Providers/NYSE/NYSEDataSource.cs` |
| Polygon | `Infrastructure/Providers/Polygon/PolygonMarketDataClient.cs` |

### Historical Providers
| Provider | Location |
|----------|----------|
| Composite (failover) | `Infrastructure/Providers/Backfill/CompositeHistoricalDataProvider.cs` |
| Alpaca | `Infrastructure/Providers/Backfill/AlpacaHistoricalDataProvider.cs` |
| Yahoo Finance | `Infrastructure/Providers/Backfill/YahooFinanceHistoricalDataProvider.cs` |
| Stooq | `Infrastructure/Providers/Backfill/StooqHistoricalDataProvider.cs` |
| Tiingo | `Infrastructure/Providers/Backfill/TiingoHistoricalDataProvider.cs` |
| Finnhub | `Infrastructure/Providers/Backfill/FinnhubHistoricalDataProvider.cs` |
| Alpha Vantage | `Infrastructure/Providers/Backfill/AlphaVantageHistoricalDataProvider.cs` |
| Nasdaq Data Link | `Infrastructure/Providers/Backfill/NasdaqDataLinkHistoricalDataProvider.cs` |
| Polygon | `Infrastructure/Providers/Backfill/PolygonHistoricalDataProvider.cs` |

### Resilience Infrastructure
| File | Purpose |
|------|---------|
| `Infrastructure/Providers/Resilience/CircuitBreaker.cs` | Open/Closed/HalfOpen states |
| `Infrastructure/Providers/Resilience/ConcurrentProviderExecutor.cs` | Parallel provider operations |
| `Infrastructure/Providers/Backfill/RateLimiter.cs` | Per-provider rate limiting |
| `Infrastructure/Providers/Backfill/DataGapRepair.cs` | Automatic gap detection/repair |
| `Infrastructure/Providers/Backfill/DataQualityMonitor.cs` | Multi-dimensional quality scoring |

---

## Key Interfaces

### IDataSource (Base Interface)

All providers implement this base interface:

```csharp
public interface IDataSource : IAsyncDisposable
{
    // Identity
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }

    // Classification
    DataSourceType Type { get; }        // Realtime, Historical, Hybrid
    DataSourceCategory Category { get; } // Exchange, Broker, Aggregator, Free, Premium
    int Priority { get; }                // Lower = higher priority

    // Capabilities
    DataSourceCapabilities Capabilities { get; }
    IReadOnlySet<string> SupportedMarkets { get; }
    IReadOnlySet<AssetClass> SupportedAssetClasses { get; }

    // Health & Status
    DataSourceHealth Health { get; }
    DataSourceStatus Status { get; }
    RateLimitState RateLimitState { get; }

    // Lifecycle
    Task InitializeAsync(CancellationToken ct = default);
    Task<bool> ValidateCredentialsAsync(CancellationToken ct = default);
    Task<bool> TestConnectivityAsync(CancellationToken ct = default);
}
```

### DataSourceCapabilities (Bitwise Flags)

```csharp
[Flags]
public enum DataSourceCapabilities : long
{
    // Real-time (bits 0-9)
    RealtimeTrades = 1L << 0,
    RealtimeQuotes = 1L << 1,
    RealtimeDepthL1 = 1L << 2,
    RealtimeDepthL2 = 1L << 3,
    RealtimeDepthL3 = 1L << 4,

    // Historical (bits 10-19)
    HistoricalDailyBars = 1L << 10,
    HistoricalIntradayBars = 1L << 11,
    HistoricalTicks = 1L << 12,
    HistoricalAdjustedPrices = 1L << 13,
    HistoricalDividends = 1L << 14,
    HistoricalSplits = 1L << 15,

    // Operational (bits 20-29)
    SupportsBackfill = 1L << 20,
    SupportsStreaming = 1L << 21,
    SupportsWebSocket = 1L << 23,
    SupportsBatchRequests = 1L << 24,

    // Quality (bits 30-39)
    ExchangeTimestamps = 1L << 30,
    SequenceNumbers = 1L << 31,
    TradeConditions = 1L << 32,
}
```

### IHistoricalDataProvider

```csharp
public interface IHistoricalDataProvider
{
    string Name { get; }
    int Priority { get; }
    bool SupportsSymbol(string symbol);

    Task<IReadOnlyList<HistoricalBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default);

    IAsyncEnumerable<HistoricalBar> StreamHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default);
}
```

---

## Adding a New Streaming Provider

### Step 1: Create the Provider Class

```csharp
// Location: Infrastructure/Providers/{ProviderName}/{ProviderName}DataSource.cs

[DataSource(
    "myprovider",
    "My Provider",
    DataSourceType.Realtime,
    DataSourceCategory.Broker,
    Priority = 50,
    Description = "My custom data provider")]
public sealed class MyProviderDataSource : IRealtimeDataSource
{
    private readonly ILogger<MyProviderDataSource> _logger;
    private readonly MyProviderOptions _options;
    private readonly Channel<MarketDataEvent> _events;

    public MyProviderDataSource(
        ILogger<MyProviderDataSource> logger,
        IOptions<MyProviderOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _events = Channel.CreateBounded<MarketDataEvent>(10_000);
    }

    // Implement IDataSource properties...

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Connecting to {Provider}", DisplayName);
        // Connection logic...
    }

    public async IAsyncEnumerable<MarketDataEvent> GetEventsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _events.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    // Implement remaining interface methods...
}
```

### Step 2: Create Options Class

```csharp
// Location: Infrastructure/Providers/{ProviderName}/{ProviderName}Options.cs

public sealed class MyProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "wss://api.myprovider.com/stream";
    public int ReconnectDelayMs { get; set; } = 5000;
    public int MaxReconnectAttempts { get; set; } = 10;
}
```

### Step 3: Add Configuration Section

In `appsettings.sample.json`:

```json
{
  "MyProvider": {
    "ApiKey": "",
    "ApiSecret": "",
    "Endpoint": "wss://api.myprovider.com/stream",
    "ReconnectDelayMs": 5000
  }
}
```

### Step 4: Register in DI

```csharp
// In Program.cs or a ServiceExtensions file
services.Configure<MyProviderOptions>(
    configuration.GetSection("MyProvider"));
services.AddSingleton<IRealtimeDataSource, MyProviderDataSource>();
```

### Step 5: Add Tests

```csharp
// Location: tests/MarketDataCollector.Tests/Providers/MyProviderTests.cs

public class MyProviderDataSourceTests
{
    [Fact]
    public async Task ConnectAsync_WithValidCredentials_Succeeds()
    {
        // Arrange
        var options = Options.Create(new MyProviderOptions { ApiKey = "test" });
        var logger = NullLogger<MyProviderDataSource>.Instance;
        var sut = new MyProviderDataSource(logger, options);

        // Act
        await sut.ConnectAsync();

        // Assert
        sut.Status.Should().Be(DataSourceStatus.Connected);
    }
}
```

---

## Adding a New Historical Provider

### Step 1: Create the Provider Class

```csharp
// Location: Infrastructure/Providers/Backfill/{ProviderName}HistoricalDataProvider.cs

public sealed class MyHistoricalProvider : IHistoricalDataProvider
{
    private readonly ILogger<MyHistoricalProvider> _logger;
    private readonly HttpClient _httpClient;

    public string Name => "myprovider";
    public int Priority => 50; // Lower = tried first

    public MyHistoricalProvider(
        ILogger<MyHistoricalProvider> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public bool SupportsSymbol(string symbol)
    {
        // Return true if this provider can handle the symbol
        return !symbol.Contains(":");  // e.g., exclude forex pairs
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Fetching {Symbol} bars from {Start} to {End}",
            symbol, start, end);

        // API call logic...
        var url = BuildUrl(symbol, start, end, timeframe);
        var response = await _httpClient.GetFromJsonAsync<ApiResponse>(url, ct);

        return response.Data
            .Select(bar => new HistoricalBar(
                Timestamp: bar.Date,
                Symbol: symbol,
                Open: bar.Open,
                High: bar.High,
                Low: bar.Low,
                Close: bar.Close,
                Volume: bar.Volume,
                Provider: Name))
            .ToList();
    }

    public async IAsyncEnumerable<HistoricalBar> StreamHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // For providers that support streaming, implement here
        // Otherwise, fetch all and yield
        var bars = await GetHistoricalBarsAsync(symbol, start, end, timeframe, ct);
        foreach (var bar in bars)
        {
            yield return bar;
        }
    }
}
```

### Step 2: Register with Composite Provider

```csharp
// In CompositeHistoricalDataProvider or DI setup
services.AddSingleton<IHistoricalDataProvider, MyHistoricalProvider>();
```

---

## Provider Priority System

Providers are tried in priority order (lower number = higher priority):

| Priority | Provider | Notes |
|----------|----------|-------|
| 5 | NYSE Direct | Exchange-direct, highest quality |
| 5 | Alpaca | High quality, unlimited free |
| 10 | Interactive Brokers | Requires subscription |
| 20 | Yahoo Finance | Free, 50K+ securities |
| 30 | Stooq | US equities EOD |
| 40 | Tiingo | Best for dividend-adjusted |
| 50 | Finnhub | Includes fundamentals |
| 60 | Alpha Vantage | Limited free tier |
| 70 | Nasdaq Data Link | Alternative datasets |
| 80 | Polygon | High-quality tick data |

---

## Circuit Breaker Pattern

All providers use circuit breakers for resilience:

```csharp
public sealed class CircuitBreaker
{
    public CircuitState State { get; }  // Closed, Open, HalfOpen

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        if (State == CircuitState.Open)
        {
            if (!ShouldAttemptReset())
                throw new CircuitBreakerOpenException();

            State = CircuitState.HalfOpen;
        }

        try
        {
            var result = await action(ct);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
    }
}
```

### Circuit Breaker States

| State | Description |
|-------|-------------|
| **Closed** | Normal operation, requests flow through |
| **Open** | Circuit tripped, requests fail fast |
| **HalfOpen** | Testing if service recovered |

---

## Rate Limiting

### Per-Provider Rate Limits

```csharp
public sealed class RateLimiter
{
    private readonly int _maxRequestsPerMinute;
    private readonly int _maxRequestsPerHour;
    private readonly int _maxRequestsPerDay;

    public async Task<bool> TryAcquireAsync(CancellationToken ct = default)
    {
        // Check all rate limit windows
        if (await _minuteWindow.TryAcquireAsync(ct) &&
            await _hourWindow.TryAcquireAsync(ct) &&
            await _dayWindow.TryAcquireAsync(ct))
        {
            return true;
        }
        return false;
    }

    public TimeSpan? GetRetryAfter()
    {
        // Return time until next available request
    }
}
```

### Rate Limit Configuration

```json
{
  "Providers": {
    "AlphaVantage": {
      "MaxRequestsPerMinute": 5,
      "MaxRequestsPerDay": 500
    },
    "YahooFinance": {
      "MaxRequestsPerMinute": 100,
      "MaxRequestsPerHour": 2000
    }
  }
}
```

---

## Data Quality Monitoring

### Quality Dimensions

| Dimension | Weight | Description |
|-----------|--------|-------------|
| Completeness | 30% | No missing data points |
| Accuracy | 25% | Prices within expected range |
| Timeliness | 20% | Data delivered on time |
| Consistency | 15% | No sequence gaps |
| Validity | 10% | Schema conformance |

### Quality Grades

| Grade | Score | Action |
|-------|-------|--------|
| A+ | 95-100 | Optimal |
| A | 90-94 | Good |
| B | 80-89 | Acceptable |
| C | 70-79 | Warning |
| D | 60-69 | Alert |
| F | <60 | Switch provider |

---

## Common Patterns

### Credential Validation

```csharp
public async Task<bool> ValidateCredentialsAsync(CancellationToken ct = default)
{
    if (string.IsNullOrEmpty(_options.ApiKey))
    {
        _logger.LogWarning("API key not configured for {Provider}", DisplayName);
        return false;
    }

    try
    {
        // Make a lightweight API call to validate
        await _httpClient.GetAsync("/v1/account", ct);
        return true;
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
        _logger.LogError("Invalid credentials for {Provider}", DisplayName);
        return false;
    }
}
```

### Reconnection with Exponential Backoff

```csharp
private async Task ReconnectWithBackoffAsync(CancellationToken ct)
{
    var attempt = 0;
    var maxAttempts = _options.MaxReconnectAttempts;

    while (attempt < maxAttempts)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        _logger.LogInformation(
            "Reconnecting to {Provider} in {Delay}s (attempt {Attempt}/{Max})",
            DisplayName, delay.TotalSeconds, attempt + 1, maxAttempts);

        await Task.Delay(delay, ct);

        try
        {
            await ConnectAsync(ct);
            _logger.LogInformation("Reconnected to {Provider}", DisplayName);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", attempt + 1);
            attempt++;
        }
    }

    throw new ReconnectionFailedException(DisplayName, maxAttempts);
}
```

---

## Testing Providers

### Unit Test Template

```csharp
public class MyProviderTests
{
    private readonly Mock<ILogger<MyProvider>> _logger;
    private readonly Mock<HttpMessageHandler> _httpHandler;
    private readonly MyProvider _sut;

    public MyProviderTests()
    {
        _logger = new Mock<ILogger<MyProvider>>();
        _httpHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpHandler.Object);
        _sut = new MyProvider(_logger.Object, httpClient);
    }

    [Fact]
    public async Task GetHistoricalBarsAsync_ReturnsData()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, SampleBarsJson);

        // Act
        var bars = await _sut.GetHistoricalBarsAsync(
            "AAPL",
            DateTime.Today.AddDays(-7),
            DateTime.Today,
            BarTimeframe.Daily);

        // Assert
        bars.Should().NotBeEmpty();
        bars.Should().AllSatisfy(b => b.Provider.Should().Be("myprovider"));
    }
}
```

---

## Environment Variables

Provider credentials should be set via environment variables:

```bash
# Alpaca
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key

# Interactive Brokers
export IB__HOST=127.0.0.1
export IB__PORT=7496
export IB__CLIENTID=1

# NYSE
export NYSE__APIKEY=your-api-key

# Alpha Vantage
export ALPHAVANTAGE__APIKEY=your-key

# Tiingo
export TIINGO__APIKEY=your-key
```

---

## Related Documentation

- [docs/providers/provider-comparison.md](MarketDataCollector/docs/providers/provider-comparison.md) - Provider feature matrix
- [docs/providers/backfill-guide.md](MarketDataCollector/docs/providers/backfill-guide.md) - Historical backfill guide
- [docs/providers/interactive-brokers-setup.md](MarketDataCollector/docs/providers/interactive-brokers-setup.md) - IB setup
- [docs/providers/alpaca-setup.md](MarketDataCollector/docs/providers/alpaca-setup.md) - Alpaca setup
- [docs/architecture/provider-management.md](MarketDataCollector/docs/architecture/provider-management.md) - Provider architecture

---

*Last Updated: 2026-01-30*
