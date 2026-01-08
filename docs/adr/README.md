# Architectural Decision Records (ADRs)

This directory contains Architectural Decision Records documenting significant technical decisions made in the Market Data Collector project.

## What is an ADR?

An ADR is a document that captures an important architectural decision along with its context and consequences. Each ADR includes links to the actual code implementations, making it easy to verify that code matches documented decisions.

## ADR Index

| ID | Title | Status | Implementation |
|----|-------|--------|----------------|
| [ADR-001](001-provider-abstraction.md) | Provider Abstraction Pattern | Accepted | `IMarketDataClient`, `IHistoricalDataProvider` |
| [ADR-002](002-tiered-storage-architecture.md) | Tiered Storage Architecture | Accepted | `Storage/` directory |
| [ADR-003](003-microservices-decomposition.md) | Microservices Decomposition | Accepted | `src/Microservices/` |
| [ADR-004](004-async-streaming-patterns.md) | Async Streaming Patterns | Accepted | `IAsyncEnumerable<T>` usage |
| [ADR-005](005-attribute-based-discovery.md) | Attribute-Based Provider Discovery | Accepted | `DataSourceAttribute` |

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

---

*Last Updated: 2026-01-08*
