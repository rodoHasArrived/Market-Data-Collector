# Provider Management & Data Quality Architecture

**Version:** 2.1 | **Last Updated:** 2026-02-10

This document describes the current provider-management architecture used by Market Data Collector. It focuses on provider discovery, capability modeling, runtime selection/failover, and data-quality operations for historical backfill.

See also:
- [ADR-001: Provider Abstraction](../adr/001-provider-abstraction.md)
- [ADR-005: Attribute-Based Discovery](../adr/005-attribute-based-discovery.md)

## Overview

The provider stack is built around a single metadata contract (`IProviderMetadata`) and a unified capability model (`ProviderCapabilities`) that covers:

- Streaming providers
- Historical/backfill providers
- Symbol-search providers

At runtime, provider registration and routing are handled by `ProviderRegistry`, while resilience and quality operations are handled by dedicated services (`CircuitBreaker`, `ConcurrentProviderExecutor`, `PriorityBackfillQueue`, `DataGapRepairService`, and `DataQualityMonitor`).

## Layered Architecture

```text
Application Layer
└── DataSourceManager / UI endpoints / jobs
    └── Provider Registry + Catalog
        ├── Attribute-based discovery ([DataProvider])
        ├── ProviderTemplateFactory (UI/ops metadata)
        └── Capability filtering and priority ordering
            └── Resilience Layer
                ├── CircuitBreaker registry
                ├── ConcurrentProviderExecutor
                └── RateLimiter
                    └── Concrete providers
                        ├── Streaming (Alpaca, Polygon, IB, StockSharp, ...)
                        ├── Backfill (Alpaca, Polygon, Yahoo, ...)
                        └── Symbol search (Alpaca, Finnhub, OpenFIGI, ...)
```

## Core Contracts

### 1) Unified metadata (`IProviderMetadata`)

All provider types expose a shared identity + capability surface:

- `ProviderId`, `ProviderDisplayName`, `ProviderDescription`
- `ProviderPriority` (lower value = preferred)
- `ProviderCapabilities` (single record shared by all provider categories)

The interface also supports UI-oriented defaults for:

- Credential requirements (`ProviderCredentialFields`)
- Notes/warnings (`ProviderNotes`, `ProviderWarnings`)
- Derived data-type labels (`SupportedDataTypes`)

### 2) Unified capability model (`ProviderCapabilities`)

Capabilities are represented as a strongly-typed record with boolean features and optional metadata (instead of the previous bit-flag enum style).

Key groups include:

- **Provider type:** `SupportsStreaming`, `SupportsBackfill`, `SupportsSymbolSearch`
- **Streaming:** real-time trades/quotes, market depth, depth limits
- **Backfill:** intraday support, adjusted prices, dividends/splits, historical trades/quotes/auctions
- **Search:** asset/exchange filtering and supported values
- **Operational metadata:** supported markets and rate-limit descriptors

Factory helpers are provided for common configurations (for example `Streaming(...)`, `BackfillBarsOnly`, `BackfillFullFeatured`, `SymbolSearch`, and `Hybrid(...)`).

## Discovery & Registration

### Attribute-based discovery

Providers are discovered from assemblies via `[DataProvider]` metadata and registered through DI.

```csharp
services.AddProviderRegistry(
    Assembly.GetExecutingAssembly(),
    typeof(AlpacaMarketDataClient).Assembly
);
```

### Registry responsibilities

`ProviderRegistry` is the central authority for:

- Provider registration and lookup by id
- Filtering by provider type and capability traits
- Priority-aware ordering
- Catalog generation via `ProviderTemplateFactory`

The catalog output is used by UI and operations surfaces to render provider capabilities and limits without provider-specific code paths.

## Runtime Execution & Resilience

### 1) Circuit breaker isolation

Each provider can be guarded by a dedicated `CircuitBreaker` instance:

- **Closed:** normal operation
- **Open:** fail fast while a provider is unhealthy
- **Half-open:** recovery probing before full re-enable

This avoids cascading failures and enables controlled recovery.

### 2) Concurrent execution

`ConcurrentProviderExecutor` runs provider operations in parallel with options such as:

- Maximum concurrency
- Per-provider timeout
- Continue-on-error vs. fail-fast semantics
- Strategy selection (`All`, `FirstSuccess`, `HighestPriority`, `Merge`, `BestQuality`)

### 3) Rate limiting

Provider-specific request pacing is handled with `RateLimiter` settings (window, max requests, minimum delay), typically derived from provider metadata/configuration.

## Historical Backfill, Gap Repair, and Quality

### 1) Priority queue

`PriorityBackfillQueue` supports:

- Priority-based ordering (Critical, High, Normal, Low, Deferred)
- Batch enqueue
- Optional dependency chains for orchestrated workflows
- Explicit completion and retry-friendly processing

### 2) Gap detection & repair

`DataGapRepairService` analyzes expected vs. stored data and can attempt automated repair using preferred providers and fallback behavior.

Gap classifications include missing, partial, holiday, and suspicious periods, enabling deterministic repair/reporting pipelines.

### 3) Quality monitoring

`DataQualityMonitor` computes weighted quality scores across dimensions such as:

- Completeness
- Accuracy
- Timeliness
- Consistency
- Validity

Scores and alerts can drive follow-up repair flows and operational dashboards.

## Configuration Guidance

Recommended configuration domains:

- Provider operation defaults (timeouts, concurrency)
- Circuit breaker thresholds and windows
- Queue/retry settings for backfill jobs
- Quality thresholds for alerting/auto-repair
- Per-provider rate limits

Keep provider credentials and provider-specific toggles in environment/config overrides; avoid hardcoding limits in provider implementations when they can be centrally configured.

## Best Practices

1. **Select by capability, then by priority and health**
   - Filter providers by required features.
   - Exclude providers with open circuits.
   - Prefer lower priority value when multiple providers qualify.

2. **Use strategy-based execution for fallback behavior**
   - `FirstSuccess` for latency-sensitive reads.
   - `All` or `Merge` for reconciliation/verification jobs.
   - `BestQuality` for quality-driven backfill paths.

3. **Close the loop on data quality**
   - Regularly score symbols/time ranges.
   - Trigger targeted gap repair on low scores.
   - Re-score after repair and emit metrics.

4. **Publish catalog metadata for UI/ops**
   - Use registry/catalog APIs so UI components consume normalized provider metadata rather than provider-specific DTOs.

## Migration Notes (v2.0 -> v2.1 docs refresh)

- Terminology has been aligned to the current `IProviderMetadata` + record-based `ProviderCapabilities` model.
- Discovery/registry sections now reflect catalog-first usage with `ProviderTemplateFactory`.
- Resilience and backfill quality sections were consolidated to describe current runtime flow.
