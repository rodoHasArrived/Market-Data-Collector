# Deterministic Canonicalization Across Providers

> **Status:** Proposal
> **Related ADRs:** ADR-001 (Provider Abstraction), ADR-006 (Domain Events Polymorphic Payload), ADR-009 (F# Type-Safe Domain)
> **Related:** [Data Uniformity Plan](../reference/data-uniformity.md), [Storage Design](storage-design.md)

## Problem Statement

Today, the `MarketEvent.Symbol` field stores whatever string the provider emitted. The `EventPipeline` passes events through to storage sinks without symbol resolution, condition-code mapping, or timestamp alignment. This means:

- The same instrument may appear as `"AAPL"` from Alpaca, `"AAPL"` from Polygon, but `"AAPL.US"` from StockSharp or `"AAPL.O"` from another feed. They are structurally different strings representing the same security.
- Trade condition codes are stored as raw `string[]?` (`TradeDto.Conditions`) with no normalization. Alpaca uses CTA plan codes (`"@"`, `"T"`), Polygon uses numeric codes (`"37"`, `"12"`), and IB uses free-text descriptions.
- `ExchangeTimestamp` is optional and rarely populated. Latency calculations via `EstimatedLatencyMs` are unreliable for cross-provider comparison.
- `Venue` is an optional freeform string that differs across providers for the same exchange.

The `CanonicalSymbolRegistry` and `SymbolRegistry.ProviderMappings` infrastructure exists but is **not consulted** at event publish time. The resolution happens in configuration/UI tooling, never in the ingestion path (`MarketEvent.cs:23-83` factory methods pass `symbol` through unchanged).

## Goal

Equivalent market events from different providers for the same instrument should produce **structurally comparable canonical records** without losing the raw provider payload for auditability.

## Design Direction

### What changes

1. **Inject a canonicalization step between provider adapters and `EventPipeline`** that resolves symbols, maps condition codes, and normalizes venue identifiers.
2. **Extend `MarketEvent`** with a `CanonicalSymbol` field and a `CanonicalizationVersion` field so consumers can distinguish raw vs. canonicalized events and pin to a specific transformation version.
3. **Introduce a deterministic condition-code mapping registry** keyed by `(provider, event_type)`.
4. **Standardize timestamp semantics** by making providers populate `ExchangeTimestamp` when the data is available, and tagging clock quality.

### What does not change

- The `EventPipeline` remains a passthrough bounded channel. Canonicalization happens **before** publish, not inside the consumer loop, to avoid adding latency to the high-throughput sink path.
- Raw provider payloads persist unchanged. Canonical fields are **additive** (new fields on the envelope), not mutations of existing fields.
- The WAL, storage sinks, and serialization pipeline are unaffected except for the new fields surfacing in JSON output.
- `SymbolNormalization.cs` continues to handle provider-specific format transforms (Tiingo dashes, Stooq lowercase, etc.). Canonicalization is a **higher-level identity resolution** that builds on normalization.

## Current State Assessment

### Infrastructure that exists and can be leveraged

| Component | Location | Readiness |
|-----------|----------|-----------|
| `CanonicalSymbolRegistry` | `Application/Services/CanonicalSymbolRegistry.cs` | Has multi-identifier resolution (ISIN, FIGI, aliases, provider mappings). Not wired into ingestion. |
| `SymbolRegistry.ProviderMappings` | `Contracts/Catalog/SymbolRegistry.cs` | Maps `provider -> providerSymbol -> canonical`. Populated by config tooling but never queried at event time. |
| `SymbolNormalization` | `Infrastructure/Utilities/SymbolNormalization.cs` | Per-provider format normalization (uppercase, Tiingo dashes, etc.). Working. |
| `MarketEventTier` enum | `Contracts/Domain/Enums/` | Already has `Raw`, `Enriched`, `Processed` values. `Enriched` is the natural tier for canonicalized events. |
| `IntegrityEvent` factories | `Contracts/Domain/Models/IntegrityEvent.cs` | Has `InvalidSymbol`, `SequenceGap`, etc. Can be extended for canonicalization failures. |
| `DataQualityMonitoringService` | `Application/Monitoring/DataQuality/` | Full quality pipeline with completeness scoring, gap analysis, cross-provider comparison. |
| `EventSchemaValidator` | `Application/Monitoring/EventSchemaValidator.cs` | Lightweight pre-persistence validation. |
| `MarketEvent.StampReceiveTime()` | `Domain/Events/MarketEvent.cs:89` | Demonstrates the `with` expression pattern for enriching immutable records. |
| F# `ValidationPipeline` | `FSharp/Validation/ValidationPipeline.fs` | Applicative validation with `Result<'T, ValidationError list>`. |
| `EventSchema` + `DataDictionary` | `Contracts/Schema/EventSchema.cs` | Schema definitions with `TradeConditions` and `QuoteConditions` dictionaries already in the model. |
| `CrossProviderComparisonService` | `Application/Monitoring/DataQuality/` | Tracks price/volume discrepancies across providers. |

### Gaps to fill

| Gap | Impact | Effort |
|-----|--------|--------|
| No symbol resolution at event publish time | Different files per provider for same instrument | Medium - wire `CanonicalSymbolRegistry.Resolve()` into provider adapters |
| Condition codes stored as raw `string[]?` | Cannot filter/compare conditions across providers | Medium - build mapping table, new canonical enum |
| `Venue` field is freeform | Same exchange appears as different strings | Low - venue normalization lookup table |
| `ExchangeTimestamp` rarely populated | Latency metrics unreliable cross-provider | Low-Medium - per-provider adapter changes |
| No `CanonicalizationVersion` field | Cannot pin backtests to a specific transform | Low - add field to `MarketEvent` record |
| No `CanonicalSymbol` field on envelope | Consumers must resolve symbols themselves | Low - add field to `MarketEvent` record |
| No dead-letter routing for unmapped events | Unmappable events silently persist with raw data | Medium - add quarantine sink option |

## Detailed Design

### A. Extended MarketEvent Envelope

Add three fields to the existing `MarketEvent` sealed record using the established `with` expression pattern:

```csharp
public sealed record MarketEvent(
    DateTimeOffset Timestamp,
    string Symbol,                        // Raw provider symbol (unchanged)
    MarketEventType Type,
    MarketEventPayload? Payload,
    long Sequence = 0,
    string Source = "IB",
    int SchemaVersion = 1,
    MarketEventTier Tier = MarketEventTier.Raw,
    DateTimeOffset? ExchangeTimestamp = null,
    DateTimeOffset ReceivedAtUtc = default,
    long ReceivedAtMonotonic = 0,
    // --- New canonicalization fields ---
    string? CanonicalSymbol = null,       // Resolved canonical identity (e.g., "AAPL")
    int CanonicalizationVersion = 0,      // 0 = not canonicalized, 1+ = version applied
    string? CanonicalVenue = null         // Normalized venue (e.g., "XNAS" ISO 10383 MIC)
);
```

**Rationale for additive fields vs. mutating `Symbol`:**
- Existing consumers and storage paths continue to work unchanged.
- Cross-provider reconciliation can group by `CanonicalSymbol` while preserving the raw `Symbol` for debugging.
- `CanonicalizationVersion = 0` marks events that haven't been through the pipeline (backward compatible with all existing data).

**Impact on serialization:** New fields must be added to `MarketDataJsonContext` source generator attributes. Since `JsonIgnoreCondition.WhenWritingNull` and default-value omission are already configured, the fields will be absent from JSON output when not set, preserving backward compatibility with existing JSONL files.

### B. Canonicalization Stage

A new `IEventCanonicalizer` that runs **before** `EventPipeline.PublishAsync()`, inside the provider adapter or as a wrapping publisher:

```csharp
public interface IEventCanonicalizer
{
    MarketEvent Canonicalize(MarketEvent raw, CancellationToken ct = default);
}
```

The implementation follows the same `with` expression pattern as `StampReceiveTime()`:

```csharp
public sealed class EventCanonicalizer : IEventCanonicalizer
{
    private readonly ICanonicalSymbolRegistry _symbols;
    private readonly ConditionCodeMapper _conditions;
    private readonly VenueMicMapper _venues;
    private readonly int _version;

    public MarketEvent Canonicalize(MarketEvent raw, CancellationToken ct = default)
    {
        var canonicalSymbol = _symbols.TryResolve(raw.Symbol, raw.Source);
        var canonicalVenue = _venues.TryMapVenue(raw.Payload?.Venue, raw.Source);

        // Enrich condition codes on payload if applicable
        var enrichedPayload = _conditions.TryEnrichPayload(raw.Payload, raw.Source);

        return raw with
        {
            CanonicalSymbol = canonicalSymbol ?? raw.Symbol,
            CanonicalVenue = canonicalVenue,
            CanonicalizationVersion = _version,
            Tier = MarketEventTier.Enriched,
            Payload = enrichedPayload ?? raw.Payload
        };
    }
}
```

**Placement in the pipeline:**

```
Provider WebSocket message
    |
    v
Provider Adapter (AlpacaMarketDataClient, PolygonMarketDataClient, etc.)
    |  Creates MarketEvent with raw Symbol, Source, optional ExchangeTimestamp
    |  Calls StampReceiveTime()
    v
EventCanonicalizer.Canonicalize()         <--- NEW STAGE
    |  Resolves CanonicalSymbol via CanonicalSymbolRegistry
    |  Maps condition codes via ConditionCodeMapper
    |  Normalizes Venue to ISO 10383 MIC
    |  Sets CanonicalizationVersion, Tier = Enriched
    v
EventPipeline.PublishAsync()              <--- Existing, unchanged
    |
    v
Storage Sinks (JSONL, Parquet)
```

**Why before the pipeline, not inside it:**
- The `EventPipeline` consumer loop (`ConsumeAsync` at `EventPipeline.cs:428`) is optimized for throughput with `AggressiveInlining` on publish and batched writes. Adding per-event lookups there would couple canonicalization latency to storage throughput.
- Canonicalization is synchronous (in-memory lookups). It does not need async I/O and fits naturally in the provider adapter's publish path.
- If canonicalization fails, the raw event can still enter the pipeline with `CanonicalizationVersion = 0` and a companion `IntegrityEvent`.

### C. Condition Code Mapping

**Current state:** `TradeDto.Conditions` is `string[]?`. `HistoricalTrade.Conditions` is `IReadOnlyList<string>?`. Both store raw provider values.

**Proposed model:**

```csharp
// Canonical condition codes (provider-agnostic)
public enum CanonicalTradeCondition
{
    Regular = 0,
    FormT_ExtendedHours = 1,
    OddLot = 2,
    AveragePrice = 3,
    Intermarket_Sweep = 4,
    OpeningPrint = 5,
    ClosingPrint = 6,
    DerivativelyPriced = 7,
    CrossTrade = 8,
    StockOption = 9,
    Halted = 10,
    CorrectedConsolidated = 11,
    // ... extend as needed
    Unknown = 255
}

// Mapping table loaded from config
public sealed class ConditionCodeMapper
{
    // Key: (provider, raw_code) -> CanonicalTradeCondition
    private readonly Dictionary<(string Provider, string RawCode), CanonicalTradeCondition> _map;

    public (CanonicalTradeCondition[] Canonical, string[] Raw) MapConditions(
        string provider, string[]? rawConditions);
}
```

**Mapping source data (examples):**

| Provider | Raw Code | Canonical |
|----------|----------|-----------|
| ALPACA | `"@"` | `Regular` |
| ALPACA | `"T"` | `FormT_ExtendedHours` |
| ALPACA | `"I"` | `Intermarket_Sweep` |
| POLYGON | `"0"` | `Regular` |
| POLYGON | `"12"` | `FormT_ExtendedHours` |
| POLYGON | `"37"` | `OddLot` |
| IB | `"RegularTrade"` | `Regular` |
| IB | `"OddLot"` | `OddLot` |

The mapping table will be stored as a JSON config file (`config/condition-codes.json`) and loaded at startup. The `DataDictionary.TradeConditions` field in `EventSchema.cs` already has a slot for this.

**Enriched payload contract:**

Condition codes are added to the payload alongside raw conditions, not replacing them. For `Trade`:

```csharp
// New fields on Trade or a wrapper
public string[]? RawConditions { get; }          // Original provider codes (preserved)
public CanonicalTradeCondition[]? Conditions { get; }  // Mapped canonical codes
```

### D. Venue Normalization

Normalize freeform venue strings to [ISO 10383 MIC codes](https://www.iso20022.org/market-identifier-codes):

| Provider | Raw Venue | Canonical MIC |
|----------|-----------|---------------|
| ALPACA | `"V"`, `"NASDAQ"` | `"XNAS"` |
| ALPACA | `"P"`, `"NYSE_ARCA"` | `"ARCX"` |
| POLYGON | `"4"` (exchange ID) | `"XNAS"` |
| IB | `"ISLAND"` | `"XNAS"` |
| IB | `"ARCA"` | `"ARCX"` |
| IB | `"NYSE"` | `"XNYS"` |

Stored in `config/venue-mapping.json`, loaded at startup. The `CanonicalVenue` field on `MarketEvent` carries the resolved MIC.

### E. Timestamp Semantics

Clarify the three timestamp fields and enforce population:

| Field | Semantics | Populated by | Required |
|-------|-----------|-------------|----------|
| `Timestamp` | When the event was created in the collector process | Factory methods (`MarketEvent.Trade()`, etc.) | Yes (always set) |
| `ExchangeTimestamp` | Exchange/venue timestamp from the provider feed | `StampReceiveTime(exchangeTs)` in provider adapter | Best-effort (depends on provider feed) |
| `ReceivedAtUtc` | Wall-clock time when event entered the collector | `StampReceiveTime()` | Yes (after stamping) |

**New field (future):**

| Field | Semantics | Purpose |
|-------|-----------|---------|
| `ClockQuality` | Enum: `ExchangeNtp`, `ProviderServer`, `CollectorLocal`, `Unknown` | Qualifies how trustworthy `ExchangeTimestamp` is for latency measurement |

Provider adapters should be updated to call `StampReceiveTime(exchangeTs)` with the exchange timestamp when the provider feed includes it (Alpaca and Polygon both provide it; IB provides it for most events).

### F. Symbol Identity Layer

The `CanonicalSymbolRegistry` already supports multi-identifier resolution:

```
CanonicalSymbolDefinition {
  Canonical: "AAPL"
  Aliases: ["AAPL.US", "AAPL.O", "US0378331005"]
  AssetClass: "equity"
  Exchange: "NASDAQ"
  ISIN, FIGI, CompositeFIGI, SEDOL, CUSIP
  ProviderSymbols: { "ALPACA": "AAPL", "POLYGON": "AAPL", "IB": "AAPL" }
}
```

The canonicalization engine calls `_symbols.TryResolve(rawSymbol, provider)` which:
1. Checks `ProviderMappings[provider][rawSymbol]` for an exact match.
2. Falls back to `AliasIndex[rawSymbol]`.
3. Falls back to `SymbolNormalization.Normalize(rawSymbol)` and retries.
4. Returns `null` if no match (unresolved).

**Unresolved symbols:**
- Event persists with `CanonicalSymbol = null`, `CanonicalizationVersion = N`.
- A companion `IntegrityEvent` with code `1005` (new: `UnresolvedSymbol`) is emitted.
- Metric `canonicalization_unresolved_total{provider,symbol}` is incremented.
- Alert threshold: > 0.1% unresolved rate for a provider triggers a warning.

### G. Failure Handling

| Severity | Condition | Action |
|----------|-----------|--------|
| **Hard-fail** | Missing required identity fields (`Symbol` empty or null) | Drop event, emit `IntegrityEvent` with `Severity.Error`, increment `canonicalization_hard_fail_total` |
| **Soft-fail** | Unknown condition code, unmapped venue, unresolved symbol | Persist with `CanonicalizationVersion = N` but `CanonicalSymbol = null` or partial mapping. Emit `IntegrityEvent` with `Severity.Warning` |
| **Degraded mode** | Unresolved mapping rate > 1% for 5+ minutes | Log alert, metric spike triggers PagerDuty/webhook if configured. No automatic fallback -- events continue persisting with raw values |

Hard-fail events are routed to the existing `DroppedEventAuditTrail` (already wired into `EventPipeline`).

### H. Versioning and Schema Evolution

- `CanonicalizationVersion` starts at `1` for the initial mapping tables.
- Any change to mapping tables (new condition codes, venue renames, symbol alias updates) bumps the version.
- Mapping table files are versioned in git alongside the source code.
- Backtests can pin to `CanonicalizationVersion = N` by replaying raw events through the canonicalizer at that version.
- The `EventSchema.Version` field and `DataDictionary` already support this pattern.

**Backward compatibility:**
- All existing JSONL files have `CanonicalizationVersion = 0` (implicit, field absent due to `WhenWritingNull`/default omission).
- Consumers that don't read `CanonicalSymbol` continue using `Symbol` unchanged.
- No migration of existing files is required. Re-canonicalization can be done offline by replaying through `JsonlReplayer` + `EventCanonicalizer`.

## Test Strategy

### Golden fixtures
- Curate raw JSON payloads from each provider for each event type (trade, quote, L2 update).
- Include edge cases: trading halts, crossed markets, odd lots, corporate action renames, pre/post-market trades.
- Store fixtures in `tests/MarketDataCollector.Tests/Fixtures/Canonicalization/`.
- Each fixture has a `.raw.json` input and `.expected.json` canonical output.

### Property tests
- **Idempotency:** `Canonicalize(Canonicalize(evt)) == Canonicalize(evt)` -- applying canonicalization twice produces the same result.
- **Determinism:** Same raw input always produces same canonical output (no time-dependent behavior).
- **Preservation:** `canonicalized.Symbol == raw.Symbol` (raw symbol is never overwritten).
- **Tier progression:** `canonicalized.Tier >= raw.Tier` (tier only increases or stays the same).

### Integration with existing test infrastructure
- Extend `CrossProviderComparisonService` tests to verify that events from different providers for the same symbol produce matching `CanonicalSymbol` values.
- Add tests in `tests/MarketDataCollector.Tests/Storage/CanonicalSymbolRegistryTests.cs` (already exists) to cover the new `TryResolve(symbol, provider)` path.
- Leverage the F# `ValidationPipeline` for condition-code mapping validation using applicative `Result<'T, ValidationError list>`.

### Drift canaries (CI)
- Nightly job fetches sample data from staging providers and runs canonicalization.
- Compares output against baseline snapshots.
- Alerts when a new unmapped condition code or venue appears.
- Integrates with existing `test-matrix.yml` workflow.

### Backward compatibility tests
- Replay archived JSONL files through the current canonicalizer.
- Verify no field is lost, no existing field value is mutated.
- Verify `CanonicalizationVersion = 0` files deserialize correctly with new schema.

## Operational Metrics

Expose via existing `PrometheusMetrics` infrastructure:

| Metric | Labels | Type |
|--------|--------|------|
| `canonicalization_events_total` | `provider`, `event_type`, `status` (success/soft_fail/hard_fail) | Counter |
| `canonicalization_duration_seconds` | `provider`, `event_type` | Histogram |
| `canonicalization_unresolved_total` | `provider`, `field` (symbol/venue/condition) | Counter |
| `canonicalization_version_active` | `service` | Gauge |
| `provider_parity_mismatch_total` | `symbol`, `mismatch_class` | Counter |

These integrate with the existing monitoring dashboard and `CrossProviderComparisonService`.

## Acceptance Criteria

| Criterion | Target | How to measure |
|-----------|--------|----------------|
| Cross-provider canonical identity match | >= 99.5% of equivalent events map to the same `CanonicalSymbol` | `CrossProviderComparisonService` with canonical grouping |
| Unresolved mapping rate (liquid US equities) | < 0.1% | `canonicalization_unresolved_total / canonicalization_events_total` per provider |
| Ingest latency overhead | < 5% median increase | `canonicalization_duration_seconds` p50 vs. baseline |
| Condition code coverage (CTA plan) | >= 95% of observed codes mapped | `canonicalization_unresolved_total{field="condition"}` |
| Backward compatibility | Zero breaking changes to existing consumers | Backward compat test suite passes |
| Schema versioning | Every mapping change has version bump + changelog entry | CI check on `config/condition-codes.json` and `config/venue-mapping.json` |

## Rollout Plan

### Phase 1: Contract + Mapping Inventory

- Add `CanonicalSymbol`, `CanonicalizationVersion`, `CanonicalVenue` fields to `MarketEvent`.
- Update `MarketDataJsonContext` source generator attributes.
- Build `ConditionCodeMapper` with initial mapping tables for Alpaca, Polygon, and IB.
- Build `VenueMicMapper` with ISO 10383 MIC lookup.
- Add `IEventCanonicalizer` interface and `EventCanonicalizer` implementation.
- Wire `CanonicalSymbolRegistry.TryResolve()` to accept a provider hint parameter.
- Golden fixture test suite for `trade` and `quote` event types, 3 providers.
- **Gate:** All existing tests pass. New fields are absent from serialized output when not set.

### Phase 2: Dual-Write Validation

- Enable canonicalization in provider adapters for a subset of pilot symbols (configurable via `appsettings.json`).
- Persist both raw (`Tier = Raw`) and canonicalized (`Tier = Enriched`) events via `CompositeSink`.
- Stand up parity dashboard view in the web UI showing match rates per symbol/provider.
- Run drift canaries in nightly CI.
- **Gate:** >= 99% canonical identity match rate for pilot symbols. < 0.5% unresolved mapping rate.

### Phase 3: Default Canonical Read Path

- Enable canonicalization for all symbols by default.
- Downstream consumers (UI, export, quality monitoring) read `CanonicalSymbol` when present, fall back to `Symbol`.
- Stop dual-writing raw events once parity is confirmed (configurable cutover flag).
- Add `book_update` / `L2Snapshot` event type canonicalization.
- Finalize schema evolution SOP document.
- **Gate:** All acceptance criteria met. Rollback automation tested.

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Mapping table incomplete for new provider | Medium | Soft-fail events with raw values | Drift canary CI alerts on unmapped codes; auto-create GitHub issue |
| Canonicalization adds measurable latency | Low | Pipeline throughput reduction | In-memory hash lookups only; benchmarked with BenchmarkDotNet |
| Source generator doesn't pick up new fields | Low | Serialization breaks | CI build verifies `MarketDataJsonContext` compiles cleanly |
| Corporate action renames break symbol mapping | Medium | Temporary unresolved symbols | `CanonicalSymbolRegistry` supports alias updates; registry hot-reload via `ConfigWatcher` |
| Backward incompatibility with existing JSONL | Low | Downstream consumers break | New fields use `WhenWritingNull`/default omission; absent = `CanonicalizationVersion = 0` |

## Appendix: Files to Modify

| File | Change |
|------|--------|
| `src/MarketDataCollector.Domain/Events/MarketEvent.cs` | Add `CanonicalSymbol`, `CanonicalizationVersion`, `CanonicalVenue` parameters |
| `src/MarketDataCollector.Contracts/Domain/Events/MarketEvent.cs` | Mirror new fields in contract |
| `src/MarketDataCollector.Core/Serialization/MarketDataJsonContext.cs` | Register new types for source generation |
| `src/MarketDataCollector.Application/Services/CanonicalSymbolRegistry.cs` | Add `TryResolve(symbol, provider)` overload |
| `src/MarketDataCollector.Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` | Wire canonicalization before publish |
| `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` | Wire canonicalization before publish |
| `src/MarketDataCollector.Infrastructure/Providers/Streaming/IB/IBMarketDataClient.cs` | Wire canonicalization before publish |
| `src/MarketDataCollector.Application/Monitoring/PrometheusMetrics.cs` | Add canonicalization counters |
| `config/condition-codes.json` | New file: provider condition code mapping table |
| `config/venue-mapping.json` | New file: raw venue to ISO 10383 MIC mapping |
| `tests/MarketDataCollector.Tests/Application/Services/EventCanonicalizerTests.cs` | New test class |
