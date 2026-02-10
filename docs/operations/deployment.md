# Deployment Guide

This guide covers the three primary deployment methods for Market Data Collector: Docker, systemd, and standalone.

---

## Docker Deployment

### Prerequisites

- Docker 24+ and Docker Compose v2
- At least 1 GB RAM available for the container
- Persistent storage volume for market data

### Quick Start

```bash
# 1. Clone repository
git clone https://github.com/rodoHasArrived/Market-Data-Collector.git
cd Market-Data-Collector

# 2. Create configuration
cp config/appsettings.sample.json config/appsettings.json
# Edit config/appsettings.json with your settings

# 3. Start the application
docker compose -f deploy/docker/docker-compose.yml up -d

# 4. Verify
curl http://localhost:8080/healthz
```

### Building the Image

The Dockerfile uses a multi-stage build. Build context must be the repository root:

```bash
docker build -t marketdatacollector:latest -f deploy/docker/Dockerfile .
```

### Running Modes

**Web dashboard (default):**
```bash
docker run -d -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/config/appsettings.json:/app/appsettings.json:ro \
  --name mdc marketdatacollector:latest
```

**Headless mode:**
```bash
docker run -d \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/config/appsettings.json:/app/appsettings.json:ro \
  --name mdc marketdatacollector:latest \
  --mode headless --watch-config
```

**One-off backfill:**
```bash
docker run --rm \
  -v $(pwd)/data:/app/data \
  marketdatacollector:latest \
  --backfill --backfill-symbols AAPL,MSFT --backfill-from 2024-01-01
```

### Environment Variables

Pass API credentials via environment variables (never bake into images):

```bash
docker run -d -p 8080:8080 \
  -e ALPACA__KEYID=your-key-id \
  -e ALPACA__SECRETKEY=your-secret-key \
  -e MDC_API_KEY=your-api-key \
  -v $(pwd)/data:/app/data \
  marketdatacollector:latest
```

### Docker Compose with Monitoring

Start with Prometheus and Grafana:

```bash
docker compose -f deploy/docker/docker-compose.yml --profile monitoring up -d
```

This starts:
- **MDC** at `http://localhost:8080` (dashboard + API)
- **Prometheus** at `http://localhost:9090` (metrics)
- **Grafana** at `http://localhost:3000` (dashboards, default: admin/admin)

### Volumes

| Mount | Purpose | Required |
|-------|---------|----------|
| `/app/data` | Market data storage | Yes |
| `/app/appsettings.json` | Configuration (read-only) | Recommended |
| `/app/logs` | Application logs | Optional |

### Health Checks

The container includes a built-in health check that polls `/health` every 30 seconds. Docker will mark the container as unhealthy after 3 consecutive failures.

```bash
docker inspect --format='{{.State.Health.Status}}' mdc
```

---

## systemd Deployment

### Prerequisites

- .NET 9.0 Runtime or SDK installed
- Linux with systemd
- Dedicated service account

### Setup

```bash
# 1. Create service account
sudo useradd -r -s /bin/false -d /opt/marketdatacollector mdc

# 2. Deploy application
sudo mkdir -p /opt/marketdatacollector
sudo chown mdc:mdc /opt/marketdatacollector
# Clone or copy the application to /opt/marketdatacollector

# 3. Create data directories
sudo -u mdc mkdir -p /opt/marketdatacollector/{data,logs,run}

# 4. Create environment file for credentials
sudo cp /dev/null /opt/marketdatacollector/.env
sudo chmod 600 /opt/marketdatacollector/.env
sudo chown mdc:mdc /opt/marketdatacollector/.env
# Edit .env with credentials:
#   ALPACA__KEYID=your-key-id
#   ALPACA__SECRETKEY=your-secret-key

# 5. Install service
sudo cp deploy/systemd/marketdatacollector.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable marketdatacollector

# 6. Start
sudo systemctl start marketdatacollector
```

### Service Management

```bash
# Status
sudo systemctl status marketdatacollector

# Logs (follow)
sudo journalctl -u marketdatacollector -f

# Logs (last hour)
sudo journalctl -u marketdatacollector --since "1 hour ago"

# Restart
sudo systemctl restart marketdatacollector

# Stop
sudo systemctl stop marketdatacollector
```

### Security Hardening

The systemd unit includes these security directives:

| Directive | Purpose |
|-----------|---------|
| `NoNewPrivileges=true` | Prevents privilege escalation |
| `ProtectSystem=strict` | Read-only filesystem except allowed paths |
| `ProtectHome=true` | No access to /home |
| `PrivateTmp=true` | Isolated /tmp |
| `ProtectKernelTunables=true` | No sysctl writes |
| `ReadWritePaths=` | Explicit write-allowed paths |
| `MemoryMax=2G` | Memory ceiling |
| `LimitNOFILE=65536` | File descriptor limit for many connections |

### Interactive Brokers Integration

When using IB as the data provider, the service preflight checks verify IB Gateway connectivity:

```bash
# Set in /opt/marketdatacollector/.env:
USE_IBAPI=true
IB_HOST=127.0.0.1
IB_PORT=7497
IB_CLIENT_ID=17
```

The service will fail to start if IB Gateway is not reachable on any of the standard ports (7497, 4002, 7496, 4001).

---

## Standalone Deployment

### Running Directly

```bash
# Build
dotnet build -c Release

# Run with web dashboard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- \
  --ui --http-port 8080 --watch-config

# Run headless
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- \
  --mode headless --watch-config
```

### First-Time Setup

```bash
# Interactive wizard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --wizard

# Auto-configure from environment variables
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --auto-config

# Validate configuration
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --dry-run
```

---

## Monitoring Setup

### Prometheus

The application exposes Prometheus metrics at `/metrics`. Key metric families:

| Metric | Type | Description |
|--------|------|-------------|
| `mdc_pipeline_events_published_total` | Counter | Events published to pipeline |
| `mdc_pipeline_events_dropped_total` | Counter | Events dropped from pipeline |
| `mdc_pipeline_queue_utilization` | Gauge | Pipeline queue utilization (0-1) |
| `mdc_provider_connected` | Gauge | Provider connection status (0/1) |
| `mdc_provider_latency_seconds` | Histogram | Provider response latency |
| `mdc_storage_write_errors_total` | Counter | Storage write errors |
| `mdc_data_quality_score` | Gauge | Per-symbol data quality score |

### Alert Rules

Pre-built alert rules are in `deploy/monitoring/alert-rules.yml`. Key alerts:

| Alert | Severity | Condition |
|-------|----------|-----------|
| MdcDown | Critical | Instance unreachable for 1m |
| MdcHighDropRate | Warning | >10 drops/sec for 5m |
| MdcPipelineBackpressure | Warning | Queue >90% for 2m |
| MdcProviderDisconnected | Warning | Provider down for 2m |
| MdcStorageWriteErrors | Critical | Any write errors for 5m |

### Grafana

When using Docker Compose with the monitoring profile, Grafana is auto-provisioned with:
- Prometheus data source pointing to `http://prometheus:9090`
- Dashboard provisioning from `deploy/monitoring/grafana/provisioning/dashboards/`

---

## API Authentication

When deploying with external access, set the `MDC_API_KEY` environment variable to protect API endpoints:

```bash
# Docker
docker run -e MDC_API_KEY=your-secret-key ...

# systemd (.env file)
MDC_API_KEY=your-secret-key

# Standalone
export MDC_API_KEY=your-secret-key
```

API requests must include the key via `X-Api-Key` header or `api_key` query parameter. Health probes (`/healthz`, `/readyz`, `/livez`) are always exempt.

Rate limiting is enforced at 120 requests/minute per API key or IP address.

---

**See also:** [Operator Runbook](operator-runbook.md) | [API Reference](../reference/api-reference.md) | [Architecture Overview](../architecture/overview.md)
