## MarketDepthCollector (L2 Microstructure)

This project now includes a `MarketDepthCollector` in `src/IBDataCollector/Domain/Collectors` that:
- consumes `MarketDepthUpdate` deltas (insert/update/delete) for Bid/Ask at book levels
- maintains a per-symbol L2 order book
- emits `MarketEventType.L2Snapshot` with `LOBSnapshot` payloads
- emits `MarketEventType.Integrity` with `DepthIntegrityEvent` payloads on gaps/out-of-order/invalid ops
- stops processing for a symbol once stale until `ResetSymbolStream(symbol)` is called

### Running a local smoke test (no IB required)

```bash
dotnet run --project src/IBDataCollector/IBDataCollector.csproj
```

This will simulate a few depth updates and write JSONL output under `./data/`.

### Running built-in self tests (no external test framework)

```bash
dotnet run --project src/IBDataCollector/IBDataCollector.csproj -- --selftest
```

### Notes on IB market depth

To receive L2, IB requires the correct market data subscriptions for the venues involved.
When you connect the official IB API:
- call `reqMktDepth` / `cancelMktDepth` per symbol with your desired depth levels
- map `tickerId -> symbol` (see `Infrastructure/IB/IBCallbackRouter.cs`)
- route `updateMktDepth` / `updateMktDepthL2` callbacks into `MarketDepthCollector.OnDepth(...)`

### Building with the official IB API

To compile the real IB connection manager:
- add a reference to IBApi (dll/package)
- define the compilation constant `IBAPI`

Example:
```bash
dotnet build -p:DefineConstants=IBAPI
```


## ContractFactory + preferred shares

`Infrastructure/IB/ContractFactory.cs` builds IB contracts from `SymbolConfig`.

For preferred shares, set `LocalSymbol` whenever possible (this avoids IB ambiguity).

Example config entry:
```json
{
  "Symbol": "PCG-PA",
  "SubscribeTrades": true,
  "SubscribeDepth": true,
  "DepthLevels": 10,
  "SecurityType": "STK",
  "Exchange": "SMART",
  "Currency": "USD",
  "PrimaryExchange": "NYSE",
  "LocalSymbol": "PCG PRA"
}
```

### Building with IBApi
Reference IBApi and define `IBAPI`:
```bash
dotnet build -p:DefineConstants=IBAPI
```


## IIBMarketDataClient abstraction

Program now uses `IIBMarketDataClient` so the same config-driven subscription logic runs in all builds:
- In normal builds (no IBApi), `NoOpIBClient` is used (no connection).
- In IB builds (`IBAPI` constant + IBApi reference), `IBMarketDataClient` wraps `EnhancedIBConnectionManager` + `IBCallbackRouter`.

This removes the need to manually uncomment any code in Program.


## Hot-reloaded trade subscriptions

`SubscriptionManager` now applies `SubscribeTrades` via `IIBMarketDataClient.SubscribeTrades(SymbolConfig)`.
In IB builds (`IBAPI` defined), this uses `reqTickByTickData(..., "AllLast")` and routes `tickByTickAllLast` into `TradeDataCollector`.
In non-IB builds, it is a no-op.


## Documentation

See `/docs`:

* `architecture.md` – system design and data flow
* `domains.md` – domain model and invariants
* `operator-runbook.md` – production operations


### Documentation Site (DocFX)

Build:
```bash
docfx docs/docfx/docfx.json
```

Key docs:
- `docs/c4-diagrams.md`
- `docs/design-review-memo.md`
- `docs/why-this-architecture.md`


## Startup

Use the canonical startup scripts in repo root:
- Linux/macOS: `START_COLLECTOR.exp`
- Windows: `START_COLLECTOR.ps1`
- systemd unit: `deploy/systemd/ibdatacollector.service`

See `docs/operator-runbook.md` for details.
