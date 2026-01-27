# Market Data Collector

**Version**: 1.6.1 (Production Ready) | **Last Updated**: 2026-01-27

A cross-platform, production-ready market data collector with an intuitive web dashboard. Ingests real-time market data from multiple sources (Interactive Brokers, Alpaca, Polygon), normalizes them into domain events, and persists them as JSONL for downstream research. Features comprehensive error handling, single-executable deployment, and built-in help system.

## ✨ New in v1.6.1

- **🔧 Provider Refactoring** - Historical providers now extend `BaseHistoricalDataProvider` for shared HTTP handling
- **🛠️ HttpResponseHandler** - Centralized HTTP error handling utility across all providers
- **📊 447 Source Files** - Codebase expanded to 413 C# files and 12 F# files
- **📖 Documentation Refresh** - All docs updated to reflect current project state

## ✨ New in v1.6

- **🧹 Simplified Architecture** - Streamlined monolithic architecture with removal of microservices complexity
- **📖 Enhanced Documentation** - Comprehensive documentation updates across all guides
- **🔧 Improved Maintainability** - Removed dead code and simplified codebase structure
- **⚡ Optimized GitHub Actions** - Faster CI/CD workflows with better caching

## ✨ v1.5 Features

- **🔒 Archival-First Storage Pipeline** - Write-ahead logging (WAL) for crash-safe data persistence with checksums
- **📁 Compression Profiles** - Optimized compression profiles for hot/warm/cold storage tiers (LZ4, ZSTD, Gzip)
- **📋 Schema Versioning** - Long-term format preservation with schema migration and JSON Schema export
- **📊 Analysis-Ready Exports** - Pre-built export profiles for Python, R, QuantConnect Lean, Excel, PostgreSQL
- **✅ Data Quality Reports** - Comprehensive quality metrics with outlier detection and gap analysis for exports
- **🔄 Data Versioning** - Dataset fingerprinting and version tracking for reproducible analysis
- **🧙 Configuration Wizard** - Interactive setup wizard for first-time users (`--wizard`)
- **⚡ Auto-Configuration** - Automatic provider detection from environment variables (`--auto-config`)
- **🔍 Provider Detection** - Discover available providers and their status (`--detect-providers`)
- **✅ Credential Validation** - Validate API credentials before running (`--validate-credentials`)

## ✨ v1.4 Features

- **🔷 F# Domain Library** - Type-safe domain models using discriminated unions and exhaustive pattern matching
- **✅ Railway-Oriented Validation** - Composable validation with error accumulation (no more exceptions!)
- **📊 Pure Functional Calculations** - Spread, imbalance, VWAP, TWAP, and order book analytics
- **🔄 Pipeline Transforms** - Declarative stream processing for filtering, enriching, and aggregating events
- **🔗 C# Interop** - Seamless integration with existing C# codebase via wrapper classes

## ✨ v1.3 Features

- **🔌 Unified Provider Abstraction** - Provider-agnostic interface with capability discovery flags
- **📋 Provider Registry** - Attribute-based automatic provider discovery and registration
- **⚡ Concurrent Provider Executor** - Parallel operations across multiple providers with configurable strategies
- **🔄 Circuit Breaker Pattern** - Intelligent failover with automatic recovery and health monitoring
- **📊 Priority Backfill Queue** - Sophisticated job scheduling with dependencies and priority levels
- **🔧 Data Gap Repair** - Automatic detection and repair of missing historical data
- **📈 Data Quality Monitor** - Multi-dimensional quality scoring (completeness, accuracy, timeliness)

## ✨ v1.2 Features

- **🔗 Multi-Provider Connections** - Connect to multiple data providers simultaneously
- **📊 Provider Comparison View** - Side-by-side data quality metrics across providers
- **🔄 Automatic Failover** - Configure automatic failover rules between providers
- **🗺️ Symbol Mapping** - Provider-specific symbol mapping interface
- **📦 Portable Data Packager** - Create self-contained, portable archive packages
- **📅 Data Completeness Calendar** - Visual calendar showing data coverage and gaps
- **🔍 Archive Browser** - In-app file browser for navigating archived data
- **📤 Batch Export Scheduler** - Schedule and automate recurring export jobs

## ✨ v1.1 Features

- **🖥️ Native Windows Desktop App** - UWP/XAML application with modern WinUI 3 styling
- **🔐 Secure Credential Management** - Windows CredentialPicker integration for API keys
- **🎨 Enhanced Dashboard Pages** - Provider, storage, symbols, and backfill configuration

## ✨ v1.0 Features

- **🎨 Modern Web Dashboard** - Full-featured UI for configuration and monitoring
- **📦 Single Executable** - Deploy as one file, no dependencies
- **🛡️ Enhanced Error Handling** - Comprehensive error detection and user-friendly messages
- **📚 Complete Documentation** - Built-in help system with detailed HELP.md guide
- **🎯 Production Ready** - Robust deployment with systemd support

## Supported Data Providers

- **Interactive Brokers (IB)** – L2 depth, tick-by-tick trades, quotes
- **Alpaca** – Real-time trades and quotes via WebSocket
- **Polygon** – Stub implementation ready for expansion
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
./MarketDataCollector --ui
```

Then open your browser to `http://localhost:8080` for a full-featured dashboard with:
- 📊 Real-time system status and metrics
- ⚙️ Point-and-click configuration
- 📈 Symbol management
- 📅 Historical backfill interface
- 💡 Built-in help and tooltips
- 🎨 Modern, responsive UI

### **🖥️ Windows Desktop App** (New!)

Launch the native UWP desktop application on Windows:

```bash
dotnet run --project src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj
```

Features:
- 🔐 Secure credential management via Windows CredentialPicker
- 📊 Native Windows UI with WinUI 3 styling
- ⚡ Direct integration with Windows security features
- 🎨 Tabbed interface for all configuration options

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

**Production mode** with HTTP monitoring and hot-reload:
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

See `docs/operator-runbook.md` for production startup scripts, including the systemd unit and PowerShell helpers.

## Configuration highlights

* `appsettings.json` drives symbol subscriptions (trades/depth), provider settings, and API credentials.
* Hot reload is enabled by default: edits to `appsettings.json` apply without restarting when `--watch-config` is set.
* Set `DataSource` to `IB`, `Alpaca`, or `Polygon` to select the active data provider; BBO snapshots keep recording with stream IDs preserved for reconciliation.
* Configure storage options including naming conventions, date partitioning, retention policies, and capacity limits.

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

* `architecture.md` – layered architecture and event flow
* `c4-diagrams.md` – rendered system, container, and component diagrams
* `domains.md` – event contracts and invariants
* `operator-runbook.md` – operational guidance and startup scripts
* `why-this-architecture.md` – non-technical overview of design decisions
* `interactive-brokers-setup.md` – IB API installation and configuration
* `open-source-references.md` – catalog of related projects and resources
* `lean-integration.md` – QuantConnect Lean Engine integration guide
* `STORAGE_ORGANIZATION_DESIGN.md` – comprehensive storage organization improvements and best practices
* `PROVIDER_MANAGEMENT_ARCHITECTURE.md` – provider abstraction, circuit breakers, and quality monitoring
* `FSHARP_INTEGRATION.md` – F# domain library integration guide and examples

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

See [`docs/FSHARP_INTEGRATION.md`](docs/FSHARP_INTEGRATION.md) for comprehensive documentation.

## Recent Improvements

### Simplified Architecture (2026-01-19) - v1.6.0
- **Monolithic Core**: Removed microservices layer for improved simplicity and maintainability
- **Dead Code Removal**: Cleaned up unused code and deprecated features
- **Documentation Refresh**: Updated all documentation to reflect current architecture
- **Optimized CI/CD**: Improved GitHub Actions workflows with better caching and faster builds

### Archival & Export Excellence (2026-01-04)
- **Archival-First Storage Pipeline**: Write-Ahead Logging (WAL) for crash-safe persistence with SHA256 checksums
- **Compression Profiles**: Pre-built profiles for hot (LZ4), warm (ZSTD-6), and cold (ZSTD-19) storage tiers
- **Schema Versioning**: Long-term format preservation with migration support and JSON Schema export
- **Analysis-Ready Export Formats**: Pre-built profiles for Python/Pandas, R, QuantConnect Lean, Excel, PostgreSQL
- **Data Quality Reports**: Comprehensive quality metrics with outlier detection, gap analysis, and recommendations
- **Data Versioning**: Dataset fingerprinting and version tracking for reproducible analysis

### F# Domain Library (2026-01-03)
- **Type-Safe Domain Models**: Discriminated unions for market events with exhaustive pattern matching
- **Railway-Oriented Validation**: Composable validation with error accumulation using Result types
- **Pure Functional Calculations**: Spread, imbalance, VWAP, TWAP, microprice, and order flow metrics
- **Pipeline Transforms**: Declarative stream processing with filtering, enrichment, and aggregation
- **C# Interop**: Extension methods, wrapper classes, and nullable-friendly APIs for C# consumers
- **Comprehensive Tests**: 50+ unit tests covering domain, validation, calculations, and pipeline logic

### Provider Management & Data Quality System (2026-01-03)
- **Unified Provider Abstraction**: Provider-agnostic interfaces with declarative capability discovery (`ProviderCapabilities` flags)
- **Provider Registry**: Attribute-based automatic provider discovery via `[DataProvider]` attribute
- **Circuit Breaker Pattern**: Intelligent failover with Open/Closed/HalfOpen states and automatic recovery
- **Concurrent Provider Executor**: Parallel operations across providers with configurable strategies (FirstSuccess, All, Merge)
- **Priority Backfill Queue**: Sophisticated job scheduling with Critical/High/Normal/Low/Deferred priority levels
- **Data Gap Repair**: Automatic detection and repair of missing data using alternate providers
- **Data Quality Monitor**: Multi-dimensional quality scoring (Completeness 30%, Accuracy 25%, Timeliness 20%, Consistency 15%, Validity 10%)

### Storage Organization Design (2026-01-02)
- **Comprehensive Design Document**: Best practices for organizing and managing market data at scale
- **Hierarchical Taxonomy**: Enhanced directory structures with metadata catalogs
- **Tiered Storage**: Hot/warm/cold tier architecture for cost-effective data management
- **File Maintenance**: Automated health checks, self-healing, and integrity validation
- **Data Quality**: Quality scoring system with best-of-breed source selection
- **Search Infrastructure**: Multi-level indexes with faceted search capabilities
- **Off-Hours Scheduling**: Trading-hours-aware maintenance automation

### Multi-Provider Support (2026-01-03)
- **Simultaneous Connections**: Connect to IB, Alpaca, and Polygon providers simultaneously
- **Provider Comparison**: Side-by-side data quality metrics (latency, drops, throughput)
- **Automatic Failover**: Configure failover rules with health monitoring and auto-recovery
- **Symbol Mapping**: Provider-specific symbol mapping with FIGI/ISIN/CUSIP support

### Offline Storage & Archival (2026-01-03)
- **Portable Data Packager**: Create self-contained archive packages (ZIP, TAR.GZ) with manifests
- **Data Completeness Calendar**: Visual calendar heatmap showing data coverage by symbol/date
- **Archive Browser**: Tree-view navigation with file metadata, preview, and verification
- **Batch Export Scheduler**: Schedule recurring exports with format conversion (CSV, Parquet)

### UWP Desktop Application (2026-01-02)
- **Native Windows App**: Full-featured UWP/XAML desktop application with WinUI 3 styling
- **Secure Credentials**: Windows CredentialPicker integration for API key management
- **Configuration Pages**: Dashboard, Provider, Storage, Symbols, and Backfill pages
- **Real-time Monitoring**: Live status updates and metrics display

### Code Quality (2026-01-01)
- **Subscription Management**: New `SymbolSubscriptionTracker` base class provides thread-safe subscription handling for depth collectors
- **Logging Standardization**: All components now use `LoggingSetup.ForContext<T>()` for consistent logging
- **Consumer Cleanup**: Removed boilerplate from MassTransit consumer classes
- **Security**: Added `.gitignore` to protect credentials from version control

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

### Current Limitations

**Provider Integration:**
- Alpaca quote messages ("T":"q") not yet wired to QuoteCollector (trade-only currently)
- ✅ IB connection now supports auto-retry via Circuit Breaker pattern
- ✅ Provider failover with automatic recovery implemented

**Security:**
- ✅ Secure credential management via Windows CredentialPicker (UWP app)
- ✅ `.gitignore` now excludes credential files from version control
- API credentials in `appsettings.json` for CLI mode (consider environment variables or secrets manager)
- No built-in authentication for HTTP monitoring endpoints

**Observability:**
- ✅ Structured Serilog logging implemented throughout codebase
- Some error paths may still need additional logging (parse errors, connection failures)

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
