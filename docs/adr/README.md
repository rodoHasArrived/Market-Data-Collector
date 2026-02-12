# Architectural Decision Records (ADRs)

This directory contains Architectural Decision Records documenting significant technical decisions made in the Market Data Collector project.

## What is an ADR?

An ADR is a document that captures an important architectural decision along with its context and consequences. Each ADR includes links to the actual code implementations, making it easy to verify that code matches documented decisions.

## ADR Index

| ID | Title | Status | Implementation |
|----|-------|--------|----------------|
| [ADR-001](001-provider-abstraction.md) | Provider Abstraction Pattern | Accepted | `IMarketDataClient`, `IHistoricalDataProvider` |
| [ADR-002](002-tiered-storage-architecture.md) | Tiered Storage Architecture | Accepted | `Storage/` directory |
| [ADR-003](003-microservices-decomposition.md) | Microservices Decomposition | Rejected | Monolith preferred |
| [ADR-004](004-async-streaming-patterns.md) | Async Streaming Patterns | Accepted | `IAsyncEnumerable<T>` usage |
| [ADR-005](005-attribute-based-discovery.md) | Attribute-Based Provider Discovery | Accepted | `DataSourceAttribute` |
| [ADR-006](006-domain-events-polymorphic-payload.md) | Domain Events Polymorphic Payload Pattern | Accepted | `MarketEvent`, `IMarketEventPayload` |
| [ADR-007](007-write-ahead-log-durability.md) | Write-Ahead Log (WAL) + Event Pipeline Durability | Accepted | `WriteAheadLog`, `EventPipeline` |
| [ADR-008](008-multi-format-composite-storage.md) | Multi-Format Composite Storage Sink Pattern | Accepted | `CompositeSink`, `IStorageSink` |
| [ADR-009](009-fsharp-interop.md) | F# Type-Safe Domain with C# Interop Bridge | Accepted | `MarketDataCollector.FSharp`, `Interop.fs` |
| [ADR-010](010-httpclient-factory.md) | HttpClientFactory Lifecycle | Accepted | `HttpClientConfiguration` |
| [ADR-011](011-centralized-configuration-and-credentials.md) | Centralized Configuration & Credentials | Accepted | `IConfigurationProvider`, `ICredentialStore` |
| [ADR-012](012-monitoring-and-alerting-pipeline.md) | Unified Monitoring & Alerting Pipeline | Accepted | `IHealthCheckProvider`, `IAlertDispatcher` |
| [ADR-013](013-bounded-channel-policy.md) | Bounded Channel Pipeline Policy with Backpressure | Accepted | `EventPipelinePolicy` |
| [ADR-014](014-json-source-generators.md) | High-Performance JSON Serialization via Source Generators | Accepted | `MarketDataJsonContext`, `HighPerformanceJson` |

## ADR Lifecycle

1. **Proposed** - Under discussion
2. **Accepted** - Approved and implemented
3. **Deprecated** - No longer recommended
4. **Superseded** - Replaced by another ADR

## Creating a New ADR

Use the template at [_template.md](_template.md) to create new ADRs.

## Verification

ADRs include implementation links that are verified during the build process. Run:

```bash
make verify-adrs
```

This ensures documented decisions remain in sync with actual code.

## Recent Additions

The following ADRs were added in February 2026:

- **ADR-006**: Domain Events Polymorphic Payload Pattern - Documents the sealed record wrapper with static factory methods for type-safe event construction
- **ADR-007**: Write-Ahead Log (WAL) + Event Pipeline Durability - Crash-safe event persistence with automatic recovery
- **ADR-008**: Multi-Format Composite Storage Sink Pattern - Simultaneous JSONL + Parquet storage from single event stream
- **ADR-009**: F# Type-Safe Domain with C# Interop Bridge - F# discriminated unions for domain logic with C# consumption
- **ADR-013**: Bounded Channel Pipeline Policy with Backpressure - Consistent channel configuration with static presets
- **ADR-014**: High-Performance JSON Serialization via Source Generators - Source-generated serializers eliminate reflection overhead

---

*Last Updated: 2026-02-12*
