# Architecture Diagrams

**Last Updated:** 2026-01-27
**Version:** 1.0.0

This folder contains architecture diagrams for the Market Data Collector system in Graphviz DOT format.

---

## Diagram Index

| Diagram | Description | File |
|---------|-------------|------|
| **C4 Level 1: Context** | System context showing actors and external systems | `c4-level1-context.dot` |
| **C4 Level 2: Containers** | High-level container view (apps, services, storage) | `c4-level2-containers.dot` |
| **C4 Level 3: Components** | Internal component architecture of core collector | `c4-level3-components.dot` |
| **Data Flow** | End-to-end data flow from sources to export | `data-flow.dot` |
| **Provider Architecture** | Data provider abstraction and implementation | `provider-architecture.dot` |
| **Storage Architecture** | Storage pipeline with WAL, compression, tiering | `storage-architecture.dot` |
| **Event Pipeline Sequence** | Detailed event processing sequence | `event-pipeline-sequence.dot` |
| **Resilience Patterns** | Circuit breakers, retry, failover patterns | `resilience-patterns.dot` |
| **Deployment Options** | Standalone to Kubernetes deployment paths | `deployment-options.dot` |
| **Onboarding Flow** (NEW) | User journey from first-run to operation | `onboarding-flow.dot` |
| **CLI Commands** (NEW) | All CLI flags and commands reference | `cli-commands.dot` |

---

## Diagram Descriptions

### C4 Level 1: System Context

Shows the Market Data Collector in context with:
- **Users**: Operators, Quants/Analysts, and DevOps
- **Streaming Providers**: IB, Alpaca, NYSE Direct, StockSharp
- **Historical Providers**: Yahoo, Tiingo, Finnhub, Polygon, Stooq, and more (9 total)
- **Downstream Systems**: QuantConnect Lean, Python/R Analytics, PostgreSQL/TimescaleDB
- **Infrastructure**: Storage (JSONL/Parquet), Message Bus (RabbitMQ), Monitoring (Prometheus/Grafana)

### C4 Level 2: Container Diagram

Shows the major deployable units:
- **Presentation Layer**: UWP Desktop App, Web Dashboard, CLI Interface (11+ flags)
- **Onboarding & Diagnostics Layer** (NEW): Configuration Wizard, Auto-Configuration, Diagnostic Services, Error Formatter
- **Core Collector Service** (.NET 9 Console with 100K bounded channel)
- **F# Domain Library** (Type-safe validation, discriminated unions)
- **Contracts Library** (Shared DTOs, MassTransit contracts)
- **Microservices** (6 services + Gateway on ports 5000-5005)
- **Message Bus** (RabbitMQ/Azure Service Bus with MassTransit)
- **Storage Layer** (WAL → JSONL → Parquet with tiered storage)
- **Observability** (Prometheus, Grafana, Jaeger/Tempo)

### C4 Level 3: Component Diagram

Detailed view of the core collector internals:
- **Infrastructure Layer**: Streaming clients (IB, Alpaca, NYSE, StockSharp), Historical providers (9), Connection/Resilience management, Performance optimizations (source-generated JSON, connection warmup)
- **Domain Layer**: Collectors (Trade, Quote, Depth), Domain models, F# validation pipeline
- **Application Layer**: EventPipeline (100K bounded channel), Technical indicators (200+), Config/Monitoring, Backfill service, **Onboarding & Diagnostics** (8 services: AutoConfiguration, Wizard, FirstRunDetector, ConnectivityTest, CredentialValidation, ErrorFormatter, ProgressDisplay, StartupSummary)
- **Storage Layer**: Write path (WAL, JSONL, Parquet), Compression profiles, Export service, Quality reporting

### Data Flow Diagram

Shows data moving through the system:
0. **First-Time Setup** (NEW): First-run detection → Wizard/Auto-config → Generate appsettings.json
1. **Streaming Sources**: IB, Alpaca, NYSE, StockSharp → Real-time ingestion
2. **Historical Sources**: Yahoo, Tiingo, Finnhub, Polygon → Batch backfill
3. **Processing**: Domain collectors → F# validation → Technical indicators
4. **Pipeline**: Bounded channel (100K) → Composite publisher
5. **Storage**: WAL → JSONL (hot) → Compression → Parquet (archive) → Tiered storage
6. **Export**: Python/Pandas, R, QuantConnect Lean, Excel, PostgreSQL
7. **Optional Messaging**: RabbitMQ → Microservice consumers

### Provider Architecture

Details the provider abstraction:
- **Core Interfaces**: IDataSource, IRealtimeDataSource, IHistoricalDataSource
- **Legacy Interfaces**: IMarketDataClient, IHistoricalDataProvider
- **Streaming Providers (4 Production + 1 Stub)**: IB, Alpaca, NYSE, StockSharp, Polygon (stub)
- **Historical Providers (9 in 3 tiers)**:
  - Tier 1 (Priority 5): NYSE, Alpaca
  - Tier 2 (Priority 10-20): Yahoo, Polygon, Stooq
  - Tier 3 (Priority 25+): Tiingo, Nasdaq, Finnhub, Alpha Vantage
- **Resilience**: EnhancedIBConnectionManager, WebSocketResiliencePolicy, AutomaticFailoverManager, CircuitBreaker, RateLimiter
- **Symbol Resolution**: OpenFIGI, SymbolMapper, ContractFactory
- **CompositeHistoricalDataProvider**: Automatic failover, rate-limit rotation, priority selection

### Storage Architecture

Details the archival-first storage pipeline:
- **Write-Ahead Log**: Crash-safe journal, SHA256 checksums, NoSync/BatchedSync/EveryWrite modes
- **Hot Storage (JSONL)**: Append-only, daily partitioning, multiple naming conventions
- **Compression Profiles**: LZ4 (real-time), ZSTD Level 3/6/19 (high-volume/warm/cold), Gzip (portable)
- **Archive Storage (Parquet)**: Columnar format, 10-20x compression, schema versioning
- **Tiered Storage**: Hot (SSD, 0-7d) → Warm (HDD, 7-30d) → Cold (S3/Glacier, 30d+)
- **Export Formats**: Python/Pandas, R Statistics, QuantConnect Lean, Excel, PostgreSQL
- **Quality Assessment**: Multi-dimensional scoring, outlier detection (4σ), A+ to F grading

### Event Pipeline Sequence (NEW)

Shows the detailed event processing sequence:
1. **Data Source** → Raw events from provider WebSocket
2. **Provider Client** → Normalize to domain updates
3. **Domain Collectors** → Process trades/quotes/depth, emit domain events
4. **F# Validation** → Railway-oriented type-safe validation
5. **Event Pipeline** → Bounded channel async write/read
6. **Composite Publisher** → Fanout to configured sinks
7. **Storage Path** → WAL journal → JSONL persist
8. **Messaging Path** (optional) → MassTransit → RabbitMQ → Consumers
9. **Observability** → Metrics, traces, status endpoints

### Resilience Patterns (NEW)

Shows fault tolerance mechanisms:
- **Circuit Breaker**: Closed → Open → Half-Open states, configurable thresholds
- **Retry Pattern**: Exponential backoff with jitter, max 5 attempts
- **Provider Failover**: Priority-based, health-monitored automatic switching
- **Historical Provider Failover**: CompositeProvider with 3-tier priority chain
- **Rate Limiting**: Token bucket per provider, configurable limits
- **Connection Management**: State machine, heartbeat monitoring, auto-reconnect
- **Graceful Degradation**: Full → Partial → Minimal service levels

### Deployment Options

Shows deployment paths from simple to enterprise:
1. **Standalone Console** - Single .NET app, local storage, simplest setup
2. **Docker Compose** - Containerized, service orchestration, team development
3. **Docker Microservices** - Distributed architecture, horizontal scaling
4. **Kubernetes** - Cloud-native, HPA auto-scaling, self-healing
5. **Cloud Managed** - Azure AKS/Service Bus, AWS EKS/MQ/S3
6. **System Service** - systemd (Linux), Windows Service for bare metal
7. **Pre-Deployment Setup** (NEW) - First-time configuration via wizard or auto-config

### Onboarding Flow (NEW)

Shows the user journey from first-run to operational:
1. **First-Run Detection** - Automatic detection of new installations
2. **Setup Options**:
   - `--wizard` - Interactive 8-step configuration wizard
   - `--auto-config` - Quick auto-configuration from environment variables
   - `--generate-config <type>` - Template generation (minimal, full, alpaca, etc.)
   - `--detect-providers` - Scan and display available providers
3. **Configuration Wizard Steps**:
   - Detect providers → Select use case → Configure data source
   - Configure symbols → Configure storage → Configure backfill
   - Review settings → Save configuration
4. **Validation & Diagnostics**:
   - `--validate-credentials` - Test API keys
   - `--test-connectivity` - Network diagnostics
   - `--quick-check` - Fast health check
   - `--dry-run` - Full test without data collection
5. **Error Handling** - FriendlyErrorFormatter with 24 error codes (MDC-XXX-NNN)

### CLI Commands Reference (NEW)

Comprehensive reference for all CLI flags:
- **Setup Commands**: --wizard, --auto-config, --generate-config, --detect-providers
- **Diagnostic Commands**: --validate-credentials, --test-connectivity, --quick-check, --validate-config, --dry-run
- **Operation Commands**: --ui, --http-port, --backfill, --backfill-provider, --backfill-symbols, --backfill-from, --backfill-to
- **Documentation Commands**: --show-config, --error-codes, --help, --version
- **Common Workflows**: New installation, CI/CD setup, troubleshooting, backfill operations
- **Exit Codes**: 0 (success), 1 (general error), 2 (config error), 3 (connection error), 4 (auth error), 5 (validation failed)

---

## Generating Images

### Prerequisites

Install Graphviz:

```bash
# Ubuntu/Debian
sudo apt-get install graphviz

# macOS
brew install graphviz

# Windows (via Chocolatey)
choco install graphviz
```

### Generate PNG Images

```bash
cd docs/diagrams

# Generate all PNGs
for f in *.dot; do
  dot -Tpng "$f" -o "${f%.dot}.png"
done

# Or generate individual files
dot -Tpng c4-level1-context.dot -o c4-level1-context.png
dot -Tpng c4-level2-containers.dot -o c4-level2-containers.png
dot -Tpng c4-level3-components.dot -o c4-level3-components.png
dot -Tpng data-flow.dot -o data-flow.png
dot -Tpng provider-architecture.dot -o provider-architecture.png
dot -Tpng storage-architecture.dot -o storage-architecture.png
dot -Tpng microservices-architecture.dot -o microservices-architecture.png
dot -Tpng event-pipeline-sequence.dot -o event-pipeline-sequence.png
dot -Tpng resilience-patterns.dot -o resilience-patterns.png
dot -Tpng deployment-options.dot -o deployment-options.png
dot -Tpng onboarding-flow.dot -o onboarding-flow.png
dot -Tpng cli-commands.dot -o cli-commands.png
```

### Generate SVG Images

```bash
# Generate all SVGs
for f in *.dot; do
  dot -Tsvg "$f" -o "${f%.dot}.svg"
done
```

### High-DPI PNG (for presentations)

```bash
dot -Tpng -Gdpi=300 c4-level2-containers.dot -o c4-level2-containers-hd.png
```

---

## Color Scheme

The diagrams use a consistent color palette based on Tailwind CSS colors:

| Color | Hex | Usage |
|-------|-----|-------|
| **Actors** | | |
| Dark Blue | `#08427b` | Persons/Users |
| **System Boundary** | | |
| Blue | `#438dd5` | Core system containers |
| Light Blue | `#4a90d9` | Streaming providers |
| Pale Blue | `#dbeafe` / `#bfdbfe` | Infrastructure components |
| **Domain Layer** | | |
| Teal | `#2c7a7b` | Domain/Microservices |
| Green | `#38a169` | Historical providers |
| Light Green | `#d1fae5` / `#a7f3d0` | Domain components |
| Mint | `#6ee7b7` | F# components |
| **Application Layer** | | |
| Purple | `#805ad5` | Application/Pipeline layer |
| Light Purple | `#ede9fe` / `#ddd6fe` | Application components |
| **Storage Layer** | | |
| Red | `#c53030` / `#e53e3e` | Storage/Critical systems |
| Light Red | `#fee2e2` / `#fecaca` | Storage components |
| Hot tier | `#fc8181` | WAL, Hot storage |
| Warm tier | `#fbd38d` | JSONL, Compression |
| **Messaging** | | |
| Bright Red | `#e53e3e` | Message bus/RabbitMQ |
| Light Coral | `#feb2b2` | Message consumers |
| **Infrastructure** | | |
| Orange | `#ed8936` | HTTP/Monitoring servers |
| Gray | `#718096` | External/Shared systems |
| Light Gray | `#e2e8f0` | System services |
| **Onboarding/UX** | | |
| Orange | `#ed8936` | Onboarding services |
| Light Orange | `#fbd38d` | Setup/Config components |
| Pale Orange | `#feebc8` | Wizard steps |
| Cream | `#fffaf0` | Onboarding background |
| **Export/Quality** | | |
| Export Blue | `#2b6cb0` | Export layer |
| Quality Green | `#9ae6b4` | Quality/Observability |
| **Status Indicators** | | |
| Success Green | `#9ae6b4` | Healthy/Production |
| Warning Yellow | `#fef3c7` / `#fbd38d` | Config required/Warning |
| Error Red | `#fecaca` | Stub/Error/Failed |

---

## C4 Model Reference

These diagrams follow the [C4 Model](https://c4model.com/) notation:

- **Level 1 (Context)**: System context - how the system fits into the world
- **Level 2 (Container)**: High-level technology choices and deployment units
- **Level 3 (Component)**: Major structural building blocks within a container
- **Level 4 (Code)**: Not included (use IDE for code-level exploration)

---

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [C4 Diagrams Reference](../architecture/c4-diagrams.md)
- [Production Status](../status/production-status.md)
- [Provider Management](../architecture/provider-management.md)

---

*Diagrams generated with Graphviz DOT language*
