# Why This Architecture (Non-Engineer Explainer)

## What this program does
It records detailed market activity from Interactive Brokers and/or Alpaca WebSocket feeds:
- **Trades:** every print (tick-by-tick) with sequence validation
- **Depth:** the live order book (Level 2) with integrity checking
- **Quotes:** best bid/offer (BBO) snapshots with spread and mid-price
- **Order Flow:** rolling statistics like VWAP, buy/sell volume, and imbalance

It stores that data in a format that can be audited, replayed, and analyzed.

---

## Why we split it into layers

### 1) Infrastructure (the "adapter")
Interactive Brokers has its own API and quirks. Alpaca data comes from a WebSocket protocol. We isolate each provider so:
- we can swap providers later if needed (just implement `IIBMarketDataClient`)
- the rest of the system stays stable
- data logic doesn't become entangled with vendor specifics

Key classes: `IBMarketDataClient`, `AlpacaMarketDataClient`, `IBCallbackRouter`, `ContractFactory`

### 2) Domain (the "brains")
This is where we decide what the data *means*:
- `MarketDepthCollector` – keeps an order book and validates every insert/update/delete
- `TradeDataCollector` – processes trades, validates sequence numbers, computes order-flow statistics
- `QuoteCollector` – tracks BBO per symbol and provides quote context for aggressor inference
- detect when data is inconsistent or missing (emit integrity events)

This layer is written so it can be tested without connecting to IB.

### 3) Application (the "conductor")
This is the part that runs the show:
- `Program.cs` – wires everything together
- `ConfigWatcher` – hot reloads `appsettings.json` without restarts
- `StatusWriter` – writes health snapshots for monitoring/UI
- `Metrics` – tracks published, dropped, and integrity event counts

### 4) Pipeline/Storage (the "transport and memory")
We treat all recorded activity as a stream of standardized `MarketEvent` objects and store them safely:
- `EventPipeline` – bounded channel (default 50,000 events) prevents runaway memory use
- `JsonlStorageSink` – writes events as append-only JSON lines
- `JsonlStoragePolicy` – partitions files by `<Symbol>.<EventType>.jsonl` for easy audit and replay

---

## Why this is safer and more "institutional"
- **Audit-friendly output:** append-only logs per symbol/type (e.g., `AAPL.Trade.jsonl`, `SPY.BboQuote.jsonl`)
- **Integrity events:** the system detects sequence gaps, out-of-order trades, and book corruption rather than silently writing bad data
- **Quote-aware analytics:** BBO tracking enables aggressor inference (buy vs. sell) and better validation of trade prints
- **Hot reload with atomic writes:** reduces operational risk of restarts and partial configs
- **Provider independence:** the core logic remains correct even if IB or Alpaca integration changes
- **Backpressure protection:** bounded queues drop oldest events under load rather than exhausting memory

---

## Current capabilities

### Implemented
- Trade sequence validation with `IntegrityEvent` emission
- Order book integrity checking with `DepthIntegrityEvent`
- BBO tracking via `QuoteCollector` with `IQuoteStateStore` interface
- Aggressor inference when BBO context is available
- Order-flow statistics (VWAP, buy/sell volume, imbalance)
- Multiple data sources (IB, Alpaca) via pluggable clients
- Hot reload of symbol configuration
- Status monitoring via `StatusWriter`

### Coming next for production maturity
- Wire Alpaca quote messages fully to `QuoteCollector`
- Structured logging with rotation
- Automated recovery policies (auto-resubscribe on integrity)
- CI test automation and release workflow
- Authentication for UI if network-exposed
- Feed-divergence alarms for IB vs. Alpaca reconciliation
