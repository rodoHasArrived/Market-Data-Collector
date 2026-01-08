# Frequently Asked Questions (FAQ)

This FAQ answers common "What is..." and "Where can I find..." questions about the Market Data Collector project.

## Table of Contents

- [What Is...](#what-is)
  - [General Concepts](#general-concepts)
  - [Data Types](#data-types)
  - [Components](#components)
  - [Configuration](#configuration)
- [Where Can I Find...](#where-can-i-find)
  - [Documentation](#documentation)
  - [Code](#code)
  - [Configuration](#configuration-1)
  - [Data Output](#data-output)

---

## What Is...

### General Concepts

#### What is Market Data Collector?

Market Data Collector is a high-performance, cross-platform system that captures, validates, and persists real-time market data from multiple providers (Interactive Brokers, Alpaca, Polygon) for research, backtesting, and trading applications.

**Where to learn more**: [Main README](../../README.md) and [Architecture](architecture.md)

#### What is a tick-by-tick trade?

A tick-by-tick trade is a record of every individual trade execution that occurs in the market, including the price, size, timestamp, and exchange. This is the highest resolution of trade data available.

**Where to learn more**: [Domains](domains.md)

#### What is Level 2 market data?

Level 2 market data (also called market depth or order book) shows all pending buy and sell orders at different price levels, not just the best bid and ask. This provides visibility into market liquidity and potential price movements.

**Where to learn more**: [Domains](domains.md)

#### What is BBO?

BBO stands for "Best Bid and Offer" (also called Best Bid and Ask). It represents the highest price a buyer is willing to pay (bid) and the lowest price a seller is willing to accept (ask) at any given moment.

**Where to learn more**: [Domains](domains.md)

#### What is an aggressor?

In trading, the aggressor is the party that initiates a trade by accepting the current market price (crossing the spread). A buy aggressor lifts the offer, and a sell aggressor hits the bid.

**Where to learn more**: [Domains](domains.md)

#### What is order flow?

Order flow refers to the analysis of buy and sell orders in the market, including volume imbalances, VWAP, and trade direction. It helps understand market dynamics and participant behavior.

**Where to learn more**: [Domains](domains.md)

#### What is VWAP?

VWAP (Volume-Weighted Average Price) is the average price weighted by volume over a specific time period. It's commonly used as a trading benchmark and to assess execution quality.

**Where to learn more**: [Domains](domains.md)

#### What is an integrity event?

An integrity event is a data quality alert emitted by the collector when it detects issues like sequence gaps, out-of-order messages, or order book inconsistencies. These help identify data reliability problems.

**Where to learn more**: [Domains](domains.md) and [TROUBLESHOOTING](TROUBLESHOOTING.md)

### Data Types

#### What is JSONL?

JSONL (JSON Lines) is a newline-delimited JSON format where each line is a valid JSON object. It's ideal for streaming data and log files because you can append new events without parsing the entire file.

**Where to learn more**: [Architecture](architecture.md)

#### What is the difference between a trade, depth update, and quote?

- **Trade**: An actual transaction that occurred (price, size, time)
- **Depth Update**: A change in the order book at a specific price level
- **Quote**: A snapshot of the best bid and offer (BBO) at a point in time

**Where to learn more**: [Domains](domains.md)

#### What file formats are supported?

The collector outputs JSONL (`.jsonl`) files with optional gzip compression (`.jsonl.gz`). For replay and analysis, any standard JSON parser can read these files.

**Where to learn more**: [Configuration](CONFIGURATION.md) and [Architecture](architecture.md)

### Components

#### What is the EventPipeline?

The EventPipeline is the core message queue that buffers events between collectors and writers. It uses bounded channels to handle backpressure and ensure reliable event delivery.

**Where to learn more**: [Architecture](architecture.md) and [Why This Architecture](why-this-architecture.md)

#### What is a collector?

A collector is a component that subscribes to a data provider's feed (IB, Alpaca, Polygon) and converts raw market data into normalized domain events. Examples: `TradeCollector`, `DepthCollector`, `QuoteCollector`.

**Where to learn more**: [Architecture](architecture.md)

#### What is a writer?

A writer is a component that consumes events from the EventPipeline and persists them to storage (JSONL files). The `JsonlEventWriter` handles file organization, rotation, and compression.

**Where to learn more**: [Architecture](architecture.md)

#### What is the monitoring dashboard?

The monitoring dashboard is a built-in HTTP server that provides real-time metrics, integrity events, and system health information through web endpoints (`/`, `/metrics`, `/status`).

**Where to learn more**: [Getting Started](GETTING_STARTED.md) and [Operator Runbook](operator-runbook.md)

#### What is the replayer?

The replayer (`JsonlReplayer`) is a component that reads historical JSONL files and emits events in chronological order, enabling backtesting and analysis of past market data.

**Where to learn more**: [Main README](../../README.md)

### Configuration

#### What is appsettings.json?

`appsettings.json` is the main configuration file where you specify data providers, symbol subscriptions, storage options, and monitoring settings. It supports hot reload when `--watch-config` is enabled.

**Where to learn more**: [CONFIGURATION](CONFIGURATION.md) and [Getting Started](GETTING_STARTED.md)

#### What is hot reload?

Hot reload allows you to modify `appsettings.json` (add/remove symbols, change settings) and have the changes take effect without restarting the collector. Enable it with the `--watch-config` flag.

**Where to learn more**: [CONFIGURATION](CONFIGURATION.md)

#### What is DataSource?

`DataSource` is the configuration setting that determines which market data provider to use: `IB` (Interactive Brokers), `Alpaca`, or `Polygon`.

**Where to learn more**: [CONFIGURATION](CONFIGURATION.md)

#### What are naming conventions?

Naming conventions control how output files are organized on disk. Options include `BySymbol` (default), `ByDate`, `ByType`, and `Flat`. This affects the directory structure and file naming.

**Where to learn more**: [CONFIGURATION](CONFIGURATION.md)

#### What are date partitions?

Date partitions control how often new files are created based on time: `Daily` (one file per day), `Hourly` (one per hour), `Monthly` (one per month), or `None` (single file).

**Where to learn more**: [CONFIGURATION](CONFIGURATION.md)

#### What are retention policies?

Retention policies automatically delete old data files based on age (`RetentionDays`) or total storage size (`MaxTotalMegabytes`) to manage disk space.

**Where to learn more**: [CONFIGURATION](CONFIGURATION.md)

---

## Where Can I Find...

### Documentation

#### Where can I find the quick start guide?

- **Main README**: [README.md (root)](../../README.md)
- **Getting Started**: [GETTING_STARTED.md](GETTING_STARTED.md)

#### Where can I find configuration documentation?

- **Configuration Guide**: [docs/CONFIGURATION.md](CONFIGURATION.md)
- **Sample Config**: `appsettings.sample.json` (in the repository root)

#### Where can I find architecture documentation?

- **Architecture Overview**: [docs/architecture.md](architecture.md)
- **C4 Diagrams**: [docs/c4-diagrams.md](c4-diagrams.md)
- **Design Decisions**: [docs/why-this-architecture.md](why-this-architecture.md)

#### Where can I find troubleshooting help?

- **Troubleshooting Guide**: [docs/TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Operator Runbook**: [docs/operator-runbook.md](operator-runbook.md)

#### Where can I find information about data providers?

- **Interactive Brokers**: [docs/interactive-brokers-setup.md](interactive-brokers-setup.md)
- **Alpaca**: [docs/GETTING_STARTED.md](GETTING_STARTED.md#alpaca)
- **Polygon**: [docs/GETTING_STARTED.md](GETTING_STARTED.md) (stub implementation)

#### Where can I find event contracts and domain models?

- **Domain Documentation**: [docs/domains.md](domains.md)
- **Data Uniformity**: [docs/data-uniformity.md](data-uniformity.md)

#### Where can I find dependency information?

- **Dependencies**: [DEPENDENCIES.md](../DEPENDENCIES.md)
- **Open Source References**: [docs/open-source-references.md](open-source-references.md)

#### Where can I find production deployment guidance?

- **Operator Runbook**: [docs/operator-runbook.md](operator-runbook.md)
- **Getting Started**: [docs/GETTING_STARTED.md](GETTING_STARTED.md)

#### Where can I find API documentation?

- **API Docs Index**: [api/index.md](api/index.md)
- **DocFx**: [docfx/](docfx/)

### Code

#### Where can I find the main entry point?

- **Program.cs**: `src/MarketDataCollector/Program.cs`

#### Where can I find collector implementations?

- **Trade Collector**: `src/MarketDataCollector/Collectors/TradeCollector.cs`
- **Depth Collector**: `src/MarketDataCollector/Collectors/DepthCollector.cs`
- **Quote Collector**: `src/MarketDataCollector/Collectors/QuoteCollector.cs`
- **Collectors Directory**: `src/MarketDataCollector/Collectors/`

#### Where can I find provider adapters?

- **Interactive Brokers**: `src/MarketDataCollector/Adapters/IB/`
- **Alpaca**: `src/MarketDataCollector/Adapters/Alpaca/`
- **Polygon**: `src/MarketDataCollector/Adapters/Polygon/`
- **Adapters Directory**: `src/MarketDataCollector/Adapters/`

#### Where can I find the event pipeline?

- **EventPipeline**: `src/MarketDataCollector/Pipeline/EventPipeline.cs`
- **Pipeline Directory**: `src/MarketDataCollector/Pipeline/`

#### Where can I find event writers?

- **JSONL Writer**: `src/MarketDataCollector/Writers/JsonlEventWriter.cs`
- **Writers Directory**: `src/MarketDataCollector/Writers/`

#### Where can I find domain models?

- **Events**: `src/MarketDataCollector/Domain/Events/`
- **Models**: `src/MarketDataCollector/Domain/Models/`

#### Where can I find the monitoring server?

- **Status Server**: `src/MarketDataCollector/Monitoring/StatusServer.cs`
- **Monitoring Directory**: `src/MarketDataCollector/Monitoring/`

#### Where can I find the data replayer?

- **Replayer**: `src/MarketDataCollector/Replay/JsonlReplayer.cs`
- **Replay Directory**: `src/MarketDataCollector/Replay/`

### Configuration

#### Where can I find the sample configuration file?

- **Sample Config**: `appsettings.sample.json` (in project root or MarketDataCollector directory)

#### Where should I put my configuration?

- **Default Location**: `appsettings.json` in the same directory as the executable
- **Alternative**: Specify a custom path with the `--config` command-line argument

#### Where can I find environment variable names?

- **Configuration Guide**: [docs/CONFIGURATION.md](CONFIGURATION.md)
- **Getting Started**: [docs/GETTING_STARTED.md](GETTING_STARTED.md#alpaca) (shows Alpaca env vars)

#### Where can I find command-line arguments?

Run the application with `--help`:
```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --help
```

### Data Output

#### Where are market data files stored?

- **Default Location**: `./data/` directory relative to where you run the collector
- **Configurable**: Set `DataRoot` in `appsettings.json` to change location

#### Where are log files stored?

- **Default Location**: `./data/_logs/` directory
- **Configurable**: Set via Serilog configuration in `appsettings.json`

#### Where can I find example output files?

After running the smoke test or self-test, check:
- `./data/{symbol}/{type}/{date}.jsonl`
- Example: `./data/SPY/trade/2024-01-15.jsonl`

The exact path depends on your naming convention setting.

#### Where can I find integrity events?

Integrity events are stored alongside regular events in the same directory structure:
- File: `./data/{symbol}/integrity/{date}.jsonl`
- Also visible in: Monitoring dashboard at `http://localhost:8080`

#### Where can I see real-time metrics?

- **HTML Dashboard**: `http://localhost:8080/` (when running with `--serve-status`)
- **JSON Status**: `http://localhost:8080/status`
- **Prometheus Metrics**: `http://localhost:8080/metrics`

---

## Common Question Patterns

### "How do I...?"

For "how to" questions, see these guides:

- **Getting Started**: [docs/GETTING_STARTED.md](GETTING_STARTED.md)
- **Configuration**: [docs/CONFIGURATION.md](CONFIGURATION.md)
- **Operator Runbook**: [docs/operator-runbook.md](operator-runbook.md)
- **Troubleshooting**: [docs/TROUBLESHOOTING.md](TROUBLESHOOTING.md)

### "Why is...?"

For "why" questions about design decisions:

- **Architecture Rationale**: [docs/why-this-architecture.md](why-this-architecture.md)
- **Design Review Memo**: [docs/design-review-memo.md](design-review-memo.md)

### "What's the difference between...?"

For comparison questions:

- **Domain Models**: [docs/domains.md](domains.md)
- **Architecture**: [docs/architecture.md](architecture.md)

---

## Still Have Questions?

If your question isn't answered here:

1. **Search the documentation** - Use your editor's search or `grep` to find keywords
2. **Check the code** - Most classes have detailed XML comments
3. **Run with help** - Use `--help` flag to see command-line options
4. **Review logs** - Enable debug logging for detailed information
5. **File an issue** - Ask on the project's issue tracker

### Quick Search Tips

```bash
# Search all markdown docs
grep -r "your search term" MarketDataCollector/docs/

# Find files with specific content
find MarketDataCollector/docs -name "*.md" -exec grep -l "keyword" {} \;

# Search code for class/method
grep -r "ClassName" src/
```
