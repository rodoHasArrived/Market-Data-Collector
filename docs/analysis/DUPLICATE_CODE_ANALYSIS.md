# Duplicate Code Analysis Report

**Date:** 2026-01-26
**Branch:** `claude/identify-duplicate-code-3kPMs`
**Scope:** Full codebase analysis for duplicate functionality and integration issues

---

## Executive Summary

This analysis identified **significant duplicate code patterns** across the Market Data Collector codebase, primarily in:
1. **Domain Models** - 12+ duplicate models between `Domain/` and `Contracts/`
2. **HTTP Client Configuration** - Nearly identical implementations in main project and UWP
3. **Provider Implementations** - Repeated patterns across 8+ backfill and 5+ streaming providers
4. **Storage Services** - Duplicate buffer, retention, and checksum logic
5. **UWP Service Wrappers** - 59 services with significant overlap with core functionality

**Estimated duplicate code:** 2,500-3,500 lines that could be consolidated.

---

## 1. Domain Model Duplication (Critical)

### Location
- `src/MarketDataCollector/Domain/Models/` (12 files)
- `src/MarketDataCollector.Contracts/Domain/Models/` (12 files)

### Affected Models
| Model | Domain Version | Contracts Version | Differences |
|-------|----------------|-------------------|-------------|
| `Trade.cs` | 50 lines | 82 lines | Contracts has XML docs |
| `HistoricalBar.cs` | ~60 lines | ~112 lines | Contracts has XML docs |
| `HistoricalQuote.cs` | Present | Present | Duplicate |
| `HistoricalTrade.cs` | Present | Present | Duplicate |
| `HistoricalAuction.cs` | Present | Present | Duplicate |
| `BboQuotePayload.cs` | Present | Present | Duplicate |
| `L2SnapshotPayload.cs` | Present | Present | Duplicate |
| `LOBSnapshot.cs` | Present | Present | Duplicate |
| `OrderBookLevel.cs` | Present | Present | Duplicate |
| `OrderFlowStatistics.cs` | Present | Present | Duplicate |
| `IntegrityEvent.cs` | Present | Present | Duplicate |
| `DepthIntegrityEvent.cs` | Present | Present | Duplicate |

### Evidence
**Domain/Models/Trade.cs (lines 1-50):**
```csharp
public sealed record Trade : MarketEventPayload
{
    public DateTimeOffset Timestamp { get; }
    public string Symbol { get; }
    public decimal Price { get; }
    // ... identical validation logic
}
```

**Contracts/Domain/Models/Trade.cs (lines 1-82):**
```csharp
public sealed record Trade : MarketEventPayload
{
    /// <summary>Gets the timestamp...</summary>
    public DateTimeOffset Timestamp { get; }
    // ... same implementation with more XML docs
}
```

### Recommendation
**Consolidate to `MarketDataCollector.Contracts`** as the single source of truth:
1. Delete `Domain/Models/*.cs` duplicates
2. Update imports throughout `MarketDataCollector` to use `Contracts.Domain.Models`
3. Keep `Domain/` folder for domain-specific logic not shared externally

---

## 2. HTTP Client Configuration Duplication (High)

### Location
- `src/MarketDataCollector/Infrastructure/Http/HttpClientConfiguration.cs` (403 lines)
- `src/MarketDataCollector.Uwp/Services/HttpClientConfiguration.cs` (290 lines)

### Duplicate Components

| Component | Main Project | UWP Project | Overlap |
|-----------|--------------|-------------|---------|
| `HttpClientNames` class | 31 constants | 16 constants | 8 identical |
| `AddStandardResiliencePolicy()` | lines 311-316 | lines 181-186 | 100% identical |
| `GetRetryPolicy()` | lines 321-335 | lines 191-204 | 95% identical |
| `GetCircuitBreakerPolicy()` | lines 340-347 | lines 209-216 | 100% identical |
| `HttpClientFactoryProvider` | lines 357-402 | lines 226-289 | 80% identical |

### Recommendation
Extract shared HTTP utilities to `MarketDataCollector.Contracts`:
```
MarketDataCollector.Contracts/
├── Http/
│   ├── SharedHttpClientNames.cs    # Common constants
│   ├── ResiliencePolicies.cs       # Retry, circuit breaker policies
│   └── HttpClientFactoryBase.cs    # Base factory implementation
```

---

## 3. Provider Implementation Duplication (High)

### 3.1 Symbol Normalization (6+ implementations)

| File | Line | Implementation |
|------|------|----------------|
| `AlpacaHistoricalDataProvider.cs` | 748-751 | `symbol.ToUpperInvariant().Trim()` |
| `PolygonHistoricalDataProvider.cs` | 370-373 | `symbol.ToUpperInvariant()` |
| `FinnhubHistoricalDataProvider.cs` | 365-368 | `symbol.ToUpperInvariant()` |
| `TiingoHistoricalDataProvider.cs` | 204-207 | `symbol.ToUpperInvariant().Replace(".", "-")` |
| `YahooFinanceHistoricalDataProvider.cs` | 203-206 | `symbol.ToUpperInvariant()` |
| `AlphaVantageHistoricalDataProvider.cs` | 349-352 | `symbol.ToUpperInvariant()` |

**Recommendation:** Create `Infrastructure/Utilities/SymbolNormalization.cs`:
```csharp
public static class SymbolNormalization
{
    public static string Normalize(string symbol) => symbol.ToUpperInvariant().Trim();
    public static string NormalizeForTiingo(string symbol) => Normalize(symbol).Replace(".", "-");
}
```

### 3.2 Rate Limiter Initialization (8+ providers)

All backfill providers duplicate this pattern:
```csharp
_rateLimiter = new RateLimiter(
    maxRequestsPerWindow: {provider-specific},
    window: TimeSpan.FromMinutes(1),
    minDelayBetweenRequests: TimeSpan.FromMilliseconds(...),
    log: _log
);
```

**Recommendation:** Create provider-specific presets in `RateLimiterPresets.cs`.

### 3.3 HTTP Error Handling (5+ providers)

Repeated pattern for 429/403/404 status codes:
```csharp
if (statusCode == 429)
{
    _isRateLimited = true;
    _rateLimitResetsAt = DateTimeOffset.UtcNow + RateLimitWindow;
    throw new InvalidOperationException(...);
}
```

**Recommendation:** Create `HttpResponseHandler` utility class.

### 3.4 Subscription Management (5 streaming providers)

Identical lock-based subscription tracking:
```csharp
var id = Interlocked.Increment(ref _nextSubId);
lock (_gate)
{
    _tradeSymbols.Add(symbol);
    _subs[id] = (symbol, "trades");
}
```

**Files:** AlpacaMarketDataClient, PolygonMarketDataClient, NYSEDataSource, StockSharpMarketDataClient, IBMarketDataClient

**Recommendation:** Create `SubscriptionManager<T>` base class.

### 3.5 WebSocket Cleanup (3 providers)

Duplicate try-dispose patterns for WebSocket and CancellationTokenSource.

**Recommendation:** Create `WebSocketCleanup` utility class.

---

## 4. Storage Service Duplication (Medium)

### 4.1 Checksum Computation (3-4 implementations)

| File | Method | Algorithm |
|------|--------|-----------|
| `PortableDataPackager.cs:1062-1067` | `ComputeFileChecksumAsync()` | SHA256 |
| `AnalysisExportService.cs:1298-1303` | `ComputeChecksumAsync()` | SHA256 |
| `ManifestService.cs` (UWP) | Checksum logic | SHA256 |

**Recommendation:** Create `StorageChecksumService` in `Storage/Services/`.

### 4.2 Buffer/Batching Logic (2 implementations)

- `JsonlStorageSink.cs:277-405` - `BatchBuffer` and `WriterState` inner classes
- `ParquetStorageSink.cs:348-385` - `ParquetBufferState` inner class

Both implement identical lock-based buffer patterns.

**Recommendation:** Extract to generic `EventBuffer<T>` class.

### 4.3 Retention Policy (3 implementations)

- `JsonlStorageSink.cs:407-532` - Embedded `RetentionManager`
- `JsonlStoragePolicy.cs` - Separate implementation
- `ScheduledArchiveMaintenanceService.cs:661-720` - Inline retention logic

**Recommendation:** Consolidate to single `RetentionPolicyManager`.

---

## 5. UWP Service Wrapper Duplication (Medium)

### Scope
59 service files in `src/MarketDataCollector.Uwp/Services/`

### Notable Duplications

| Core Service | UWP Service | Issue |
|--------------|-------------|-------|
| `AnalysisExportService` | `AnalysisExportService` | **Same class name** in different namespaces |
| `PortableDataPackager` | `PortablePackagerService` | 1:1 wrapper |
| `DataQualityService` | `DataQualityService` | Different implementations |
| Archive health logic | `ArchiveHealthService` | Re-implements calculations |
| Storage analytics | `StorageAnalyticsService` | Duplicate calculations |

### Recommendation
1. Rename UWP services with `Uwp` prefix to avoid confusion
2. Create thin adapter layer instead of full re-implementations
3. Reference core services where possible via API clients

---

## 6. Summary Statistics

| Category | Duplicate Instances | Files Affected | Est. LOC Savings |
|----------|---------------------|----------------|------------------|
| Domain Models | 12 models | 24 files | 600-800 lines |
| HTTP Configuration | 3 classes | 2 files | 150-200 lines |
| Symbol Normalization | 6 methods | 6 files | 30-50 lines |
| Rate Limiter Init | 8 instances | 8 files | 50-80 lines |
| HTTP Error Handling | 5 implementations | 5 files | 200-300 lines |
| Subscription Mgmt | 5 implementations | 5 files | 300-400 lines |
| Storage Checksum | 3 implementations | 3 files | 50-80 lines |
| Buffer/Batching | 2 implementations | 2 files | 150-200 lines |
| Retention Policy | 3 implementations | 3 files | 200-300 lines |
| UWP Services | Various | 59 files | 800-1000 lines |
| **Total** | **~50+ patterns** | **~100 files** | **2,500-3,500 lines** |

---

## 7. Recommended Refactoring Plan

### Phase 1: Quick Wins (Low Risk)
1. **Create utility classes:**
   - `Infrastructure/Utilities/SymbolNormalization.cs`
   - `Storage/Services/StorageChecksumService.cs`
   - `Infrastructure/Http/ResiliencePolicies.cs`

2. **Extract rate limiter presets** to centralized configuration

### Phase 2: Model Consolidation (Medium Risk)
1. **Delete duplicate Domain models**
2. **Update all imports** to use `Contracts.Domain.Models`
3. **Add project reference** from main to Contracts if not present

### Phase 3: Provider Base Classes (Medium Risk)
1. **Create `BaseHistoricalDataProvider`** with common functionality
2. **Create `BaseStreamingDataClient`** for WebSocket providers
3. **Migrate providers** to use base classes

### Phase 4: UWP Cleanup (Higher Risk)
1. **Rename conflicting services** (e.g., `UwpAnalysisExportService`)
2. **Create thin adapters** instead of full implementations
3. **Reference shared HTTP configuration**

---

## 8. Files Requiring Immediate Review

**Critical Priority:**
- [ ] `src/MarketDataCollector/Domain/Models/*.cs` (12 files to delete)
- [ ] `src/MarketDataCollector.Uwp/Services/HttpClientConfiguration.cs` (consolidate)

**High Priority:**
- [ ] All backfill providers in `Infrastructure/Providers/Backfill/` (8 files)
- [ ] All streaming providers (5 files)
- [ ] `Storage/Sinks/JsonlStorageSink.cs` and `ParquetStorageSink.cs`

**Medium Priority:**
- [ ] Symbol search providers (4 files)
- [ ] UWP services with naming conflicts

---

## 9. Integration Verification

### Cross-Project Dependencies
```
MarketDataCollector.Contracts  <- Primary domain models (should be single source)
    ↑
MarketDataCollector           <- Main application
    ↑
MarketDataCollector.Ui        <- Web dashboard

MarketDataCollector.Uwp       <- Desktop app (separate dependency graph)
MarketDataCollector.FSharp    <- F# calculations
```

### Issues Found
1. **Domain models exist in two places** - violates DRY principle
2. **HTTP client configuration duplicated** - maintenance burden
3. **No shared base classes for providers** - repeated boilerplate

### Recommendation
Establish `MarketDataCollector.Contracts` as the canonical source for:
- All domain models
- Shared HTTP utilities
- Common interfaces

---

*Report generated by code analysis on 2026-01-26*
