# Market Data Collector

**Version**: 1.6.1 | **Last Updated**: 2026-01-30

**Current Status**: Production Ready. Some providers require credentials or build-time flags; see [production status](status/production-status.md) for readiness notes.

A cross-platform market data collector for real-time and historical market microstructure data. It ingests provider feeds, normalizes them into domain events, and persists them to JSONL/Parquet for downstream research. The current repository snapshot includes a CLI, web dashboard, and a Windows desktop UI (UWP).

## Current capabilities

- **CLI modes**: real-time collection, backfill, replay, packaging, import, self-test, and config validation
- **Auto-configuration**: interactive wizard, environment-based auto-config, provider detection, and credential validation
- **Backfill system**: multi-provider historical backfill with configurable provider priority and job tracking
- **Storage formats**: JSONL (optionally gzip) and Parquet outputs, plus portable data packages
- **Observability**: Prometheus metrics and HTTP status endpoints
- **Optional UWP desktop UI**: Windows-only companion app for configuration/monitoring (feature coverage varies by page)

## Supported Data Providers

- **Interactive Brokers (IB)** – Requires an IBAPI build to enable live connections
- **Alpaca** – Real-time trades and quotes via WebSocket
- **Polygon** – Stub mode unless API credentials are configured; WebSocket parsing is still in progress
- **NYSE** – Direct connection endpoints implemented; requires NYSE credentials
- **StockSharp** – Provider integration enabled (credentials + connector setup required)
- Extensible architecture for adding additional providers

## Quick start

### **🧙 First-Time Setup: Configuration Wizard** (Recommended)

New users should use the interactive configuration wizard:

```bash
./MarketDataCollector --wizard
```

The wizard guides you through:
- Detecting available data providers
- Selecting and configuring your data source
- Setting up symbols to track
- Configuring storage options
- Generating your `appsettings.json`

### **⚡ Quick Auto-Configuration**

If you have environment variables set for your providers:

```bash
# Auto-detect and configure from environment
./MarketDataCollector --auto-config

# Check available providers
./MarketDataCollector --detect-providers

# Validate your credentials
./MarketDataCollector --validate-credentials
```

### **🚀 Web Dashboard**

Start the intuitive web dashboard for easy configuration and monitoring:

```bash
./MarketDataCollector --mode web
```

Then open your browser to `http://localhost:8080` for a full-featured dashboard with:
- 📊 Real-time system status and metrics
- ⚙️ Point-and-click configuration
- 📈 Symbol management
- 📅 Historical backfill interface
- 💡 Built-in help and tooltips
- 🎨 Modern, responsive UI

### **🖥️ Windows Desktop App (UWP)**

Launch the native UWP desktop application on Windows:

```bash
dotnet run --project src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj
```

Highlights:
- 🔐 Secure credential management via Windows CredentialPicker
- 📊 Native Windows UI for configuration, status, and exports
- ⚡ Windows-only companion to the CLI/dashboard (feature coverage varies by page)

### **🧰 Windows Desktop App Install (MSIX/AppInstaller)**

Use the packaged MSIX/AppInstaller flow for a standard Windows install, upgrade, and uninstall experience.

**Prerequisites**
- Windows 10/11 (build 19041 or newer recommended).
- App Installer from the Microsoft Store (required for `.appinstaller` files).
- .NET 9 Desktop Runtime if you are using an unpackaged build.

**Install with AppInstaller**
1. Download the `.appinstaller` file from the release assets.
2. Double-click it to open App Installer.
3. Click **Install**.

**Install with MSIX**
1. Download the `.msix`/`.msixbundle` from the release assets.
2. Double-click the package and follow the prompts.

**CI artifact (preview builds)**
- Desktop App Build workflow uploads an MSIX archive named `MarketDataCollector.Desktop-msix-x64.zip` on tag/manual runs.  
  https://github.com/rodoHasArrived/Market-Data-Collector/actions/workflows/desktop-app.yml

**Upgrade**
- Re-open the latest `.appinstaller` to auto-update.
- For MSIX, install the newer package version over the current one.

**Uninstall**
- **Settings → Apps → Installed apps** → **Market Data Collector Desktop** → **Uninstall**

**Troubleshooting install**
- **Missing signing certificate**: Install the signing cert or use a release-signed package.
- **Blocked package**: Unblock the download in **Properties** or choose **More info → Run anyway** when prompted by SmartScreen.

### Command Line Modes

**Run local smoke test** (no provider connectivity required):
```bash
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj
```

**Desktop mode (collector + UI server sidecar):**
```bash
./MarketDataCollector --mode desktop --http-port 8080
```

**Monitoring mode** with HTTP monitoring and hot-reload:
```bash
./MarketDataCollector --http-port 8080 --watch-config
```

**Self-tests:**
```bash
./MarketDataCollector --selftest
```

**Historical backfill:**
```bash
./MarketDataCollector --backfill \
  --backfill-provider stooq \
  --backfill-symbols SPY,QQQ \
  --backfill-from 2024-01-01 \
  --backfill-to 2024-01-05
```

**Get help:**
```bash
./MarketDataCollector --help
```

**Auto-Configuration Commands:**
```bash
# Interactive configuration wizard (recommended for new users)
./MarketDataCollector --wizard

# Quick auto-configuration from environment variables
./MarketDataCollector --auto-config

# Detect available providers and their status
./MarketDataCollector --detect-providers

# Validate configured API credentials
./MarketDataCollector --validate-credentials

# Generate a configuration template
./MarketDataCollector --generate-config
```

See `docs/guides/operator-runbook.md` for deployment and operations guidance.

## Configuration highlights

* `appsettings.json` drives symbol subscriptions (trades/depth), provider settings, and API credentials.
* Hot reload is enabled by default: edits to `appsettings.json` apply without restarting when `--watch-config` is set.
* Set `DataSource` to `IB`, `Alpaca`, or `Polygon` to select the active data provider; BBO snapshots keep recording with stream IDs preserved for reconciliation.
* Configure storage options including naming conventions, date partitioning, retention policies, capacity limits, and optional storage profiles.

## Outputs

Events are written under `./data/` as newline-delimited JSON. The storage system supports multiple file organization strategies:

### File Naming Conventions
- **BySymbol** (default): `{root}/{symbol}/{type}/{date}.jsonl` - Best for analyzing individual symbols over time
- **ByDate**: `{root}/{date}/{symbol}/{type}.jsonl` - Best for daily batch processing
- **ByType**: `{root}/{type}/{symbol}/{date}.jsonl` - Best for analyzing specific event types across symbols
- **Flat**: `{root}/{symbol}_{type}_{date}.jsonl` - Simple flat structure for small datasets

### Date Partitioning
- **Daily** (default): One file per day
- **Hourly**: One file per hour (high-volume scenarios)
- **Monthly**: One file per month (long-term storage)
- **None**: Single file per symbol/type

### Retention Management
- **RetentionDays**: Automatically delete files older than specified days
- **MaxTotalMegabytes**: Cap total storage size, removing oldest files first

Example configuration in `appsettings.json`:
```json
{
  "Storage": {
    "NamingConvention": "BySymbol",
    "DatePartition": "Daily",
    "IncludeProvider": false,
    "RetentionDays": 30,
    "MaxTotalMegabytes": 10240
  }
}
```

Integrity events are stored alongside trade/depth/quote streams so data quality issues are easy to correlate.

## Monitoring and Observability

The collector includes a built-in HTTP server for real-time monitoring without requiring ASP.NET:

```bash
dotnet run -- --http-port 8080
```

### Available Endpoints

- **`/`** - Live HTML dashboard with auto-refreshing metrics and integrity events
- **`/metrics`** - Prometheus-compatible metrics endpoint
- **`/status`** - JSON status with metrics, pipeline statistics, and integrity events
- **`/api/backfill/*`** - REST endpoints surfaced by the dashboard to list providers, run backfill, and read the latest backfill status

### Metrics Exposed

- `mdc_published` - Total events published
- `mdc_dropped` - Events dropped due to backpressure
- `mdc_integrity` - Integrity validation events
- `mdc_trades` - Trade events processed
- `mdc_depth_updates` - Market depth updates
- `mdc_quotes` - Quote updates
- `mdc_events_per_second` - Current event throughput rate
- `mdc_drop_rate` - Drop rate percentage

Integrate with Prometheus, Grafana, or simply monitor the dashboard in your browser.

## Historical Backfill

The collector can prime disk with historical daily bars before live capture begins. The feature ships with a Stooq provider and can be extended with additional implementations under `Infrastructure/Providers/Backfill`.

### Configuration

`appsettings.json` accepts a `Backfill` section to seed defaults:

```json
"Backfill": {
  "Enabled": false,
  "Provider": "stooq",
  "Symbols": ["SPY", "QQQ"],
  "From": "2024-01-01",
  "To": "2024-01-05"
}
```

Command-line arguments override the config file when present:

- `--backfill` enables a run at startup (even if config is disabled)
- `--backfill-provider <name>` selects a specific provider
- `--backfill-symbols <CSV>` overrides the symbol list
- `--backfill-from <yyyy-MM-dd>` / `--backfill-to <yyyy-MM-dd>` bound the date range

Results are written alongside the live data under `DataRoot` and surfaced through `/api/backfill/status` and the dashboard.

## Data Replay

Replay previously captured JSONL files for backtesting and analysis:

```csharp
var replayer = new JsonlReplayer("./data");
await foreach (var evt in replayer.ReadEventsAsync(cancellationToken))
{
    // Process historical events
    Console.WriteLine($"{evt.EventType}: {evt.Timestamp}");
}
```

The replayer supports:
- Automatic gzip decompression (`.jsonl.gz` files)
- Chronological playback across multiple files
- All event types (trades, depth, quotes, integrity events)

## Lean Engine Integration

Market Data Collector integrates with **QuantConnect's Lean Engine** to enable algorithmic trading and backtesting:

### Key Features
- **Custom BaseData Types**: `MarketDataCollectorTradeData` and `MarketDataCollectorQuoteData` for Lean algorithms
- **Data Provider**: `MarketDataCollectorDataProvider` implements Lean's `IDataProvider` interface
- **Tick-Level Backtesting**: Use collected market microstructure data for strategy development
- **Sample Algorithms**: Pre-built examples for spread analysis, order flow, and microstructure strategies

### Quick Start with Lean

```csharp
using QuantConnect.Algorithm;
using MarketDataCollector.Integrations.Lean;

public class MyAlgorithm : QCAlgorithm
{
    public override void Initialize()
    {
        AddData<MarketDataCollectorTradeData>("SPY", Resolution.Tick);
        AddData<MarketDataCollectorQuoteData>("SPY", Resolution.Tick);
    }

    public override void OnData(Slice data)
    {
        // Access high-fidelity market data
    }
}
```

See [`src/MarketDataCollector/Integrations/Lean/README.md`](src/MarketDataCollector/Integrations/Lean/README.md) for comprehensive integration guide.

## Architecture and design docs

Detailed diagrams and domain notes live in `./docs`:

* `architecture/overview.md` – layered architecture and event flow
* `architecture/c4-diagrams.md` – system and component diagrams
* `architecture/domains.md` – event contracts and invariants
* `architecture/provider-management.md` – provider abstraction details
* `architecture/storage-design.md` – storage organization and policies
* `architecture/why-this-architecture.md` – design rationale
* `guides/operator-runbook.md` – operational guidance and startup scripts
* `providers/interactive-brokers-setup.md` – IB API installation and configuration
* `integrations/lean-integration.md` – QuantConnect Lean Engine integration guide
* `integrations/fsharp-integration.md` – F# domain library integration guide
* `reference/open-source-references.md` – related projects and resources

## F# Domain Library

The `MarketDataCollector.FSharp` project provides type-safe domain models, validation, and calculations using F#:

### Quick Start with F#

```fsharp
open MarketDataCollector.FSharp.Domain.MarketEvents
open MarketDataCollector.FSharp.Validation.TradeValidator
open MarketDataCollector.FSharp.Calculations.Spread

// Create and validate a trade
let trade = MarketEvent.createTrade "AAPL" 150.00m 100L AggressorSide.Buyer 1L DateTimeOffset.UtcNow

// Validate with Railway-Oriented Programming
match validateTradeDefault trade with
| Ok validTrade -> printfn "Valid trade: %A" validTrade
| Error errors -> errors |> List.iter (fun e -> printfn "Error: %s" e.Description)

// Calculate spread from a quote
let spreadBps = spreadBpsFromQuote quote  // Returns Some 9.99m or None
```

### Using F# from C#

```csharp
using MarketDataCollector.FSharp.Interop;

// Use C#-friendly wrapper classes
var validator = new TradeValidator();
var result = validator.Validate(trade);

if (result.IsSuccess)
    Console.WriteLine($"Valid: {result.Value.Symbol}");
else
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error}");

// Use calculation helpers
var spread = SpreadCalculator.Calculate(bidPrice, askPrice);
var imbalance = ImbalanceCalculator.FromQuote(quote);
var vwap = AggregationFunctions.Vwap(trades);
```

See [`docs/integrations/fsharp-integration.md`](docs/integrations/fsharp-integration.md) for comprehensive documentation.

## Release notes

The repository currently tracks changes directly in git without curated release notes. Refer to
`docs/status/CHANGELOG.md` for the current snapshot summary and `docs/status/ROADMAP.md` for planned work.

## Troubleshooting

### Build and Restore Issues

If you encounter issues with `dotnet restore` or `dotnet build`, use the diagnostic scripts to gather detailed information:

**Linux/macOS:**
```bash
./scripts/diagnose-build.sh         # Run full diagnostics
./scripts/diagnose-build.sh restore # Diagnose restore only
./scripts/diagnose-build.sh build   # Diagnose build only
./scripts/diagnose-build.sh clean   # Clean and diagnose
```

**Windows:**
```powershell
.\scripts\diagnose-build.ps1              # Run full diagnostics
.\scripts\diagnose-build.ps1 -Action restore  # Diagnose restore only
.\scripts\diagnose-build.ps1 -Action build    # Diagnose build only
.\scripts\diagnose-build.ps1 -Action clean    # Clean and diagnose
```

**Manual Diagnostic Commands:**
```bash
# Restore with diagnostic logging
dotnet restore MarketDataCollector /p:EnableWindowsTargeting=true -v diag

# Build with diagnostic logging
dotnet build MarketDataCollector -c Release -v diag

# Save output to file for analysis
dotnet restore MarketDataCollector /p:EnableWindowsTargeting=true -v diag > restore-diag.log 2>&1
```

The diagnostic scripts will:
- Check .NET SDK version and installed SDKs
- Verify NuGet sources configuration
- Run restore/build with diagnostic logging
- Save detailed logs to `diagnostic-logs/` directory
- Report warnings and errors found
- Provide commands for log analysis

### Common Issues

For detailed troubleshooting of common issues, see [HELP.md](HELP.md#troubleshooting), including:
- Configuration file issues
- Provider connection problems
- Authentication failures
- File permission errors
- High CPU usage
- Data file creation issues
- Build and restore failures

## Known Limitations and Roadmap

For current gaps and planned work, refer to `docs/status/production-status.md` and `docs/status/ROADMAP.md`.

**Data Precision:**
- Order book uses `double` for prices (consider `decimal` to avoid floating-point precision issues)

### Planned Enhancements

See the project [DEPENDENCIES.md](DEPENDENCIES.md) for detailed recommendations on:
- **Serilog** integration for structured logging
- **Polly** for retry policies and circuit breakers
- **FluentValidation** for configuration validation
- **prometheus-net** for enhanced metrics
- **xUnit/Moq** for comprehensive testing
- **Parquet.Net** for columnar storage

The main project README includes a detailed roadmap for near-term and long-term improvements.
