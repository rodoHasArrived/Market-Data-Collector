# Getting Started

This folder contains essential guides for new users of the Market Data Collector.

## Guides

1. **[setup.md](setup.md)** - Initial setup and first run instructions
2. **[configuration.md](configuration.md)** - Configuration options and settings
3. **[troubleshooting.md](troubleshooting.md)** - Common issues and solutions

## Quick Start

For the fastest setup experience, run:

```bash
# Interactive configuration wizard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --wizard

# Or auto-configure from environment variables
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --auto-config
```

See [setup.md](setup.md) for detailed instructions.
