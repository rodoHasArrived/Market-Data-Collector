# Data Ingestion Microservices

A production-ready microservices architecture for high-throughput market data ingestion, processing, and validation.

## Architecture Overview

```
                                    ┌─────────────────┐
                                    │   Data Sources  │
                                    │ (IB/Alpaca/etc) │
                                    └────────┬────────┘
                                             │
                                    ┌────────▼────────┐
                                    │   API Gateway   │
                                    │    (Port 5000)  │
                                    └────────┬────────┘
                                             │
                         ┌───────────────────┼───────────────────┐
                         │                   │                   │
               ┌─────────▼─────────┐ ┌───────▼───────┐ ┌─────────▼─────────┐
               │ Trade Ingestion   │ │   OrderBook   │ │  Quote Ingestion  │
               │   (Port 5001)     │ │  (Port 5002)  │ │    (Port 5003)    │
               └─────────┬─────────┘ └───────┬───────┘ └─────────┬─────────┘
                         │                   │                   │
                         └───────────────────┼───────────────────┘
                                             │
                         ┌───────────────────┼───────────────────┐
                         │                   │                   │
               ┌─────────▼─────────┐ ┌───────▼───────┐ ┌─────────▼─────────┐
               │    Historical     │ │  Validation   │ │      Storage      │
               │   (Port 5004)     │ │  (Port 5005)  │ │    (JSONL/DB)     │
               └───────────────────┘ └───────────────┘ └───────────────────┘
```

## Services

### 1. API Gateway (`Gateway/`)
- **Port:** 5000
- **Purpose:** Entry point for all data ingestion
- **Features:**
  - Request routing based on data type
  - Rate limiting (sliding window)
  - Provider connection management
  - Subscription management
  - Prometheus metrics endpoint

### 2. Trade Ingestion Service (`TradeIngestion/`)
- **Port:** 5001
- **Purpose:** Process tick-by-tick trade data
- **Features:**
  - High-throughput channel-based processing
  - Sequence validation and deduplication
  - Trade validation (price, size, timestamp)
  - JSONL storage with optional compression
  - Order flow aggregation

### 3. OrderBook Ingestion Service (`OrderBookIngestion/`)
- **Port:** 5002
- **Purpose:** Maintain L2 order book state
- **Features:**
  - Snapshot and delta update processing
  - Integrity checking (gaps, out-of-order)
  - Configurable depth levels
  - Periodic snapshot persistence
  - Book freeze on integrity errors

### 4. Quote Ingestion Service (`QuoteIngestion/`)
- **Port:** 5003
- **Purpose:** Process BBO/NBBO quotes
- **Features:**
  - Spread calculation and enrichment
  - Crossed/locked market detection
  - Latest quote state tracking
  - High-frequency quote processing

### 5. Historical Data Service (`HistoricalDataIngestion/`)
- **Port:** 5004
- **Purpose:** Backfill historical data
- **Features:**
  - Job-based backfill management
  - Progress tracking and reporting
  - Multiple data source support
  - Retry and error handling
  - REST API for job management

### 6. Validation Service (`DataValidation/`)
- **Port:** 5005
- **Purpose:** Data quality and validation
- **Features:**
  - Trade/Quote/OrderBook validation rules
  - Quality metrics aggregation
  - Alert generation for quality drops
  - Configurable validation thresholds
  - Per-symbol quality tracking

## Quick Start

### Prerequisites
- Docker and Docker Compose
- .NET 9.0 SDK (for local development)

### Start All Services

```bash
cd src/Microservices

# Build all services
docker compose -f docker-compose.microservices.yml build

# Start core services
docker compose -f docker-compose.microservices.yml up -d

# Start with monitoring (Prometheus + Grafana)
docker compose -f docker-compose.microservices.yml --profile monitoring up -d
```

### Local Development

```bash
# Build shared contracts
cd Shared && dotnet build

# Run individual service
cd Gateway && dotnet run

# Run all services (use separate terminals)
dotnet run --project Gateway/DataIngestion.Gateway.csproj
dotnet run --project TradeIngestion/DataIngestion.TradeService.csproj
dotnet run --project OrderBookIngestion/DataIngestion.OrderBookService.csproj
dotnet run --project QuoteIngestion/DataIngestion.QuoteService.csproj
dotnet run --project HistoricalDataIngestion/DataIngestion.HistoricalService.csproj
dotnet run --project DataValidation/DataIngestion.ValidationService.csproj
```

## API Examples

### Ingest a Trade

```bash
curl -X POST http://localhost:5000/api/v1/ingest/trades \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "AAPL",
    "price": 185.50,
    "size": 100,
    "aggressorSide": "buy",
    "source": "Alpaca"
  }'
```

### Ingest a Quote

```bash
curl -X POST http://localhost:5000/api/v1/ingest/quotes \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "AAPL",
    "bidPrice": 185.49,
    "bidSize": 500,
    "askPrice": 185.51,
    "askSize": 300,
    "source": "Alpaca"
  }'
```

### Subscribe to Market Data

```bash
curl -X POST http://localhost:5000/api/v1/subscriptions \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "AAPL",
    "provider": "Alpaca",
    "types": ["Trades", "Quotes"]
  }'
```

### Start Historical Backfill

```bash
curl -X POST http://localhost:5004/api/v1/backfill \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "AAPL",
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-01-31T23:59:59Z",
    "dataType": "Trades",
    "source": "Yahoo"
  }'
```

### Check Data Quality

```bash
curl http://localhost:5005/api/v1/quality/symbols/AAPL
```

## Configuration

Each service is configured via `appsettings.json` or environment variables:

```json
{
  "ServiceName": {
    "HttpPort": 5000,
    "MessageBus": {
      "Transport": "RabbitMQ",
      "RabbitMq": {
        "Host": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest"
      }
    },
    "Storage": {
      "DataDirectory": "data"
    }
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_URLS` | HTTP binding URL |
| `*__MessageBus__Transport` | Transport type (InMemory/RabbitMQ) |
| `*__MessageBus__RabbitMq__Host` | RabbitMQ host |
| `*__Storage__DataDirectory` | Data storage path |

## Health Checks

All services expose health endpoints:

- `/health` - Detailed health status (JSON)
- `/live` - Kubernetes liveness probe
- `/ready` - Kubernetes readiness probe
- `/metrics` - Prometheus metrics

## Message Contracts

See `Shared/Contracts/Messages/` for message definitions:

- `IIngestionMessage` - Base message interface
- `IRawTradeIngested` - Trade data message
- `IRawQuoteIngested` - Quote data message
- `IRawOrderBookIngested` - Order book snapshot
- `IRequestHistoricalBackfill` - Backfill command
- `IValidateIngestionData` - Validation request

## Scaling

Services can be horizontally scaled:

```bash
# Scale trade ingestion to 3 instances
docker compose -f docker-compose.microservices.yml up -d --scale trade-ingestion=3
```

## Monitoring

With the `monitoring` profile:

- **Prometheus:** http://localhost:9090
- **Grafana:** http://localhost:3000 (admin/admin)
- **RabbitMQ Management:** http://localhost:15672 (ingestion/ingestion-secret)

## Project Structure

```
src/Microservices/
├── Shared/                      # Shared contracts and configuration
│   ├── Contracts/Messages/      # Message interfaces
│   ├── Configuration/           # Shared config classes
│   └── Services/                # Service interfaces
├── Gateway/                     # API Gateway
├── TradeIngestion/              # Trade processing
├── OrderBookIngestion/          # Order book management
├── QuoteIngestion/              # Quote processing
├── HistoricalDataIngestion/     # Historical backfill
├── DataValidation/              # Data quality
├── docker-compose.microservices.yml
└── prometheus.yml
```

## Technology Stack

- **.NET 9.0** - Runtime and framework
- **ASP.NET Core** - Web framework
- **MassTransit** - Message bus abstraction
- **RabbitMQ** - Message broker
- **System.Threading.Channels** - High-performance queues
- **Prometheus** - Metrics collection
- **Serilog** - Structured logging
- **FluentValidation** - Input validation

---

For more information, see the main project [README](../../README.md).
