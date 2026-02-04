# Getting Started

Quick start guide for the Market Data Collector. For comprehensive documentation, see [HELP.md](../HELP.md).

## Fastest Setup

```bash
# Clone and build
git clone <repository-url>
cd Market-Data-Collector
dotnet build

# Run the interactive wizard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --wizard
```

The wizard guides you through provider selection, symbol configuration, and storage setup.

## Alternative Setup Methods

| Method | Command | Best For |
|--------|---------|----------|
| **Configuration Wizard** | `--wizard` | New users, interactive setup |
| **Auto-Configuration** | `--auto-config` | Users with env vars already set |
| **Web Dashboard** | `--mode web` | Visual configuration |
| **Manual Config** | Edit `config/appsettings.json` | Power users |

## Quick Reference

- **[User Guide](../HELP.md)** - Complete reference for all features
- **[Configuration](../HELP.md#configuration)** - All configuration options
- **[Data Providers](../HELP.md#data-providers)** - Provider setup guides
- **[Troubleshooting](../HELP.md#troubleshooting)** - Common issues and solutions
- **[FAQ](../HELP.md#faq)** - Frequently asked questions

## Prerequisites

- .NET 9.0 SDK
- At least one data provider account:
  - Alpaca (free tier available)
  - Interactive Brokers (requires TWS/Gateway)
  - Polygon, NYSE, or StockSharp (various tiers)

## Next Steps

After initial setup:
1. **Start collecting**: `dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode web`
2. **Run backfill**: See [Backfill Guide](../providers/backfill-guide.md)
3. **Monitor quality**: Check the Data Quality page in the web dashboard

---

*See [HELP.md](../HELP.md) for the complete user guide.*
