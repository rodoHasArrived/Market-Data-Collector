# API Reference

This section provides generated API documentation from the source code XML documentation.

## Building the API Docs

Build the API documentation with DocFX:

```bash
docfx docs/docfx/docfx.json
```

## API Components

### Domain Layer
- `TradeDataCollector` - Tick-by-tick trade processing with sequence validation
- `MarketDepthCollector` - L2 order book maintenance with integrity checking
- `QuoteCollector` - BBO state cache and quote event emission
- `SymbolSubscriptionTracker` - Thread-safe subscription management base class

### Application Layer
- `EventPipeline` - Bounded channel for event routing
- `JsonlStorageSink` - Append-only JSONL file storage
- `StatusHttpServer` - HTTP monitoring server with Prometheus metrics

### Infrastructure Layer
- `IBMarketDataClient` - Interactive Brokers provider
- `AlpacaMarketDataClient` - Alpaca WebSocket provider
- `PolygonMarketDataClient` - Polygon provider (stub)

---

**Version:** 1.1.0
**Last Updated:** 2026-01-02
**See Also:** [architecture.md](../architecture.md) | [domains.md](../domains.md)
