# Idea Dimensions — MDC Brainstorm Reference

Seeded concept bank organized by category. Use these as inspiration prompts, not final ideas — the goal is to develop them into full Idea Cards grounded in the MDC codebase.

> **See also:** [`../../_shared/project-context.md`](../../_shared/project-context.md) for authoritative project stats, ADR table, and key abstraction file paths.

---

## 🗺️ Codebase Anchor Table

Use these when referencing specific abstractions in ideas. File paths are relative to the repository root.

| Concept | Interface / Class | File Path |
|---------|-------------------|-----------|
| Streaming provider contract | `IMarketDataClient` | `src/Meridian.ProviderSdk/IMarketDataClient.cs` |
| Historical provider contract | `IHistoricalDataProvider` | `src/Meridian.Infrastructure/Adapters/Core/IHistoricalDataProvider.cs` |
| Storage sink contract | `IStorageSink` | `src/Meridian.Storage/Interfaces/IStorageSink.cs` |
| Event pipeline coordinator | `EventPipeline` | `src/Meridian.Application/Pipeline/EventPipeline.cs` |
| Write-ahead log | `WriteAheadLog` | `src/Meridian.Storage/Archival/WriteAheadLog.cs` |
| Crash-safe file writes | `AtomicFileWriter` | `src/Meridian.Storage/Archival/AtomicFileWriter.cs` |
| JSONL sink | `JsonlStorageSink` | `src/Meridian.Storage/Sinks/JsonlStorageSink.cs` |
| Parquet sink | `ParquetStorageSink` | `src/Meridian.Storage/Sinks/ParquetStorageSink.cs` |
| MVVM base class | `BindableBase` | `src/Meridian.Wpf/ViewModels/BindableBase.cs` |
| ICommand implementation | `RelayCommand` | `src/Meridian.Wpf/ViewModels/` |
| JSON source-gen context | `MarketDataJsonContext` | `src/Meridian.Core/Serialization/MarketDataJsonContext.cs` |
| Multi-provider historical routing | `CompositeHistoricalDataProvider` | `src/Meridian.Infrastructure/Adapters/Core/` |
| Backfill orchestration | `HistoricalBackfillService` | `src/Meridian.Application/Backfill/HistoricalBackfillService.cs` |
| Gap detection | `GapBackfillService` | `src/Meridian.Application/Backfill/GapBackfillService.cs` |
| Data quality orchestrator | `DataQualityMonitoringService` | `src/Meridian.Application/Monitoring/DataQuality/` |
| Data completeness scoring | `CompletenessScoreCalculator` | `src/Meridian.Application/Monitoring/DataQuality/` |
| Prometheus metrics | `PrometheusMetrics` | `src/Meridian.Application/Monitoring/PrometheusMetrics.cs` |
| Storage catalog | `StorageCatalogService` | `src/Meridian.Storage/Services/StorageCatalogService.cs` |
| Tiered storage migration | `TierMigrationService` | `src/Meridian.Storage/Services/TierMigrationService.cs` |
| Parquet conversion | `ParquetConversionService` | `src/Meridian.Storage/Services/ParquetConversionService.cs` |
| F# validation pipeline | `ValidationPipeline` | `src/Meridian.FSharp/Validation/ValidationPipeline.fs` |
| F# quote validator | `QuoteValidator` | `src/Meridian.FSharp/Validation/QuoteValidator.fs` |
| F# trade validator | `TradeValidator` | `src/Meridian.FSharp/Validation/TradeValidator.fs` |
| Provider SDK attribute | `DataSourceAttribute` | `src/Meridian.ProviderSdk/DataSourceAttribute.cs` |
| Provider SDK discovery | `DataSourceRegistry` | `src/Meridian.ProviderSdk/DataSourceRegistry.cs` |
| Graceful shutdown | `GracefulShutdownService` | `src/Meridian.Application/Services/GracefulShutdownService.cs` |
| Storage sink discovery | `StorageSinkRegistry` | `src/Meridian.Storage/StorageSinkRegistry.cs` |
| Subscription orchestration | `SubscriptionOrchestrator` | `src/Meridian.Application/Subscriptions/SubscriptionOrchestrator.cs` |
| Failover provider | `FailoverAwareMarketDataClient` | `src/Meridian.Infrastructure/Adapters/Failover/` |
| Alpaca streaming | `AlpacaMarketDataClient` | `src/Meridian.Infrastructure/Adapters/Alpaca/` |
| IB streaming | `IBMarketDataClient` | `src/Meridian.Infrastructure/Adapters/InteractiveBrokers/` |
| WPF Dashboard page | `DashboardPage` | `src/Meridian.Wpf/Views/DashboardPage.xaml(.cs)` |
| WPF Dashboard ViewModel | `DashboardViewModel` | `src/Meridian.Wpf/ViewModels/DashboardViewModel.cs` |
| Configuration pipeline | `ConfigurationPipeline` | `src/Meridian.Application/Config/ConfigurationPipeline.cs` |
| Hot-path batch serializer | `HotPathBatchSerializer` | `src/Meridian.Application/Pipeline/HotPathBatchSerializer.cs` |
| Portable data packaging | `PortableDataPackager` | `src/Meridian.Storage/Packaging/PortableDataPackager.cs` |
| Order gateway (broker-agnostic) | `IOrderGateway` | `src/Meridian.Execution/Interfaces/IOrderGateway.cs` |
| Broker adapter SDK contract | `IExecutionGateway` | `src/Meridian.Execution.Sdk/IExecutionGateway.cs` |
| Live strategy context | `IExecutionContext` | `src/Meridian.Execution/Interfaces/IExecutionContext.cs` |
| Paper trading gateway | `PaperTradingGateway` | `src/Meridian.Execution/Adapters/PaperTradingGateway.cs` |
| Order lifecycle tracking | `OrderManagementSystem` | `src/Meridian.Execution/OrderManagementSystem.cs` |
| Pre-trade risk validation | `CompositeRiskValidator` | `src/Meridian.Risk/CompositeRiskValidator.cs` |
| Risk rule contract | `IRiskRule` | `src/Meridian.Risk/IRiskRule.cs` |
| Strategy lifecycle contract | `IStrategyLifecycle` | `src/Meridian.Strategies/Interfaces/IStrategyLifecycle.cs` |
| Strategy run archive | `StrategyRunStore` | `src/Meridian.Strategies/Storage/StrategyRunStore.cs` |
| Live strategy contract | `ILiveStrategy` | `src/Meridian.Strategies/Interfaces/ILiveStrategy.cs` |
| P&L ledger (double-entry) | `Ledger` | `src/Meridian.Ledger/Ledger.cs` |
| Ledger read-only view | `IReadOnlyLedger` | `src/Meridian.Ledger/IReadOnlyLedger.cs` |
| Backtest strategy contract | `IBacktestStrategy` | `src/Meridian.Backtesting.Sdk/IBacktestStrategy.cs` |
| Backtest context | `IBacktestContext` | `src/Meridian.Backtesting.Sdk/IBacktestContext.cs` |
| Backtest engine | `BacktestEngine` | `src/Meridian.Backtesting/Engine/` |

---

## 🔌 Data Access & APIs

- **Python SDK** — async iterator over live feed, pandas DataFrame output, `snap()` for last N ticks, `history()` for bulk pull. Wraps the WebSocket/REST HTTP layer already exposed by MDC.
- **gRPC streaming endpoint** — low-latency alternative to WebSocket for intra-datacenter consumers (e.g., C++ strategy engine consuming from MDC)
- **Arrow Flight endpoint** — columnar bulk data transfer without REST overhead; natural fit for academic batch pulls
- **GraphQL query API** — flexible ad-hoc querying over stored JSONL data; explorers can filter/aggregate without file access
- **Tick replay API** — serve historical ticks at configurable speed multiplier (1x, 10x, 100x market time); valuable for backtesting engines that need realistic feed simulation
- **Snapshot API** — point-in-time orderbook snapshot endpoint; `GET /api/snapshot/{symbol}?at=2024-01-15T14:30:00Z`
- **WebSocket multiplex protocol** — single connection, multiple symbol subscriptions, backpressure signaling; reduces connection overhead for subscribers with large universes
- **R language bindings** — `reticulate`-free native R package via `.NET Interop` or a thin REST wrapper; academic quant finance community is heavily R-based
- **DuckDB virtual table** — register MDC's JSONL/Parquet store as a DuckDB external table; enables SQL queries without data movement
- **Event-driven webhooks** — push notifications on configurable triggers (new gap detected, anomaly flagged, backfill complete); enables serverless downstream workflows
- **OpenAPI spec auto-generation** — auto-generate OpenAPI 3.1 spec from MDC's REST controllers; enables code-gen for any language client

---

## 📊 Data Quality & Validation

- **Cross-provider reconciliation** — when multiple providers are connected, flag ticks where IB and Alpaca disagree by >N bps on the same timestamp; generate quality score per symbol
- **Anomaly detection pipeline** — pluggable anomaly detectors (Z-score, IQR, price spike) that tag suspicious ticks in the JSONL stream without dropping them
- **Data completeness scorecard** — per-symbol dashboard showing: expected ticks per session vs. received, gap count, last gap timestamp, interpolation rate
- **Halts and corporate actions awareness** — ingest halt/resume events and tag data around trading halts; avoid surfacing garbage ticks during circuit breakers
- **Latency attribution** — instrument the full path from provider → storage; report per-provider median/p99 latency in the dashboard; identify if a provider is lagging
- **Synthetic data injection** — for testing: inject synthetic tick sequences that stress-test downstream consumers (flash crash simulation, gap, stale quote)
- **Tick sequence validation** — detect out-of-order timestamps, duplicate sequence numbers, and backwards price jumps that indicate feed issues rather than market events
- **Provider SLA scoring** — track uptime, reconnect frequency, message throughput, and data freshness per provider over rolling windows; generate a weekly provider health report
- **Options chain consistency checks** — validate put-call parity relationships, strike price monotonicity, and expiration date coherence in options data feeds
- **Bid-ask spread anomaly detector** — flag inverted markets, abnormally wide spreads, and locked markets as data quality events distinct from normal market microstructure

---

## ⚡ Performance & Latency

- **Kernel-bypass receive path** — optional DPDK or io_uring path for the IB/FIX socket; bypasses OS TCP stack for sub-100μs receive latency on Linux
- **SIMD tick normalizer** — vectorized price/size parsing using `System.Runtime.Intrinsics`; batch-process raw FIX/ITCH bytes 16 at a time
- **Zero-allocation tick path** — object pooling for `MarketEvent` structs, avoiding GC pressure on the hot path; use `ArrayPool<T>` for intermediate buffers
- **CPU affinity pinning** — pin the receive thread and the storage flush thread to isolated cores; reduce scheduling jitter
- **Lock-free ring buffer** — replace `BoundedChannel` on the critical path with a Disruptor-style ring buffer (LMAX pattern) for guaranteed sub-microsecond handoff
- **Nanosecond timestamps** — upgrade from `DateTime` to `long` nanoseconds since epoch on all hot-path structs; use `Stopwatch.GetTimestamp()` with hardware frequency
- **Batched WAL writes** — group WAL entries into 4KB aligned blocks for sequential disk write optimization; measure with `fio` before/after
- **GC-tuned server mode** — configure `ServerGC` with region-based collection and `GCLatencyMode.SustainedLowLatency`; document the GC tuning profile as a runbook
- **Hot-path profiling harness** — built-in `BenchmarkDotNet` suite that profiles the tick-receive-to-storage path end-to-end; run as part of CI to detect regressions
- **Memory-mapped file writes** — use `MemoryMappedFile` for WAL/JSONL output on the hot path; avoids kernel buffer copies for sequential append workloads
- **Pipeline backpressure metrics** — expose `BoundedChannel` fill level, drop count, and consumer lag as Prometheus gauges; alert before pipeline saturation

---

## 🗄️ Storage & Data Formats

- **Parquet/Arrow output** — convert JSONL daily files to Parquet at session close; columnar layout makes pandas/DuckDB queries 10-100x faster
- **QuestDB integration** — optional sink to QuestDB time-series database; enables SQL queries over streaming data with nanosecond timestamps
- **InfluxDB/TimescaleDB sink** — standard TSDB integration for users with existing monitoring infrastructure
- **Tiered cold storage** — auto-migrate data older than N days to S3/Backblaze with Zstd compression; local disk holds only hot tier
- **Deduplication store** — persistent bloom filter + hash store for exactly-once storage guarantees across restarts; currently in-memory only
- **Incremental Parquet compaction** — merge small daily Parquet files into monthly partitions; improves query performance on multi-month backtests
- **Schema evolution** — forward/backward-compatible schema registry for JSONL/Parquet format changes; version tag every file header
- **HDF5 export** — one-click export to HDF5 format for academic users coming from WRDS/TAQ ecosystems; preserves hierarchical symbol/date structure
- **Delta Lake / Iceberg table format** — store market data as a lakehouse table with ACID transactions, time travel, and schema enforcement; enables both streaming ingest and batch analytics
- **Configurable retention policies** — per-symbol or per-asset-class retention rules (e.g., keep L2 data for 30 days, L1 for 1 year, daily OHLCV forever); enforced automatically with compaction

---

## 🔗 Integrations & Ecosystem

- **Jupyter kernel extension** — `%marketdata` magic command that connects to a running MDC instance and streams ticks into a DataFrame in real time
- **pandas accessor** — `df.marketdata.resample_ohlcv('1min')` style API built on top of MDC's historical export format
- **QuantConnect LEAN bridge** — MDC as a custom data source for LEAN backtesting engine; map `IMarketDataClient` to LEAN's `IDataQueueHandler` interface
- **Backtrader data feed** — Python class wrapping MDC's REST/WebSocket endpoint as a `bt.DataBase` subclass
- **Zipline ingestion bundle** — export MDC data as a Zipline ingest bundle for Zipline-Reloaded backtesting
- **dbt integration** — MDC as a dbt source, enabling SQL-based data transformations and quality tests on stored market data
- **Grafana data source plugin** — native Grafana plugin (Go) that queries MDC's `/api` endpoints; enables blending market data with infrastructure metrics on one board
- **TradingView webhook receiver** — ingest TradingView alerts as structured events alongside market data; correlate signal firing with tick data
- **Slack/Discord bot** — alert bot that posts to a channel when: data gap detected, provider disconnected, anomalous tick received, backfill completed
- **OpenBB integration** — serve as a data backend for OpenBB Terminal; MDC provides the persistent collection layer that OpenBB lacks
- **NinjaTrader / MetaTrader bridge** — expose MDC data via the NinjaTrader Connection Adapter or MetaTrader EA interface; captures the retail algo trading audience
- **Dagster / Airflow operator** — custom operator for orchestrating MDC backfill jobs, Parquet compaction, and data quality checks inside existing data pipelines

---

## 🌐 Community & Sharing

- **Public data snapshots** — opt-in feature to publish anonymized daily OHLCV snapshots to a shared S3 bucket; community-maintained free data layer
- **Provider plugin SDK** — documented interface + CLI scaffolding tool for third parties to build new `IMarketDataClient` implementations; npm-style plugin registry
- **Strategy template library** — curated set of Jupyter notebooks demonstrating: loading MDC data → feature engineering → backtest → performance report
- **Discord/forum integration** — built-in "share session" feature that posts a summary of today's collection run (symbols, tick count, anomalies) to a community feed
- **Contribution leaderboard** — gamified: users who contribute provider plugins or data quality validators earn recognition on a public leaderboard
- **Data marketplace** — users who collect rare data (options chains, crypto perps, international equities) can make it discoverable to others with access controls

---

## 🖥️ UX / Developer Experience

- **Interactive setup wizard** — terminal TUI (Spectre.Console) that walks through provider selection, API key entry, symbol configuration, and validates connectivity before first run
- **Symbol universe browser** — searchable UI (web dashboard) for browsing available symbols per provider; click-to-subscribe without editing JSON config
- **Data quality report (PDF export)** — one-click PDF report of collection quality for a date range; useful for academic data provenance documentation
- **Live tick visualizer** — real-time candlestick chart in the web dashboard for any subscribed symbol; no external charting tool needed
- **Order book heatmap** — animated L2 order book depth visualization in the web dashboard; visually identify spoofing, iceberg orders
- **Config diff viewer** — when appsettings.json changes, show a visual diff of what changed and what the system will do differently
- **Backfill progress UX** — rich progress bars for historical backfill jobs: symbol × date range × provider, with ETA, pause/resume, and error summary
- **CLI REPL** — `mdc> subscribe AAPL` style interactive shell for power users; tab-completion for symbols and commands
- **WPF theme system** — light/dark mode toggle plus a theme engine for the desktop app; reduces eye strain during long monitoring sessions and demonstrates UI polish
- **In-app diagnostics panel** — embedded panel showing pipeline throughput, channel fill levels, GC pause times, and provider connection state; replaces the need to open Grafana for quick checks
- **Config schema validation with friendly errors** — validate `appsettings.json` on startup against a JSON Schema; surface human-readable error messages in the WPF app instead of cryptic exceptions

---

## 🔐 Reliability & Production Readiness

- **Multi-provider active-active** — subscribe to the same symbol on two providers simultaneously; merge streams, use cross-provider reconciliation to detect and correct bad ticks
- **Intelligent failover** — when primary provider disconnects, automatically switch to secondary; track gap introduced by failover; backfill from secondary on reconnect
- **Health scoring per symbol** — composite score (0-100) for each symbol based on: tick rate vs. expected, anomaly rate, gap frequency; surface in dashboard as RAG status
- **Alerting engine** — configurable rules: `if health_score(AAPL) < 80 for 5min → notify`; delivery via email, webhook, Slack
- **Chaos mode** — optional fault injection (random disconnect, tick drop, latency spike) for testing downstream resilience; controlled via CLI flag
- **Rolling restart support** — drain in-flight events before shutdown; resume without data loss; important for Kubernetes rolling deployments
- **Multi-region replication** — async replication of JSONL/Parquet files to a secondary region; DR for institutional users

---

## 📚 Academic & Research

- **Data provenance ledger** — every stored file gets a hash, timestamp, provider version, and collection parameter snapshot; immutable append-only log satisfies academic reproducibility requirements
- **Citation generator** — one-click: "Generate BibTeX citation for this dataset" that produces a citable reference with DOI-style identifier
- **Dataset versioning** — snapshot the full symbol universe configuration at each session; enables exact replay of what was collected on a given date
- **Tick quality metadata** — alongside each tick file, store: source exchange, feed type (CTA/SIP vs direct), latency bucket, anomaly flags; enables downstream filtering
- **Research pack export** — bundle a date range of data for multiple symbols into a single reproducible ZIP with README, schema documentation, and provenance ledger
- **Regulatory data retention mode** — WORM (Write Once Read Many) storage policy; files are checksummed and cannot be modified; audit log of all access

---

## 🏗️ Architecture & Refactoring

- **ViewModel extraction audit** — systematically identify code-behind logic in WPF Pages/Windows that should live in ViewModels; produce a migration checklist per view with effort estimates
- **DI container for WPF shell** — introduce `Microsoft.Extensions.DependencyInjection` into the WPF application host; eliminate manual service wiring and enable constructor injection across all ViewModels
- **Hot-path / cold-path separation** — physically separate the real-time tick pipeline (hot) from config management, backfill orchestration, and UI (cold) into distinct assemblies; enables independent deployment and testing
- **Interface segregation pass** — audit `IMarketDataClient` and `IStorageSink` for ISP violations; split fat interfaces into focused contracts (e.g., `ITickReceiver`, `IOrderBookReceiver`, `IHistoricalFetcher`)
- **Event pipeline modularization** — refactor `EventPipeline` into composable middleware stages (receive → normalize → validate → route → store); each stage independently testable and replaceable
- **Configuration as strongly-typed objects** — replace raw `IConfiguration` indexing with Options pattern (`IOptions<ProviderSettings>`, `IOptions<StorageSettings>`) everywhere; compile-time safety + validation via `IValidateOptions<T>`
- **Shared kernel extraction** — extract domain primitives (Symbol, Timestamp, Price, Quantity, MarketEvent) into a standalone `Meridian.Domain` assembly with zero infrastructure dependencies; consumed by all layers
- **Plugin architecture for sinks** — replace hardcoded storage sink registration with a MEF/plugin-style discovery system; third parties drop a DLL into a `/plugins` folder and it's auto-registered as an `IStorageSink`

---

## 📈 User Growth & Adoption

- **"Zero to first tick" quickstart** — a single `docker compose up` command that starts MDC with Alpaca's free tier, subscribes to SPY, and shows live ticks in the web dashboard within 60 seconds; optimize ruthlessly for time-to-value
- **YouTube / blog content series** — "Building a Quant Data Pipeline" tutorial series showing MDC as the backbone; each episode adds a capability (backfill, Jupyter, backtest); drives organic search traffic
- **GitHub README overhaul** — hero GIF showing live ticks flowing, architecture diagram, one-click deploy badges, and "Why MDC?" comparison table vs. alternatives; first 30 seconds of the README determine star/bounce
- **Integration showcase gallery** — web page with working code snippets for each integration (Jupyter, LEAN, pandas, Grafana); copy-paste ready; each snippet links to a deeper tutorial
- **Conference talk / meetup circuit** — target QuantConnect community meetups, .NET Conf, and local quant finance groups; live demo of MDC collecting options data in real-time
- **Freemium cloud-hosted tier** — a managed MDC instance with limited symbol count (5 symbols, delayed data); removes all setup friction; converts to self-hosted when users hit limits
- **First-run analytics** — anonymous telemetry (opt-in) tracking: setup completion rate, first successful tick received, first query executed; identify and fix the biggest drop-off points
- **Contributor onboarding guide** — a `CONTRIBUTING.md` with architecture walkthrough, "good first issue" labels, and a mentorship channel; converts users into contributors

---

## 🧹 Technical Debt & Code Quality

- **Mutation testing baseline** — run Stryker.NET against the test suite to measure real test effectiveness beyond line coverage; identify tests that pass regardless of code changes
- **Architecture decision records (ADRs)** — establish a `docs/adr/` directory; document every significant technical decision (why WPF over UWP, why BoundedChannel over Disruptor, why JSONL first); reduces re-litigation
- **Static analysis gate in CI** — add Roslyn analyzers + `.editorconfig` enforcement to the GitHub Actions pipeline; fail the build on MVVM violations (e.g., code-behind referencing services directly)
- **Dead code elimination sweep** — run a coverage + reference analysis to identify unreachable code paths, unused interfaces, and orphaned files left over from the UWP migration; reduce cognitive load
- **Test isolation audit** — identify integration tests that depend on external state (file system, network, time); refactor to use `ITimeProvider`, in-memory file systems, and test doubles; reduce flaky test rate
- **Dependency freshness dashboard** — automated PR (via Dependabot or similar) for NuGet package updates; track how far behind each dependency is; flag security-relevant updates
- **XML doc coverage requirement** — enforce `<summary>` documentation on all public APIs via a Roslyn analyzer; auto-generate API reference docs from XML comments in CI
- **Consistent error handling patterns** — audit and standardize exception handling across the codebase; replace bare `catch (Exception)` blocks with typed exceptions, Result types, or structured logging; establish a project-wide error taxonomy
