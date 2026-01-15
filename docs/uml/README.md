# UML Diagrams - Market Data Collector

This directory contains UML diagrams documenting the Market Data Collector system architecture, workflows, and interactions.

## Diagram Types

### 1. Use Case Diagram
**File:** `use-case-diagram.puml`

Defines user interactions with the system, showing:
- **Actors:** CLI User, Web Dashboard User, API Client, System Operator, Desktop App User
- **External Systems:** Data Providers (IB, Alpaca), Message Broker, Kubernetes
- **Use Cases:** Real-time collection, backfill, configuration, monitoring, microservices API

### 2. Sequence Diagrams
Shows step-by-step interactions between objects:

| File | Description |
|------|-------------|
| `sequence-diagram.puml` | Real-time data collection flow from provider to storage |
| `sequence-diagram-backfill.puml` | Historical backfill process with fallback |
| `sequence-diagram-microservices.puml` | Microservices API ingestion and routing |

### 3. Activity Diagrams
Represents processes, decision flows, and dynamic system behavior:

| File | Description |
|------|-------------|
| `activity-diagram.puml` | Main data collection process with parallel tasks |
| `activity-diagram-backfill.puml` | Historical backfill workflow with provider fallback |

### 4. State Diagrams
Tracks object states and transitions:

| File | Description |
|------|-------------|
| `state-diagram.puml` | Provider connection lifecycle (Disconnected → Connected → Failed) |
| `state-diagram-orderbook.puml` | Order book stream states (Fresh → Stale → AutoReset) |
| `state-diagram-trade-sequence.puml` | Trade sequence validation states (Normal → Gap → OutOfOrder) |
| `state-diagram-backfill.puml` | Backfill request lifecycle (Pending → InProgress → Completed) |

### 5. Communication Diagram
**File:** `communication-diagram.puml`

Shows message exchange between components:
- Provider to Collector data flow
- Pipeline to Storage communication
- Microservices routing
- MassTransit message publishing

### 6. Interaction Overview Diagram
**File:** `interaction-overview-diagram.puml`

Combines multiple interactions into a high-level view:
- Initialization sequence
- Mode selection (Real-Time, Backfill, Microservices)
- Parallel task execution
- Graceful shutdown

### 7. Timing Diagrams
Visualizes event timing and synchronization:

| File | Description |
|------|-------------|
| `timing-diagram.puml` | Real-time event processing timeline (~ms scale) |
| `timing-diagram-backfill.puml` | Backfill operation timeline (~seconds scale) |

## Rendering the Diagrams

### Option 1: PlantUML Online Server
Visit [PlantUML Web Server](http://www.plantuml.com/plantuml/uml/) and paste the diagram content.

### Option 2: VS Code Extension
Install the [PlantUML extension](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml) for VS Code:
```bash
code --install-extension jebbs.plantuml
```
Then use `Alt+D` to preview diagrams.

### Option 3: Command Line
Install PlantUML and generate images:
```bash
# Install PlantUML (requires Java)
brew install plantuml  # macOS
apt install plantuml   # Ubuntu/Debian

# Generate PNG images
plantuml docs/uml/*.puml

# Generate SVG images
plantuml -tsvg docs/uml/*.puml
```

### Option 4: Docker
```bash
docker run -v $(pwd)/docs/uml:/data plantuml/plantuml -tsvg /data/*.puml
```

### Option 5: GitHub/GitLab Rendering
Many Git platforms render PlantUML diagrams automatically in markdown:
```markdown
![Use Case Diagram](http://www.plantuml.com/plantuml/proxy?src=https://raw.githubusercontent.com/user/repo/main/docs/uml/use-case-diagram.puml)
```

## Diagram Summary

| Diagram Type | Count | Files |
|--------------|-------|-------|
| Use Case | 1 | `use-case-diagram.puml` |
| Sequence | 3 | `sequence-diagram*.puml` |
| Activity | 2 | `activity-diagram*.puml` |
| State | 4 | `state-diagram*.puml` |
| Communication | 1 | `communication-diagram.puml` |
| Interaction Overview | 1 | `interaction-overview-diagram.puml` |
| Timing | 2 | `timing-diagram*.puml` |
| **Total** | **14** | |

## Key System Components Documented

### Actors
- CLI User, Web Dashboard User, API Client, Operator, Desktop User
- External: Interactive Brokers, Alpaca, Polygon, Yahoo Finance, Stooq

### Core Components
- **Collectors:** QuoteCollector, TradeDataCollector, MarketDepthCollector
- **Pipeline:** EventPipeline (bounded channel, 50K capacity)
- **Storage:** JsonlStorageSink, ParquetStorageSink
- **Providers:** AlpacaMarketDataClient, IBMarketDataClient, CompositeHistoricalProvider

### Microservices
- Gateway (5000), TradeIngestion (5001), QuoteIngestion (5002)
- OrderBookIngestion (5003), HistoricalDataIngestion (5004), DataValidation (5005)

### Key Workflows
1. Real-time streaming data collection
2. Historical backfill with provider fallback
3. Microservices API ingestion and routing
4. Configuration hot-reload
5. Integrity detection and recovery

## Updating Diagrams

When modifying system architecture:
1. Update relevant `.puml` files
2. Regenerate images if stored in repo
3. Verify diagrams render correctly
4. Update this README if adding new diagrams

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Domain Contracts](../architecture/domains.md)
- [Provider Setup Guides](../providers/)
- [AI Assistant Guides](../ai-assistants/)
