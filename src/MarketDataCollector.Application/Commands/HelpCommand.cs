namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles --help / -h CLI flags.
/// Displays usage information and exits.
/// </summary>
internal sealed class HelpCommand : ICliCommand
{
    public bool CanHandle(string[] args)
    {
        return args.Any(a =>
            a.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-h", StringComparison.OrdinalIgnoreCase));
    }

    public Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        ShowHelp();
        return Task.FromResult(0);
    }

    internal static void ShowHelp()
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════╗
║                    Market Data Collector v1.0                        ║
║          Real-time and historical market data collection             ║
╚══════════════════════════════════════════════════════════════════════╝

USAGE:
    MarketDataCollector [OPTIONS]

MODES:
    --mode <web|desktop|headless> Unified deployment mode selector
    --ui                    Start web dashboard (http://localhost:8080) [legacy]
    --backfill              Run historical data backfill
    --replay <path>         Replay events from JSONL file
    --package               Create a portable data package
    --import-package <path> Import a package into storage
    --list-package <path>   List contents of a package
    --validate-package <path> Validate a package
    --selftest              Run system self-tests
    --validate-config       Validate configuration without starting
    --dry-run               Comprehensive validation without starting (QW-93)
    --help, -h              Show this help message

AUTO-CONFIGURATION (First-time setup):
    --wizard                Interactive configuration wizard (recommended for new users)
    --auto-config           Quick auto-configuration based on environment variables
    --detect-providers      Show available data providers and their status
    --validate-credentials  Validate configured API credentials
    --generate-config       Generate a configuration template

DIAGNOSTICS & TROUBLESHOOTING:
    --quick-check           Fast configuration health check
    --test-connectivity     Test connectivity to all configured providers
    --show-config           Display current configuration summary
    --error-codes           Show error code reference guide
    --check-schemas         Check stored data schema compatibility
    --simulate-feed         Emit a synthetic depth/trade event for smoke testing

SCHEMA VALIDATION OPTIONS:
    --validate-schemas      Run schema check during startup
    --strict-schemas        Exit if schema incompatibilities found (use with --validate-schemas)
    --max-files <n>         Max files to check (default: 100, use with --check-schemas)
    --fail-fast             Stop on first incompatibility (use with --check-schemas)

SYMBOL MANAGEMENT:
    --symbols               Show all symbols (monitored + archived)
    --symbols-monitored     List symbols currently configured for monitoring
    --symbols-archived      List symbols with archived data files
    --symbols-add <list>    Add symbols to configuration (comma-separated)
    --symbols-remove <list> Remove symbols from configuration
    --symbol-status <sym>   Show detailed status for a specific symbol

SYMBOL OPTIONS (use with --symbols-add):
    --no-trades             Don't subscribe to trade data
    --no-depth              Don't subscribe to depth/L2 data
    --depth-levels <n>      Number of depth levels (default: 10)
    --update                Update existing symbols instead of skipping

OPTIONS:
    --config <path>         Path to configuration file (default: appsettings.json)
    --http-port <port>      HTTP server port (default: 8080)
    --watch-config          Enable hot-reload of configuration

ENVIRONMENT VARIABLES:
    MDC_CONFIG_PATH         Alternative to --config argument for specifying config path
    MDC_ENVIRONMENT         Environment name (e.g., Development, Production)
                            Loads appsettings.{Environment}.json as overlay
    DOTNET_ENVIRONMENT      Standard .NET environment variable (fallback for MDC_ENVIRONMENT)

BACKFILL OPTIONS:
    --backfill-provider <name>      Provider to use (default: stooq)
    --backfill-symbols <list>       Comma-separated symbols (e.g., AAPL,MSFT)
    --backfill-from <date>          Start date (YYYY-MM-DD)
    --backfill-to <date>            End date (YYYY-MM-DD)

PACKAGING OPTIONS:
    --package-name <name>           Package name (default: market-data-YYYYMMDD)
    --package-description <text>    Package description
    --package-output <path>         Output directory (default: packages)
    --package-symbols <list>        Comma-separated symbols to include
    --package-events <list>         Event types (Trade,BboQuote,L2Snapshot)
    --package-from <date>           Start date (YYYY-MM-DD)
    --package-to <date>             End date (YYYY-MM-DD)
    --package-format <fmt>          Format: zip, tar.gz (default: zip)
    --package-compression <level>   Compression: none, fast, balanced, max
    --no-quality-report             Exclude quality report from package
    --no-data-dictionary            Exclude data dictionary
    --no-loader-scripts             Exclude loader scripts
    --skip-checksums                Skip checksum verification

IMPORT OPTIONS:
    --import-destination <path>     Destination directory (default: data root)
    --skip-validation               Skip checksum validation during import
    --merge                         Merge with existing data (don't overwrite)

AUTO-CONFIGURATION OPTIONS:
    --template <name>       Template for --generate-config: minimal, full, alpaca,
                            stocksharp, backfill, production, docker (default: minimal)
    --output <path>         Output path for generated config (default: config/appsettings.generated.json)

EXAMPLES:
    # Start web dashboard on default port
    MarketDataCollector --mode web

    # Start web dashboard on custom port
    MarketDataCollector --mode web --http-port 9000

    # Desktop mode (collector + UI server) with hot-reload
    MarketDataCollector --mode desktop --watch-config

    # Run historical backfill
    MarketDataCollector --backfill --backfill-symbols AAPL,MSFT,GOOGL \
        --backfill-from 2024-01-01 --backfill-to 2024-12-31

    # Run self-tests
    MarketDataCollector --selftest

    # Validate configuration without starting
    MarketDataCollector --validate-config

    # Validate a specific configuration file
    MarketDataCollector --validate-config --config /path/to/config.json

    # Create a portable data package
    MarketDataCollector --package --package-name my-data \
        --package-symbols AAPL,MSFT --package-from 2024-01-01

    # Create package with maximum compression
    MarketDataCollector --package --package-compression max

    # Import a package
    MarketDataCollector --import-package ./packages/my-data.zip

    # List package contents
    MarketDataCollector --list-package ./packages/my-data.zip

    # Validate a package
    MarketDataCollector --validate-package ./packages/my-data.zip

    # Run interactive configuration wizard (recommended for new users)
    MarketDataCollector --wizard

    # Quick auto-configuration based on environment variables
    MarketDataCollector --auto-config

    # Detect available data providers
    MarketDataCollector --detect-providers

    # Validate configured API credentials
    MarketDataCollector --validate-credentials

    # Generate a configuration template
    MarketDataCollector --generate-config --template alpaca --output config/appsettings.json

    # Quick configuration health check
    MarketDataCollector --quick-check

    # Test connectivity to all providers
    MarketDataCollector --test-connectivity

    # Show current configuration summary
    MarketDataCollector --show-config

    # View all error codes and their meanings
    MarketDataCollector --error-codes

    # Show all symbols (both monitored and archived)
    MarketDataCollector --symbols

    # Show only symbols currently being monitored
    MarketDataCollector --symbols-monitored

    # Show symbols that have archived data
    MarketDataCollector --symbols-archived

    # Add new symbols for monitoring
    MarketDataCollector --symbols-add AAPL,MSFT,GOOGL

    # Add symbols with custom options
    MarketDataCollector --symbols-add SPY,QQQ --no-depth --depth-levels 5

    # Remove symbols from monitoring
    MarketDataCollector --symbols-remove AAPL,MSFT

    # Check status of a specific symbol
    MarketDataCollector --symbol-status AAPL

CONFIGURATION:
    Configuration is loaded from appsettings.json by default, but can be customized:

    Priority for config file path:
      1. --config argument (highest priority)
      2. MDC_CONFIG_PATH environment variable
      3. appsettings.json (default)

    Environment-specific overlays:
      Set MDC_ENVIRONMENT=Production to automatically load appsettings.Production.json
      as an overlay on top of the base configuration.

    To get started:
      Copy appsettings.sample.json to appsettings.json and customize.

DATA PROVIDERS:
    - Interactive Brokers (IB): Level 2 market depth + trades
    - Alpaca: Real-time trades and quotes via WebSocket
    - Polygon: Real-time and historical data (coming soon)

DOCUMENTATION:
    For detailed documentation, see:
    - HELP.md                    - Complete user guide
    - README.md                  - Project overview
    - docs/CONFIGURATION.md      - Configuration reference
    - docs/GETTING_STARTED.md    - Setup guide
    - docs/TROUBLESHOOTING.md    - Common issues

SUPPORT:
    Report issues: https://github.com/rodoHasArrived/Test/issues
    Documentation: ./HELP.md

╔══════════════════════════════════════════════════════════════════════╗
║  NEW USER?     Run: ./MarketDataCollector --wizard                   ║
║  QUICK CHECK:  Run: ./MarketDataCollector --quick-check              ║
║  START UI:     Run: ./MarketDataCollector --ui                       ║
║  Then open http://localhost:8080 in your browser                     ║
╚══════════════════════════════════════════════════════════════════════╝
");
    }
}
