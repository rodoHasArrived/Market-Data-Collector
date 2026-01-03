# Market Data Collector - Architecture Audit Report

**Audit Date:** 2026-01-03
**Version Audited:** v1.1.0
**Target Platform:** Windows Desktop Application
**Status:** Ready for Testing (with identified issues to address)

---

## Executive Summary

The Market Data Collector codebase has undergone significant improvements and demonstrates a mature, well-architected system. The architecture follows proper layered design patterns with clear separation of concerns. However, several integration gaps must be addressed before proceeding to Windows desktop testing.

### Overall Assessment: **READY FOR TESTING** (with caveats)

| Category | Status | Notes |
|----------|--------|-------|
| Core Library | ✅ Production Ready | Solid architecture, good patterns |
| Provider Integration | ✅ Well Designed | Registry pattern, capability-based |
| Event Pipeline | ✅ High Performance | System.Threading.Channels, backpressure |
| Storage Layer | ✅ Flexible | JSONL, Parquet, tiered storage |
| MassTransit Messaging | ✅ Complete | RabbitMQ, Azure Service Bus support |
| Microservices | ⚠️ Partial | Contracts defined, services scaffolded |
| UWP Desktop App | ⚠️ Integration Gaps | API endpoint mismatches |
| Windows Build | ✅ Configured | Native + Docker deployment ready |

---

## Critical Integration Issues

### Issue 1: UWP API Endpoint Mismatch (HIGH PRIORITY)

**Location:** `src/MarketDataCollector.Uwp/Services/StatusService.cs:25`

**Problem:** The UWP application expects API endpoints at `/api/status` and `/api/backfill/*`, but the core `StatusHttpServer` uses `/status` without the `/api/` prefix.

**UWP StatusService:**
```csharp
_statusUrl = $"{baseUrl}/api/status";  // Line 25
```

**Core StatusHttpServer routes:**
```csharp
case "status":  // Line 86 - expects /status, not /api/status
```

**Impact:** UWP desktop application cannot communicate with the core service.

**Fix Required:** Either:
1. Update `StatusHttpServer.cs` to support `/api/*` routes, OR
2. Update `StatusService.cs` to use `/status` endpoint

---

### Issue 2: UWP Project Missing Core Library Reference (HIGH PRIORITY)

**Location:** `src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj`

**Problem:** The UWP project has no `<ProjectReference>` to the core `MarketDataCollector` library. The UWP app duplicates domain models in `Models/AppConfig.cs` instead of sharing types with the core library.

**Current State:**
- UWP duplicates: `AppConfig`, `SymbolConfig`, `StorageConfig`, `BackfillConfig`
- Core defines: Same types in `Application/Config/AppConfig.cs`

**Impact:**
- Configuration model drift between UWP and core
- Maintenance burden of keeping two copies in sync
- Potential serialization compatibility issues

**Recommendation:**
1. Create a shared `MarketDataCollector.Contracts` library for common types
2. Reference shared library from both UWP and core projects

---

### Issue 3: StatusResponse Model Mismatch (MEDIUM PRIORITY)

**Location:** `src/MarketDataCollector.Uwp/Models/AppConfig.cs:251-262`

**Problem:** The UWP `StatusResponse` model expects an `isConnected` property, but the core `StatusHttpServer` returns a different structure:

**UWP expects:**
```csharp
public class StatusResponse
{
    public bool IsConnected { get; set; }
    public DateTime TimestampUtc { get; set; }
    public MetricsData? Metrics { get; set; }
}
```

**Core returns:**
```json
{
    "timestampUtc": "...",
    "metrics": { ... },
    "pipeline": { ... },
    "integrity": [ ... ]
}
```

**Impact:** UWP cannot properly determine connection status.

---

## Architecture Strengths

### 1. Provider Abstraction Layer (Excellent)

The provider system is well-designed with:
- `IDataProvider` - Core contract for all providers
- `IStreamingDataProvider` - Real-time streaming extension
- `IHistoricalDataProvider` - Historical data retrieval
- `IUnifiedDataProvider` - Combined streaming + historical
- `ProviderRegistry` - Dynamic discovery and registration
- `ProviderCapabilities` - Feature flags for provider selection

**Files:**
- `Infrastructure/Providers/Abstractions/IDataProvider.cs`
- `Infrastructure/Providers/Abstractions/ProviderRegistry.cs`
- `Infrastructure/Providers/Abstractions/ProviderCapabilities.cs`

### 2. Event Pipeline (High Performance)

The `EventPipeline` class is production-grade:
- System.Threading.Channels for high-throughput
- Bounded capacity with configurable backpressure (DropOldest)
- Performance metrics (processing time, queue depth)
- Periodic and on-demand flushing

**Key Features:**
- 100,000 event capacity
- Nanosecond precision timing
- Thread-safe statistics

### 3. MassTransit Integration (Complete)

Full distributed messaging support:
- InMemory (development)
- RabbitMQ (production)
- Azure Service Bus (cloud)
- Retry policies with exponential backoff
- Consumer registration for all event types

### 4. Storage Layer (Flexible)

Multiple storage strategies:
- JSONL (append-only, human-readable)
- Parquet (columnar, compressed)
- Tiered storage with migration
- Configurable naming conventions

---

## Component Integration Map

```
┌─────────────────────────────────────────────────────────────────────┐
│                     UWP DESKTOP APPLICATION                         │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │
│  │ ConfigService│  │StatusService │  │ BackfillService          │   │
│  │(file-based) │  │(HTTP client) │  │ (HTTP client)            │   │
│  └─────────────┘  └──────┬───────┘  └─────────────┬────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                           │                        │
                    /api/status (MISMATCH!)   /api/backfill/*
                           │                        │
                           ▼                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      CORE APPLICATION                                │
│                                                                      │
│  ┌──────────────────┐      ┌─────────────────────────────────────┐  │
│  │ StatusHttpServer │◄────►│ EventPipeline                       │  │
│  │ /status /metrics │      │ System.Threading.Channels           │  │
│  │ /health /ready   │      │ 100K capacity                       │  │
│  └────────┬─────────┘      └─────────────┬───────────────────────┘  │
│           │                              │                          │
│           │                              ▼                          │
│  ┌────────┴───────────────────────────────────────────────────────┐ │
│  │                    DOMAIN COLLECTORS                            │ │
│  │  TradeDataCollector │ MarketDepthCollector │ QuoteCollector     │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│           │                              │                          │
│           ▼                              ▼                          │
│  ┌───────────────────┐      ┌──────────────────────────────────┐   │
│  │ CompositePublisher│      │ IStorageSink                      │   │
│  │ ├─PipelinePublisher│     │ ├─JsonlStorageSink               │   │
│  │ └─MassTransitPub  │      │ └─ParquetStorageSink             │   │
│  └─────────┬─────────┘      └──────────────────────────────────┘   │
│            │                                                        │
└────────────┼────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      MESSAGING LAYER                                 │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ MassTransit                                                    │  │
│  │ ├─InMemory (dev)                                              │  │
│  │ ├─RabbitMQ (prod)                                             │  │
│  │ └─Azure Service Bus (cloud)                                   │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Windows Desktop Testing Readiness

### Build Configuration ✅

The project is properly configured for Windows:

**Main project (`MarketDataCollector.csproj`):**
- `PublishSingleFile=true` - Single executable deployment
- `SelfContained=true` - No .NET runtime required
- `RuntimeIdentifiers=win-x64;win-arm64` - Windows support
- `PublishReadyToRun=true` - Fast startup

**UWP project (`MarketDataCollector.Uwp.csproj`):**
- `net8.0-windows10.0.19041.0` - Windows 10 target
- `UseWinUI=true` - WinUI 3 framework
- `EnableMsixTooling=true` - MSIX packaging support
- Platforms: x86, x64, ARM64

### Installation Scripts ✅

Windows installation is well-supported:
- `install.ps1` - Interactive PowerShell installer
- Prerequisites check (Docker, .NET SDK, Git)
- Native and Docker deployment modes
- Self-test execution after install

### Dependencies ✅

All NuGet packages are compatible:
- .NET 8.0 - Latest LTS
- WinUI 3 (Microsoft.WindowsAppSDK 1.4.x)
- CommunityToolkit.Mvvm 8.2.2

---

## Microservices Architecture Status

### Defined Services (6 total):

| Service | Port | Status | Notes |
|---------|------|--------|-------|
| Gateway | 5000 | ⚠️ Scaffolded | API routing, rate limiting |
| TradeService | 5001 | ⚠️ Scaffolded | Trade processing |
| OrderBookService | 5002 | ⚠️ Scaffolded | L2 order book |
| QuoteService | 5003 | ⚠️ Scaffolded | BBO/NBBO quotes |
| HistoricalService | 5004 | ⚠️ Scaffolded | Backfill management |
| ValidationService | 5005 | ⚠️ Scaffolded | Data quality |

### Contracts (Complete) ✅

All message interfaces defined in `DataIngestion.Contracts`:
- `IRawTradeIngested`, `IValidatedTrade`
- `IOrderBookSnapshot`, `IOrderBookDelta`
- `IQuoteUpdated`, `INbboUpdated`
- `IHistoricalDataRequest`, `IBackfillProgress`
- `IValidationResult`, `IDataQualityAlert`

---

## Recommendations for Testing Phase

### Priority 1: Fix UWP Integration (Before Testing)

1. **Update StatusHttpServer to support /api/* routes:**

```csharp
// In StatusHttpServer.HandleRequestAsync
var path = ctx.Request.Url?.AbsolutePath?.Trim('/')?.ToLowerInvariant() ?? string.Empty;

// Add API prefix handling
if (path.StartsWith("api/"))
    path = path.Substring(4);  // Remove "api/" prefix
```

2. **Add isConnected field to status response:**

```csharp
var payload = new
{
    isConnected = true,  // Add this field
    timestampUtc = DateTimeOffset.UtcNow,
    metrics = _metricsProvider(),
    // ...
};
```

### Priority 2: Create Shared Contracts Library

Create `MarketDataCollector.Contracts` project containing:
- Configuration models (`AppConfig`, `SymbolConfig`, etc.)
- Status response models
- API request/response DTOs

### Priority 3: Windows Testing Checklist

- [ ] Build UWP app: `dotnet build src/MarketDataCollector.Uwp -c Release`
- [ ] Build core service: `dotnet publish src/MarketDataCollector -c Release -r win-x64`
- [ ] Run install.ps1 to verify prerequisites
- [ ] Test HTTP endpoints: `/status`, `/health`, `/metrics`
- [ ] Verify UWP ↔ Core communication
- [ ] Test configuration hot-reload
- [ ] Validate MassTransit InMemory mode
- [ ] Run unit tests: `dotnet test`

---

## Test Coverage Assessment

### Existing Tests (21 test files):

| Category | Coverage |
|----------|----------|
| Domain Models | ✅ TradeModelTests, BboQuotePayloadTests |
| Collectors | ✅ TradeDataCollectorTests, QuoteCollectorTests |
| Config | ✅ ConfigValidatorTests |
| Messaging | ✅ MassTransitPublisherTests, CompositePublisherTests |
| Resilience | ✅ WebSocketResiliencePolicyTests, ConnectionRetryTests |
| Storage | ✅ FilePermissionsServiceTests |
| Serialization | ✅ HighPerformanceJsonTests |

### Missing Test Coverage:

- UWP Services (StatusService, ConfigService)
- Provider implementations (AlpacaMarketDataClient, IBMarketDataClient)
- EventPipeline throughput tests
- End-to-end integration tests

---

## Known Technical Debt

1. **Decimal vs Double for Prices** - Financial prices use `double` which can cause precision issues

2. **Missing Circuit Breaker** - Polly is included but circuit breaker not fully implemented

3. **OpenTelemetry Not Instrumented** - Packages included but tracing not active

4. **No Schema Versioning** - JSONL files lack schema version metadata

5. **Dropped Event Recovery** - Events dropped due to backpressure are not persisted

---

## Conclusion

The Market Data Collector architecture is mature and well-designed. The codebase is ready for Windows desktop testing with the following conditions:

1. **Must Fix:** UWP API endpoint mismatch (Issue #1)
2. **Must Fix:** Status response model alignment (Issue #3)
3. **Recommended:** Create shared contracts library (Issue #2)

Once these issues are addressed, the system is ready for comprehensive Windows desktop testing.

---

*Report generated by Architecture Audit - Claude Code*
