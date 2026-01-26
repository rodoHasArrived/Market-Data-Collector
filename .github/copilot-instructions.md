# Market Data Collector - Copilot Instructions

## Repository Overview

**Market Data Collector** is a high-performance, cross-platform market data collection system for real-time and historical market microstructure data. It's a production-ready .NET 9.0 solution with F# domain libraries, supporting multiple data providers (Interactive Brokers, Alpaca, Polygon) and offering flexible storage options.

**Project Type:** .NET Solution (C# and F#)
**Target Framework:** .NET 9.0
**Languages:** C# 11, F# 8.0
**Size:** ~50+ project files across 16 projects
**Architecture:** Event-driven, microservices-capable, layered architecture

## Build & Test Commands

### Prerequisites
- .NET SDK 9.0 or later (SDK 10.0.101 confirmed working)
- Docker and Docker Compose (optional, for containerized deployment)

### Key Build Commands

**IMPORTANT:** Always use `/p:EnableWindowsTargeting=true` flag on non-Windows systems to avoid NETSDK1100 errors.

```bash
# Navigate to project root
cd MarketDataCollector

# Restore dependencies (ALWAYS run first)
dotnet restore /p:EnableWindowsTargeting=true

# Build
dotnet build -c Release --no-restore /p:EnableWindowsTargeting=true

# Run tests
dotnet test tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj -c Release --verbosity normal /p:EnableWindowsTargeting=true

# Clean build artifacts
dotnet clean
rm -rf bin/ obj/ publish/
```

### Test Framework
- **Framework:** xUnit
- **Test Projects:**
  - `tests/MarketDataCollector.Tests/` (C#)
  - `tests/MarketDataCollector.FSharp.Tests/` (F#)
- **Mocking:** Moq, NSubstitute, MassTransit.TestFramework
- **Assertions:** FluentAssertions

### Running the Application

```bash
# Basic run (smoke test with no provider)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj

# Run with web dashboard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --http-port 8080

# Run with config hot reload
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --watch-config

# Run self-tests
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --selftest

# Historical backfill
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --backfill --backfill-provider stooq --backfill-symbols SPY,AAPL
```

### Make Commands (Alternative)

The repository includes a comprehensive Makefile for common tasks:

```bash
make help           # Show all available commands
make install        # Interactive installation
make build          # Build the project
make test           # Run tests
make run-ui         # Run with web dashboard
make docker         # Build and start Docker container
make clean          # Clean build artifacts
```

## Project Structure

### Solution Layout

```
MarketDataCollector/
├── src/
│   ├── MarketDataCollector/              # Main console application (C#)
│   ├── MarketDataCollector.Ui/           # Web dashboard UI (C#)
│   ├── MarketDataCollector.Uwp/          # Windows UWP desktop app
│   ├── MarketDataCollector.Contracts/    # Shared contracts
│   ├── MarketDataCollector.FSharp/       # F# domain library
│   └── Microservices/                    # Microservices components
│       ├── Gateway/                      # API Gateway
│       ├── TradeIngestion/               # Trade data service
│       ├── QuoteIngestion/               # Quote data service
│       ├── OrderBookIngestion/           # Order book service
│       ├── HistoricalDataIngestion/      # Historical data service
│       ├── DataValidation/               # Validation service
│       └── Shared/                       # Shared contracts
├── tests/
│   ├── MarketDataCollector.Tests/        # C# unit tests
│   └── MarketDataCollector.FSharp.Tests/ # F# unit tests
├── benchmarks/
│   └── MarketDataCollector.Benchmarks/   # BenchmarkDotNet performance tests
├── docs/                                 # Comprehensive documentation
├── scripts/                              # Build and diagnostic scripts
└── deploy/                               # Deployment configurations
```

### Key Configuration Files

- **`Directory.Build.props`** - Root MSBuild properties, enables `EnableWindowsTargeting=true` for cross-platform builds
- **`appsettings.sample.json`** - Sample configuration (copy to `appsettings.json`)
- **`appsettings.json`** - Actual configuration (gitignored, contains credentials)
- **`MarketDataCollector.sln`** - Solution file for all projects
- **`Makefile`** - Common development tasks
- **`docker-compose.yml`** - Docker deployment configuration

### Main Application Architecture

The main application (`src/MarketDataCollector/`) follows a layered architecture:

- **Infrastructure Layer:** Provider-specific code (IB, Alpaca, Polygon clients)
- **Domain Layer:** Core collectors (TradeDataCollector, MarketDepthCollector, QuoteCollector)
- **Event Pipeline Layer:** Bounded channel event processing, CompositePublisher
- **Storage Layer:** JSONL, Parquet, tiered storage, WAL
- **Application Layer:** Program.cs, ConfigWatcher, StatusHttpServer, BackfillService

### Important Directories

- **`src/MarketDataCollector/Infrastructure/Providers/`** - Data provider implementations
- **`src/MarketDataCollector/Domain/`** - Core domain models
- **`src/MarketDataCollector/Storage/`** - Storage implementations
- **`src/MarketDataCollector/Application/`** - Application services

## CI/CD Workflow

**GitHub Actions:** `.github/workflows/dotnet-desktop.yml`

The CI pipeline runs on pushes to `main` and pull requests:

1. **Build Job** (ubuntu-latest):
   - Checkout code
   - Setup .NET 9.0.x
   - `dotnet restore /p:EnableWindowsTargeting=true`
   - `dotnet build -c Release --no-restore /p:EnableWindowsTargeting=true`
   - `dotnet test -c Release --no-build --verbosity normal /p:EnableWindowsTargeting=true`

2. **Publish Jobs** (multi-platform):
   - Linux x64, Windows x64, macOS x64, macOS ARM64
   - Publishes both `MarketDataCollector` and `MarketDataCollector.Ui` as single-file executables
   - Creates archives (.tar.gz for Unix, .zip for Windows)

3. **Release Job** (on git tags starting with 'v'):
   - Downloads all platform artifacts
   - Creates GitHub release with all platform builds

## Development Practices

### Configuration Management

- **NEVER commit credentials:** `appsettings.json` is gitignored
- **Use environment variables for secrets:** `ALPACA_KEY_ID`, `ALPACA_SECRET_KEY`, etc.
- **Copy sample config:** Always start with `cp appsettings.sample.json appsettings.json`

### Logging

- **Framework:** Serilog with structured logging
- **Initialization:** Use `LoggingSetup.ForContext<T>()` for logger instances
- **Configuration:** Defined in `appsettings.json` under `Serilog` section

### Testing Best Practices

- Use xUnit for test framework
- Use FluentAssertions for readable assertions
- Use Moq or NSubstitute for mocking
- Test files follow naming convention: `<ClassUnderTest>Tests.cs`

### Code Style

- C# 11 with nullable reference types enabled
- Implicit usings enabled
- Follow existing conventions in the codebase
- Use `async`/`await` for I/O operations
- Prefer dependency injection over static classes

## Common Issues & Workarounds

### Issue: Build fails with NETSDK1100 error on Linux/macOS
**Solution:** Always use `/p:EnableWindowsTargeting=true` flag (already set in `Directory.Build.props`)

### Issue: appsettings.json not found
**Solution:** Copy `appsettings.sample.json` to `appsettings.json` and configure

### Issue: Data or logs directories don't exist
**Solution:** Run `mkdir -p data logs` or use `make setup-config`

### Issue: Docker build fails
**Solution:** Ensure `appsettings.json` exists before building: `cp appsettings.sample.json appsettings.json`

### Issue: Tests fail due to missing configuration
**Solution:** Tests should mock configuration or use in-memory configuration. Check test setup.

## Important Files Reference

### Root Directory Files
- `README.md` - Main project documentation
- `HELP.md` - Comprehensive user guide (38KB)
- `DEPENDENCIES.md` - Complete NuGet package documentation
- `Makefile` - Development commands
- `Dockerfile` - Container build definition
- `docker-compose.yml` - Multi-container orchestration
- `install.sh` / `install.ps1` - Installation scripts
- `publish.sh` / `publish.ps1` - Publishing scripts

### Documentation
- `docs/guides/getting-started.md` - Setup guide
- `docs/guides/configuration.md` - Configuration reference
- `docs/architecture/overview.md` - System architecture (detailed)
- `docs/guides/operator-runbook.md` - Operations guide
- `docs/status/improvements.md` - Implementation status and roadmap

### Scripts
- `scripts/diagnose-build.sh` - Build diagnostics with verbose logging
- `scripts/validate-data.sh` - Data validation scripts

## Trust These Instructions

These instructions are comprehensive and accurate as of the last documentation date. Only search the codebase if:
- You need to understand implementation details not covered here
- Information appears outdated or contradictory
- You need to verify behavior of a specific component

When in doubt, refer to the extensive documentation in the `docs/` directory, particularly:
- Architecture diagrams: `docs/architecture/`
- Configuration details: `docs/guides/configuration.md`
- Troubleshooting: `HELP.md`

## Quick Decision Tree

**Adding new functionality?**
→ Add to appropriate layer in `src/MarketDataCollector/`, follow existing patterns

**Fixing a bug?**
→ Add test first in `tests/MarketDataCollector.Tests/`, then fix

**Working with providers?**
→ Look in `src/MarketDataCollector/Infrastructure/Providers/`

**Storage changes?**
→ Check `src/MarketDataCollector/Storage/`

**Need to run tests?**
→ `dotnet test tests/MarketDataCollector.Tests/` (C# tests only)

**Need to build?**
→ `dotnet restore /p:EnableWindowsTargeting=true` then `dotnet build -c Release /p:EnableWindowsTargeting=true`

**Starting the app?**
→ `dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui` for web dashboard
