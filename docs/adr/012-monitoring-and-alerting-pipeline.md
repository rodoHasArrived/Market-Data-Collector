# ADR-012: Unified Monitoring and Alerting Pipeline

**Status:** Accepted
**Date:** 2026-02-02
**Deciders:** Core Team

## Context

Market data ingestion relies on continuous health checks, data quality validation, and operational alerts. Components such as providers, storage, and background services need a consistent way to:

- Report health status with severity and diagnostics
- Aggregate system-wide health in a single snapshot
- Publish alerts that can be filtered and subscribed to by operators or UI modules

Previously, health reporting and alerting logic was scattered across services, leading to inconsistent severity classification and limited visibility into system state.

## Decision

Adopt a unified monitoring pipeline with two core abstractions:

1. **Health checks** via `IHealthCheckProvider` implementations aggregated by `IHealthCheckAggregator`.
2. **Alert publishing** via `IAlertDispatcher`, providing centralized alert history and subscription-based routing.

These abstractions standardize severity levels, encourage time-bounded checks, and ensure alerts are visible to any host or UI.

## Implementation Links

<!-- These links are verified by the build process -->

| Component | Location | Purpose |
|-----------|----------|---------|
| Health contract | `src/MarketDataCollector/Application/Monitoring/Core/IHealthCheckProvider.cs:8` | Shared health severity and provider API |
| Health aggregation | `src/MarketDataCollector/Application/Monitoring/Core/HealthCheckAggregator.cs:10` | Parallel health evaluation with timeouts |
| Alert contract | `src/MarketDataCollector/Application/Monitoring/Core/IAlertDispatcher.cs:6` | Alert modeling and dispatch API |
| Alert dispatcher | `src/MarketDataCollector/Application/Monitoring/Core/AlertDispatcher.cs:10` | Centralized alert publishing |

## Rationale

- **Consistency**: Standard severity levels across all components.
- **Operational clarity**: Aggregated health reporting prevents blind spots.
- **Extensibility**: New providers register health checks without changing the core.
- **Observability**: Alerts are centrally logged and can be subscribed to by UI or automation.

## Alternatives Considered

### Alternative 1: Ad-hoc logging only

Rely solely on log statements for health and alerting.

**Pros:**
- Minimal implementation effort

**Cons:**
- No structured severity or aggregation
- Harder to build dashboards or automated response

**Why rejected:** Lacks actionable signal for operations.

### Alternative 2: Direct metric polling per host

Each host polls components and computes its own health status.

**Pros:**
- Host-specific flexibility

**Cons:**
- Duplicated logic, inconsistent thresholds
- Difficult to share alert history across interfaces

**Why rejected:** Centralized pipeline is more reliable and consistent.

## Consequences

### Positive

- Unified health view across all components.
- Alert routing is centralized and predictable.
- Easier operator tooling and UI integration.

### Negative

- Requires providers to implement health checks explicitly.
- Alert volume must be managed to avoid noise.

### Neutral

- Legacy checks can coexist during migration.

## Compliance

### Code Contracts

```csharp
public interface IHealthCheckProvider
{
    string ComponentName { get; }
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default);
}

public interface IAlertDispatcher
{
    void Publish(MonitoringAlert alert);
    IDisposable Subscribe(Action<MonitoringAlert> handler, AlertFilter? filter = null);
}
```

### Runtime Verification

- Build-time verification via `make verify-adrs`

## References

- [Project Context](../generated/project-context.md)
- [Operator Runbook](../operations/operator-runbook.md)

---

*Last Updated: 2026-02-02*
