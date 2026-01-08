# Architecture Diagrams

**Last Updated:** 2026-01-08
**Version:** 1.5.0

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
| **Microservices** | Optional distributed microservices deployment | `microservices-architecture.dot` |

---

## Diagram Descriptions

### C4 Level 1: System Context

Shows the Market Data Collector in context with:
- **Users**: Operators and Quants/Analysts
- **Data Providers**: IB, Alpaca, Yahoo, Tiingo, Finnhub, Polygon, etc.
- **Downstream Systems**: QuantConnect Lean, Python/Pandas, PostgreSQL
- **Monitoring**: Prometheus/Grafana

### C4 Level 2: Container Diagram

Shows the major deployable units:
- **UWP Desktop App** (15 pages)
- **Core Collector Service** (.NET 9 Console)
- **F# Domain Library**
- **Microservices** (6 services + Gateway)
- **Message Bus** (RabbitMQ/MassTransit)
- **Storage Layer** (JSONL, Parquet, WAL)

### C4 Level 3: Component Diagram

Detailed view of the core collector internals:
- **Infrastructure Layer**: Provider clients, connection management, resilience
- **Domain Layer**: Collectors, domain models
- **Application Layer**: Pipeline, indicators, tracing, config
- **Storage Layer**: Sinks, archival, export
- **Messaging Layer**: Publishers

### Data Flow Diagram

Shows data moving through the system:
1. **Ingestion** (streaming + historical)
2. **Processing** (collectors, validation, indicators)
3. **Pipeline** (bounded channel, publisher)
4. **Storage** (WAL → JSONL → Parquet)
5. **Export** (Python, R, Lean, PostgreSQL)

### Provider Architecture

Details the provider abstraction:
- **Interfaces**: IMarketDataClient, IHistoricalDataProvider
- **Streaming Providers**: IB, Alpaca, Polygon (stub), StockSharp, NYSE
- **Historical Providers**: 9 implemented (Alpaca, Yahoo, Stooq, Nasdaq, Tiingo, Finnhub, Alpha Vantage, Polygon)
- **Resilience**: Connection managers, failover, circuit breakers
- **Symbol Resolution**: OpenFIGI, SymbolMapper

### Storage Architecture

Details the storage pipeline:
- **Write Path**: WAL with transaction semantics
- **Hot Storage**: JSONL append-only files
- **Compression**: LZ4/ZSTD/Gzip tiered profiles
- **Archive**: Parquet columnar storage
- **Tiered Storage**: Hot (SSD) → Warm (HDD) → Cold (S3)
- **Export**: Multi-format (Python, R, Lean, Excel, PostgreSQL)
- **Quality**: Completeness scoring, outlier detection

### Microservices Architecture

Shows the optional distributed deployment:
- **API Gateway** (:5000)
- **Quote Service** (:5001)
- **Trade Service** (:5002)
- **OrderBook Service** (:5003)
- **Historical Service** (:5004)
- **Validation Service** (:5005)
- **Message Bus**: RabbitMQ with MassTransit
- **Observability**: Prometheus, Jaeger, Grafana

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

The diagrams use a consistent color palette:

| Color | Hex | Usage |
|-------|-----|-------|
| Dark Blue | `#08427b` | Actors/Users |
| Blue | `#438dd5` | Primary containers |
| Light Blue | `#dbeafe` | Infrastructure components |
| Teal | `#2c7a7b` | Domain components |
| Green | `#d1fae5` | Domain layer |
| Purple | `#805ad5` | Application layer |
| Light Purple | `#ede9fe` | Application components |
| Red | `#c53030` | Storage layer |
| Light Red | `#fee2e2` | Storage components |
| Gray | `#999999` | External systems |
| Orange | `#ff6b6b` | Messaging layer |

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
