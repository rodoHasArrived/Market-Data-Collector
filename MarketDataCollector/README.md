# Market Data Collector

A cross-platform, provider-agnostic market data collector that ingests real-time market data from multiple sources (Interactive Brokers, Alpaca, and more), normalizes them into domain events, and persists them as JSONL for downstream research. The collector emits best bid/offer (BBO) snapshots to support trade aggressor inference and quote-aware analytics regardless of data provider.

## Supported Data Providers

- **Interactive Brokers (IB)** – L2 depth, tick-by-tick trades, quotes
- **Alpaca** – Real-time trades and quotes via WebSocket
- Extensible architecture for adding additional providers

## Quick start

Run a local smoke test (no provider connectivity required):

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

To exercise built-in self tests:

```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest
```

See `docs/operator-runbook.md` for production startup scripts, including the systemd unit and PowerShell helpers.

## Configuration highlights

* `appsettings.json` drives symbol subscriptions (trades/depth), provider settings, and API credentials.
* Hot reload is enabled by default: edits to `appsettings.json` apply without restarting when `--watch-config` is set.
* Set `DataSource` to `IB` or `Alpaca` to select the active data provider; BBO snapshots keep recording with stream IDs preserved for reconciliation.

## Outputs

Events are written under `./data/` as newline-delimited JSON. The default `JsonlStoragePolicy` rotates files by symbol and event type to make downstream consumption simpler (e.g., `AAPL.Trade.jsonl`, `AAPL.BboQuote.jsonl`). Integrity events are stored alongside trade/depth/quote streams so data quality issues are easy to correlate.

## Architecture and design docs

Detailed diagrams and domain notes live in `./docs`:

* `architecture.md` – layered architecture and event flow
* `c4-diagrams.md` – rendered system, container, and component diagrams
* `domains.md` – event contracts and invariants
* `operator-runbook.md` – operational guidance and startup scripts
