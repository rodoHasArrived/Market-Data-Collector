# Provider Implementation Template

This directory contains skeleton files for implementing a new market data provider.
Copy the relevant files to a new directory under `Adapters/{ProviderName}/` and replace
every `Template` / `TEMPLATE` placeholder with your provider's actual name and values.

## Files

| File | Purpose |
|------|---------|
| `TemplateConstants.cs` | Provider-specific constants (endpoints, rate limits, message types) |
| `TemplateMarketDataClient.cs` | Real-time streaming client (`IMarketDataClient`) |
| `TemplateHistoricalDataProvider.cs` | Historical data backfill provider (`BaseHistoricalDataProvider`) |
| `TemplateSymbolSearchProvider.cs` | Symbol search/lookup provider (`BaseSymbolSearchProvider`) |

Implement only the file(s) relevant to your provider — not every provider needs all three.

## Quick-start steps

1. **Create your provider directory**
   ```
   src/MarketDataCollector.Infrastructure/Adapters/{YourProvider}/
   ```

2. **Copy and rename the template files**
   ```
   cp _Template/TemplateConstants.cs                 {YourProvider}/{YourProvider}Constants.cs
   cp _Template/TemplateMarketDataClient.cs          {YourProvider}/{YourProvider}MarketDataClient.cs
   cp _Template/TemplateHistoricalDataProvider.cs    {YourProvider}/{YourProvider}HistoricalDataProvider.cs
   cp _Template/TemplateSymbolSearchProvider.cs      {YourProvider}/{YourProvider}SymbolSearchProvider.cs
   ```

3. **Replace all `Template` placeholders** with your provider name (case-sensitive).

4. **Fill in the `TODO` comments** in each file — they mark every section that needs
   provider-specific implementation.

5. **Register the provider** in `Program.cs` or the appropriate composition root.

6. **Write tests** — place them in `tests/MarketDataCollector.Tests/Infrastructure/Providers/`.
   Use `MarketDataClientContractTests<T>` as the base class for streaming-client tests.

## Architecture rules

- `*Constants.cs` — keep all types `internal`; no public API surface.
- `*MarketDataClient.cs` — implement `IMarketDataClient`; prefer `WebSocketProviderBase`
  for WebSocket-based providers.
- `*HistoricalDataProvider.cs` — extend `BaseHistoricalDataProvider`; use
  `ProviderRateLimitTracker` for rate limiting (not `Task.Delay`).
- `*SymbolSearchProvider.cs` — extend `BaseSymbolSearchProvider`.
- Apply `[DataSource]` and `[ImplementsAdr]` attributes as shown in the templates.
- Use structured logging: `_log.LogInformation("Fetched {Count} bars for {Symbol}", n, sym)`.
- Every async method must accept and forward `CancellationToken ct`.

## Reference providers

Consult these production implementations for additional context:

| Type | Example |
|------|---------|
| Streaming (WebSocket) | `Adapters/Alpaca/AlpacaMarketDataClient.cs` |
| Streaming (base class) | `Adapters/Core/WebSocketProviderBase.cs` |
| Historical backfill | `Adapters/Finnhub/FinnhubHistoricalDataProvider.cs` |
| Symbol search | `Adapters/Alpaca/AlpacaSymbolSearchProviderRefactored.cs` |
| Constants pattern | `Adapters/InteractiveBrokers/IBApiLimits.cs` |

See also `docs/development/provider-implementation.md` for the full implementation guide.
