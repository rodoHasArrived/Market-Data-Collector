# IB Data Collector (IB + Alpaca BBO)

A cross-platform collector that ingests Interactive Brokers (IB) market data (trades + L2 depth) and optionally Alpaca trades/quotes, normalizes them into domain events, and persists them as JSONL for downstream research. The collector also emits best bid/offer (BBO) snapshots to support trade aggressor inference and quote-aware analytics even when IB connectivity is disabled.

## Quick start

Run a local smoke test (no IB/Alpaca connectivity required):

```bash
dotnet run --project src/IBDataCollector/IBDataCollector.csproj
```

To exercise built-in self tests:

```bash
dotnet run --project src/IBDataCollector/IBDataCollector.csproj -- --selftest
```

See `docs/operator-runbook.md` for production startup scripts, including the systemd unit and PowerShell helpers.

## Configuration highlights

* `appsettings.json` drives symbol subscriptions (trades/depth), IB client settings, and Alpaca keys.
* Hot reload is enabled by default: edits to `appsettings.json` apply without restarting when `--watch-config` is set.
* Set `DataSource` to `Alpaca` to disable IB connectivity checks and use Alpaca WebSocket data instead; BBO snapshots keep recording with stream IDs preserved for reconciliation.

## Outputs

Events are written under `./data/` as newline-delimited JSON. The default `JsonlStoragePolicy` rotates files by symbol and event type to make downstream consumption simpler (e.g., `AAPL.Trade.jsonl`, `AAPL.BboQuote.jsonl`). Integrity events are stored alongside trade/depth/quote streams so data quality issues are easy to correlate.

## Architecture and design docs

Detailed diagrams and domain notes live in `./docs`:

* `architecture.md` – layered architecture and event flow
* `c4-diagrams.md` – rendered system, container, and component diagrams
* `domains.md` – event contracts and invariants
* `operator-runbook.md` – operational guidance and startup scripts
