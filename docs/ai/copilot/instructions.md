# Market Data Collector - Copilot Instructions

> **Note:** For comprehensive project context, see [CLAUDE.md](../CLAUDE.md) in the repository root.

## Repository Overview

**Market Data Collector** is a high-performance, cross-platform market data collection system for real-time and historical market microstructure data. It's a production-ready .NET 9.0 solution with F# domain libraries, supporting multiple data providers (Interactive Brokers, Alpaca, NYSE, Polygon, StockSharp) and offering flexible storage options.

**Project Type:** .NET Solution (C# and F#)
**Target Framework:** .NET 9.0
**Languages:** C# 11, F# 8.0
**Size:** 478 source files (466 C#, 12 F#) across 6 main projects
**Architecture:** Event-driven, monolithic core with optional UI projects

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
│   ├── MarketDataCollector.Ui.Shared/    # Shared UI services & endpoints
│   ├── MarketDataCollector.Uwp/          # Windows UWP desktop app (WinUI 3)
│   ├── MarketDataCollector.Contracts/    # Shared contracts and DTOs
│   └── MarketDataCollector.FSharp/       # F# domain library (12 files)
├── tests/
│   ├── MarketDataCollector.Tests/        # C# unit tests (50 files)
│   └── MarketDataCollector.FSharp.Tests/ # F# unit tests
├── benchmarks/
│   └── MarketDataCollector.Benchmarks/   # BenchmarkDotNet performance tests
├── docs/                                 # Comprehensive documentation (61 files)
├── scripts/                              # Build and diagnostic scripts
├── build-system/                         # Python build tooling
└── deploy/                               # Deployment configurations
```

## Repository Structure

```
Market-Data-Collector/
├── .claude/
│   └── settings.local.json
├── .github/  # GitHub configuration
│   ├── actions/
│   │   └── setup-dotnet-cache/
│   │       └── action.yml
│   ├── agents/
│   │   └── documentation-agent.md
│   ├── ISSUE_TEMPLATE/
│   │   ├── .gitkeep
│   │   ├── bug_report.yml
│   │   ├── config.yml
│   │   └── feature_request.yml
│   ├── prompts/
│   │   ├── add-data-provider.prompt.yml
│   │   ├── add-export-format.prompt.yml
│   │   ├── code-review.prompt.yml
│   │   ├── configure-deployment.prompt.yml
│   │   ├── explain-architecture.prompt.yml
│   │   ├── optimize-performance.prompt.yml
│   │   ├── project-context.prompt.yml
│   │   ├── provider-implementation-guide.prompt.yml
│   │   ├── README.md
│   │   ├── troubleshoot-issue.prompt.yml
│   │   ├── wpf-debug-improve.prompt.yml
│   │   └── write-unit-tests.prompt.yml
│   ├── workflows/
│   │   ├── ai-instructions-sync.yml
│   │   ├── benchmark.yml
│   │   ├── build-observability.yml
│   │   ├── cache-management.yml
│   │   ├── code-quality.yml
│   │   ├── dependency-review.yml
│   │   ├── desktop-app.yml
│   │   ├── docker.yml
│   │   ├── docs-auto-update.yml
│   │   ├── docs-structure-sync.yml
│   │   ├── documentation.yml
│   │   ├── dotnet-desktop.yml
│   │   ├── labeling.yml
│   │   ├── nightly.yml
│   │   ├── pr-checks.yml
│   │   ├── README.md
│   │   ├── release.yml
│   │   ├── reusable-dotnet-build.yml
│   │   ├── scheduled-maintenance.yml
│   │   ├── security.yml
│   │   ├── stale.yml
│   │   ├── test-matrix.yml
│   │   ├── todo-automation.yml
│   │   ├── validate-workflows.yml
│   │   ├── wpf-commands.yml
│   │   └── wpf-desktop.yml
│   ├── dependabot.yml
│   ├── labeler.yml
│   ├── labels.yml
│   ├── markdown-link-check-config.json
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── QUICKSTART.md
│   ├── spellcheck-config.yml
│   └── WORKFLOW_IMPROVEMENTS.md
├── benchmarks/  # Performance benchmarks
│   └── MarketDataCollector.Benchmarks/
│       ├── EventPipelineBenchmarks.cs
│       ├── IndicatorBenchmarks.cs
│       ├── JsonSerializationBenchmarks.cs
│       ├── MarketDataCollector.Benchmarks.csproj
│       └── Program.cs
├── build/
│   ├── dotnet/
│   │   ├── DocGenerator/
│   │   │   ├── DocGenerator.csproj
│   │   │   └── Program.cs
│   │   └── FSharpInteropGenerator/
│   │       ├── FSharpInteropGenerator.csproj
│   │       └── Program.cs
│   ├── node/
│   │   ├── generate-diagrams.mjs
│   │   └── generate-icons.mjs
│   ├── python/
│   │   ├── adapters/
│   │   │   ├── __init__.py
│   │   │   └── dotnet.py
│   │   ├── analytics/
│   │   │   ├── __init__.py
│   │   │   ├── history.py
│   │   │   ├── metrics.py
│   │   │   └── profile.py
│   │   ├── cli/
│   │   │   └── buildctl.py
│   │   ├── core/
│   │   │   ├── __init__.py
│   │   │   ├── events.py
│   │   │   ├── fingerprint.py
│   │   │   ├── graph.py
│   │   │   └── utils.py
│   │   ├── diagnostics/
│   │   │   ├── __init__.py
│   │   │   ├── doctor.py
│   │   │   ├── env_diff.py
│   │   │   ├── error_matcher.py
│   │   │   ├── preflight.py
│   │   │   └── validate_data.py
│   │   ├── knowledge/
│   │   │   └── errors/
│   │   │       ...
│   │   └── __init__.py
│   └── scripts/  # Automation scripts
│       ├── docs/  # Documentation
│       │   ├── generate-structure-docs.py
│       │   ├── scan-todos.py
│       │   └── update-claude-md.py
│       ├── install/
│       │   ├── install.ps1
│       │   └── install.sh
│       ├── lib/
│       │   └── BuildNotification.psm1
│       └── run/
│           ├── start-collector.ps1
│           ├── start-collector.sh
│           ├── stop-collector.ps1
│           └── stop-collector.sh
├── config/  # Configuration files
│   ├── appsettings.json
│   └── appsettings.sample.json
├── deploy/  # Deployment configurations
│   ├── docker/
│   │   ├── .dockerignore
│   │   ├── docker-compose.yml
│   │   └── Dockerfile
│   ├── monitoring/
│   │   ├── grafana/
│   │   │   └── provisioning/
│   │   │       ...
│   │   └── prometheus.yml
│   └── systemd/
│       └── marketdatacollector.service
├── docs/  # Documentation
│   ├── adr/
│   │   ├── 001-provider-abstraction.md
│   │   ├── 002-tiered-storage-architecture.md
│   │   ├── 003-microservices-decomposition.md
│   │   ├── 004-async-streaming-patterns.md
│   │   ├── 005-attribute-based-discovery.md
│   │   ├── 010-httpclient-factory.md
│   │   ├── 011-centralized-configuration-and-credentials.md
│   │   ├── 012-monitoring-and-alerting-pipeline.md
│   │   ├── _template.md
│   │   └── README.md
│   ├── ai/
│   │   ├── claude/
│   │   │   ├── CLAUDE.actions.md
│   │   │   ├── CLAUDE.fsharp.md
│   │   │   ├── CLAUDE.providers.md
│   │   │   ├── CLAUDE.storage.md
│   │   │   └── CLAUDE.testing.md
│   │   ├── copilot/
│   │   │   └── instructions.md
│   │   └── README.md
│   ├── api/
│   │   └── index.md
│   ├── architecture/
│   │   ├── c4-context.png
│   │   ├── c4-context.puml
│   │   ├── c4-diagrams.md
│   │   ├── consolidation.md
│   │   ├── crystallized-storage-format.md
│   │   ├── domains.md
│   │   ├── overview.md
│   │   ├── provider-management.md
│   │   ├── storage-design.md
│   │   └── why-this-architecture.md
│   ├── archived/
│   │   ├── CHANGES_SUMMARY.md
│   │   ├── desktop-ui-alternatives-evaluation.md
│   │   ├── README.md
│   │   ├── REPOSITORY_REORGANIZATION_PLAN.md
│   │   ├── uwp-development-roadmap.md
│   │   └── uwp-release-checklist.md
│   ├── design/
│   │   ├── REDESIGN_IMPROVEMENTS.md
│   │   └── ui-redesign.md
│   ├── development/
│   │   ├── desktop-app-xaml-compiler-errors.md
│   │   ├── github-actions-summary.md
│   │   ├── github-actions-testing.md
│   │   ├── project-context.md
│   │   ├── provider-implementation.md
│   │   ├── uwp-to-wpf-migration.md
│   │   └── wpf-implementation-notes.md
│   ├── diagrams/
│   │   ├── c4-level1-context.dot
│   │   ├── c4-level1-context.png
│   │   ├── c4-level1-context.svg
│   │   ├── c4-level2-containers.dot
│   │   ├── c4-level2-containers.png
│   │   ├── c4-level2-containers.svg
│   │   ├── c4-level3-components.dot
│   │   ├── c4-level3-components.png
│   │   ├── c4-level3-components.svg
│   │   ├── cli-commands.dot
│   │   ├── cli-commands.png
│   │   ├── cli-commands.svg
│   │   ├── data-flow.dot
│   │   ├── data-flow.png
│   │   ├── deployment-options.dot
│   │   ├── deployment-options.png
│   │   ├── event-pipeline-sequence.dot
│   │   ├── event-pipeline-sequence.png
│   │   ├── onboarding-flow.dot
│   │   ├── onboarding-flow.png
│   │   ├── onboarding-flow.svg
│   │   ├── provider-architecture.dot
│   │   ├── provider-architecture.png
│   │   ├── README.md
│   │   ├── resilience-patterns.dot
│   │   ├── resilience-patterns.png
│   │   ├── storage-architecture.dot
│   │   └── storage-architecture.png
│   ├── docfx/
│   │   ├── docfx.json
│   │   └── README.md
│   ├── evaluations/
│   │   ├── data-quality-monitoring-evaluation.md
│   │   ├── historical-data-providers-evaluation.md
│   │   ├── realtime-streaming-architecture-evaluation.md
│   │   └── storage-architecture-evaluation.md
│   ├── getting-started/
│   │   └── README.md
│   ├── integrations/
│   │   ├── fsharp-integration.md
│   │   ├── language-strategy.md
│   │   └── lean-integration.md
│   ├── operations/
│   │   ├── msix-packaging.md
│   │   ├── operator-runbook.md
│   │   └── portable-data-packager.md
│   ├── providers/
│   │   ├── alpaca-setup.md
│   │   ├── backfill-guide.md
│   │   ├── data-sources.md
│   │   ├── interactive-brokers-free-equity-reference.md
│   │   ├── interactive-brokers-setup.md
│   │   └── provider-comparison.md
│   ├── reference/
│   │   ├── data-dictionary.md
│   │   ├── data-uniformity.md
│   │   ├── design-review-memo.md
│   │   ├── DUPLICATE_CODE_ANALYSIS.md
│   │   ├── open-source-references.md
│   │   └── sandcastle.md
│   ├── status/
│   │   ├── CHANGELOG.md
│   │   ├── production-status.md
│   │   ├── README.md
│   │   ├── ROADMAP.md
│   │   └── TODO.md
│   ├── uml/
│   │   ├── activity-diagram-backfill.png
│   │   ├── activity-diagram-backfill.puml
│   │   ├── activity-diagram.png
│   │   ├── activity-diagram.puml
│   │   ├── communication-diagram.png
│   │   ├── communication-diagram.puml
│   │   ├── interaction-overview-diagram.png
│   │   ├── interaction-overview-diagram.puml
│   │   ├── README.md
│   │   ├── sequence-diagram-backfill.png
│   │   ├── sequence-diagram-backfill.puml
│   │   ├── sequence-diagram.png
│   │   ├── sequence-diagram.puml
│   │   ├── state-diagram-backfill.png
│   │   ├── state-diagram-backfill.puml
│   │   ├── state-diagram-orderbook.png
│   │   ├── state-diagram-orderbook.puml
│   │   ├── state-diagram-trade-sequence.png
│   │   ├── state-diagram-trade-sequence.puml
│   │   ├── state-diagram.png
│   │   ├── state-diagram.puml
│   │   ├── timing-diagram-backfill.png
│   │   ├── timing-diagram-backfill.puml
│   │   ├── timing-diagram.png
│   │   ├── timing-diagram.puml
│   │   ├── use-case-diagram.png
│   │   └── use-case-diagram.puml
│   ├── ARTIFACT_ACTIONS_DOWNGRADE.md
│   ├── build-observability.md
│   ├── DEPENDENCIES.md
│   ├── HELP.md
│   ├── README.md
│   └── toc.yml
├── src/  # Source code
│   ├── MarketDataCollector/
│   │   ├── Application/
│   │   │   ├── Backfill/
│   │   │   │   ...
│   │   │   ├── Composition/
│   │   │   │   ...
│   │   │   ├── Config/
│   │   │   │   ...
│   │   │   ├── Credentials/
│   │   │   │   ...
│   │   │   ├── Exceptions/
│   │   │   │   ...
│   │   │   ├── Filters/
│   │   │   │   ...
│   │   │   ├── Http/
│   │   │   │   ...
│   │   │   ├── Indicators/
│   │   │   │   ...
│   │   │   ├── Logging/
│   │   │   │   ...
│   │   │   ├── Monitoring/
│   │   │   │   ...
│   │   │   ├── Pipeline/
│   │   │   │   ...
│   │   │   ├── Results/
│   │   │   │   ...
│   │   │   ├── Scheduling/
│   │   │   │   ...
│   │   │   ├── Serialization/
│   │   │   │   ...
│   │   │   ├── Services/
│   │   │   │   ...
│   │   │   ├── Subscriptions/
│   │   │   │   ...
│   │   │   ├── Testing/
│   │   │   │   ...
│   │   │   └── Tracing/
│   │   │       ...
│   │   ├── Domain/
│   │   │   ├── Collectors/
│   │   │   │   ...
│   │   │   ├── Events/
│   │   │   │   ...
│   │   │   └── Models/
│   │   │       ...
│   │   ├── Infrastructure/
│   │   │   ├── Contracts/
│   │   │   │   ...
│   │   │   ├── DataSources/
│   │   │   │   ...
│   │   │   ├── Http/
│   │   │   │   ...
│   │   │   ├── Performance/
│   │   │   │   ...
│   │   │   ├── Providers/
│   │   │   │   ...
│   │   │   ├── Resilience/
│   │   │   │   ...
│   │   │   ├── Shared/
│   │   │   │   ...
│   │   │   ├── Utilities/
│   │   │   │   ...
│   │   │   ├── IMarketDataClient.cs
│   │   │   └── NoOpMarketDataClient.cs
│   │   ├── Integrations/
│   │   │   └── Lean/
│   │   │       ...
│   │   ├── Storage/
│   │   │   ├── Archival/
│   │   │   │   ...
│   │   │   ├── Export/
│   │   │   │   ...
│   │   │   ├── Interfaces/
│   │   │   │   ...
│   │   │   ├── Maintenance/
│   │   │   │   ...
│   │   │   ├── Packaging/
│   │   │   │   ...
│   │   │   ├── Policies/
│   │   │   │   ...
│   │   │   ├── Replay/
│   │   │   │   ...
│   │   │   ├── Services/
│   │   │   │   ...
│   │   │   ├── Sinks/
│   │   │   │   ...
│   │   │   ├── StorageOptions.cs
│   │   │   └── StorageProfiles.cs
│   │   ├── Tools/
│   │   │   └── DataValidator.cs
│   │   ├── wwwroot/
│   │   │   └── templates/
│   │   │       ...
│   │   ├── app.manifest
│   │   ├── GlobalUsings.cs
│   │   ├── MarketDataCollector.csproj
│   │   ├── Program.cs
│   │   └── runtimeconfig.template.json
│   ├── MarketDataCollector.Contracts/
│   │   ├── Api/
│   │   │   ├── BackfillApiModels.cs
│   │   │   ├── ClientModels.cs
│   │   │   ├── ErrorResponse.cs
│   │   │   ├── ProviderCatalog.cs
│   │   │   ├── StatusEndpointModels.cs
│   │   │   ├── StatusModels.cs
│   │   │   ├── UiApiClient.cs
│   │   │   ├── UiApiRoutes.cs
│   │   │   └── UiDashboardModels.cs
│   │   ├── Archive/
│   │   │   └── ArchiveHealthModels.cs
│   │   ├── Backfill/
│   │   │   └── BackfillProgress.cs
│   │   ├── Catalog/
│   │   │   ├── DirectoryIndex.cs
│   │   │   ├── StorageCatalog.cs
│   │   │   └── SymbolRegistry.cs
│   │   ├── Configuration/
│   │   │   └── AppConfigDto.cs
│   │   ├── Credentials/
│   │   │   └── CredentialModels.cs
│   │   ├── Domain/
│   │   │   ├── Enums/
│   │   │   │   ...
│   │   │   ├── Events/
│   │   │   │   ...
│   │   │   ├── Models/
│   │   │   │   ...
│   │   │   └── MarketDataModels.cs
│   │   ├── Export/
│   │   │   └── ExportPreset.cs
│   │   ├── Manifest/
│   │   │   └── DataManifest.cs
│   │   ├── Pipeline/
│   │   │   └── PipelinePolicyConstants.cs
│   │   ├── Schema/
│   │   │   └── EventSchema.cs
│   │   ├── Session/
│   │   │   └── CollectionSession.cs
│   │   └── MarketDataCollector.Contracts.csproj
│   ├── MarketDataCollector.FSharp/
│   │   ├── Calculations/
│   │   │   ├── Aggregations.fs
│   │   │   ├── Imbalance.fs
│   │   │   └── Spread.fs
│   │   ├── Domain/
│   │   │   ├── Integrity.fs
│   │   │   ├── MarketEvents.fs
│   │   │   └── Sides.fs
│   │   ├── Generated/
│   │   │   └── MarketDataCollector.FSharp.Interop.g.cs
│   │   ├── Pipeline/
│   │   │   └── Transforms.fs
│   │   ├── Validation/
│   │   │   ├── QuoteValidator.fs
│   │   │   ├── TradeValidator.fs
│   │   │   ├── ValidationPipeline.fs
│   │   │   └── ValidationTypes.fs
│   │   ├── Interop.fs
│   │   └── MarketDataCollector.FSharp.fsproj
│   ├── MarketDataCollector.Ui/
│   │   ├── wwwroot/
│   │   │   └── static/
│   │   │       ...
│   │   ├── app.manifest
│   │   ├── MarketDataCollector.Ui.csproj
│   │   └── Program.cs
│   ├── MarketDataCollector.Ui.Shared/
│   │   ├── Endpoints/
│   │   │   ├── BackfillEndpoints.cs
│   │   │   ├── ConfigEndpoints.cs
│   │   │   ├── FailoverEndpoints.cs
│   │   │   ├── ProviderEndpoints.cs
│   │   │   ├── StatusEndpoints.cs
│   │   │   ├── SymbolMappingEndpoints.cs
│   │   │   └── UiEndpoints.cs
│   │   ├── Services/
│   │   │   ├── BackfillCoordinator.cs
│   │   │   └── ConfigStore.cs
│   │   ├── DtoExtensions.cs
│   │   ├── HtmlTemplates.cs
│   │   └── MarketDataCollector.Ui.Shared.csproj
│   ├── MarketDataCollector.Uwp/
│   │   ├── Assets/
│   │   │   ├── Icons/
│   │   │   │   ...
│   │   │   ├── Source/
│   │   │   │   ...
│   │   │   ├── AppIcon.svg
│   │   │   ├── BadgeLogo.png
│   │   │   ├── BadgeLogo.scale-100.png
│   │   │   ├── BadgeLogo.scale-125.png
│   │   │   ├── BadgeLogo.scale-150.png
│   │   │   ├── BadgeLogo.scale-200.png
│   │   │   ├── BadgeLogo.scale-400.png
│   │   │   ├── generate-assets.ps1
│   │   │   ├── generate-assets.sh
│   │   │   ├── LargeTile.scale-100.png
│   │   │   ├── LargeTile.scale-125.png
│   │   │   ├── LargeTile.scale-150.png
│   │   │   ├── LargeTile.scale-200.png
│   │   │   ├── README.md
│   │   │   ├── SmallTile.scale-100.png
│   │   │   ├── SmallTile.scale-125.png
│   │   │   ├── SmallTile.scale-150.png
│   │   │   ├── SmallTile.scale-200.png
│   │   │   ├── SplashScreen.scale-100.png
│   │   │   ├── SplashScreen.scale-125.png
│   │   │   ├── SplashScreen.scale-150.png
│   │   │   ├── SplashScreen.scale-200.png
│   │   │   ├── Square150x150Logo.png
│   │   │   ├── Square150x150Logo.scale-100.png
│   │   │   ├── Square150x150Logo.scale-125.png
│   │   │   ├── Square150x150Logo.scale-150.png
│   │   │   ├── Square150x150Logo.scale-200.png
│   │   │   ├── Square150x150Logo.scale-400.png
│   │   │   ├── Square44x44Logo.png
│   │   │   ├── Square44x44Logo.scale-100.png
│   │   │   ├── Square44x44Logo.scale-125.png
│   │   │   ├── Square44x44Logo.scale-150.png
│   │   │   ├── Square44x44Logo.scale-200.png
│   │   │   ├── Square44x44Logo.scale-400.png
│   │   │   ├── Square44x44Logo.targetsize-16.png
│   │   │   ├── Square44x44Logo.targetsize-24.png
│   │   │   ├── Square44x44Logo.targetsize-256.png
│   │   │   ├── Square44x44Logo.targetsize-32.png
│   │   │   ├── Square44x44Logo.targetsize-48.png
│   │   │   ├── StoreLogo.png
│   │   │   ├── StoreLogo.scale-100.png
│   │   │   ├── StoreLogo.scale-125.png
│   │   │   ├── StoreLogo.scale-150.png
│   │   │   ├── StoreLogo.scale-200.png
│   │   │   ├── StoreLogo.scale-400.png
│   │   │   ├── Wide310x150Logo.scale-100.png
│   │   │   ├── Wide310x150Logo.scale-125.png
│   │   │   ├── Wide310x150Logo.scale-150.png
│   │   │   └── Wide310x150Logo.scale-200.png
│   │   ├── Collections/
│   │   │   ├── BoundedObservableCollection.cs
│   │   │   └── CircularBuffer.cs
│   │   ├── Contracts/
│   │   │   ├── IConfigService.cs
│   │   │   ├── IConnectionService.cs
│   │   │   ├── INavigationService.cs
│   │   │   └── IStatusService.cs
│   │   ├── Controls/
│   │   │   ├── AlertBanner.xaml
│   │   │   ├── AlertBanner.xaml.cs
│   │   │   ├── DataCoverageCalendar.xaml
│   │   │   ├── DataCoverageCalendar.xaml.cs
│   │   │   ├── DataTable.xaml
│   │   │   ├── DataTable.xaml.cs
│   │   │   ├── LoadingOverlay.xaml
│   │   │   ├── LoadingOverlay.xaml.cs
│   │   │   ├── MetricCard.xaml
│   │   │   ├── MetricCard.xaml.cs
│   │   │   ├── ProgressCard.xaml
│   │   │   ├── ProgressCard.xaml.cs
│   │   │   ├── SectionHeader.xaml
│   │   │   ├── SectionHeader.xaml.cs
│   │   │   ├── StatusBadge.xaml
│   │   │   └── StatusBadge.xaml.cs
│   │   ├── Converters/
│   │   │   └── BoolConverters.cs
│   │   ├── Dialogs/
│   │   │   ├── BackfillWizardDialog.xaml
│   │   │   └── BackfillWizardDialog.xaml.cs
│   │   ├── Examples/
│   │   │   ├── CardLayoutExamples.xaml
│   │   │   ├── ChartExamples.xaml
│   │   │   ├── ComponentExamples.xaml
│   │   │   ├── DataGridExamples.xaml
│   │   │   ├── NotificationExamples.xaml
│   │   │   ├── README.md
│   │   │   ├── StatusIndicatorExamples.xaml
│   │   │   └── VisualDesignGuide.md
│   │   ├── Extensions/
│   │   │   └── TaskExtensions.cs
│   │   ├── Helpers/
│   │   │   ├── AccessibilityHelper.cs
│   │   │   └── ResponsiveLayoutHelper.cs
│   │   ├── Models/
│   │   │   ├── AppConfig.cs
│   │   │   ├── OfflineTrackingModels.cs
│   │   │   └── SharedModelAliases.cs
│   │   ├── Services/
│   │   │   ├── ActivityFeedService.cs
│   │   │   ├── AdminMaintenanceService.cs
│   │   │   ├── AdvancedAnalyticsService.cs
│   │   │   ├── AnalysisExportWizardService.cs
│   │   │   ├── ApiClientService.cs
│   │   │   ├── ArchiveBrowserService.cs
│   │   │   ├── ArchiveHealthService.cs
│   │   │   ├── BackfillService.cs
│   │   │   ├── BackgroundTaskSchedulerService.cs
│   │   │   ├── BatchExportSchedulerService.cs
│   │   │   ├── BrushRegistry.cs
│   │   │   ├── ChartingService.cs
│   │   │   ├── CollectionSessionService.cs
│   │   │   ├── ConfigService.cs
│   │   │   ├── ConnectionService.cs
│   │   │   ├── ContextMenuService.cs
│   │   │   ├── CredentialService.cs
│   │   │   ├── DataCalendarService.cs
│   │   │   ├── DataCompletenessService.cs
│   │   │   ├── DataSamplingService.cs
│   │   │   ├── DiagnosticsService.cs
│   │   │   ├── ErrorHandlingService.cs
│   │   │   ├── ErrorMessages.cs
│   │   │   ├── EventReplayService.cs
│   │   │   ├── ExportPresetService.cs
│   │   │   ├── FirstRunService.cs
│   │   │   ├── FormValidationService.cs
│   │   │   ├── HttpClientConfiguration.cs
│   │   │   ├── InfoBarService.cs
│   │   │   ├── IntegrityEventsService.cs
│   │   │   ├── KeyboardShortcutService.cs
│   │   │   ├── LeanIntegrationService.cs
│   │   │   ├── LiveDataService.cs
│   │   │   ├── LoggingService.cs
│   │   │   ├── ManifestService.cs
│   │   │   ├── MessagingService.cs
│   │   │   ├── NavigationService.cs
│   │   │   ├── NotificationService.cs
│   │   │   ├── OAuthRefreshService.cs
│   │   │   ├── OfflineTrackingPersistenceService.cs
│   │   │   ├── OrderBookVisualizationService.cs
│   │   │   ├── PendingOperationsQueueService.cs
│   │   │   ├── PortablePackagerService.cs
│   │   │   ├── PortfolioImportService.cs
│   │   │   ├── ProviderHealthService.cs
│   │   │   ├── ProviderManagementService.cs
│   │   │   ├── RetentionAssuranceService.cs
│   │   │   ├── ScheduledMaintenanceService.cs
│   │   │   ├── ScheduleManagerService.cs
│   │   │   ├── SchemaService.cs
│   │   │   ├── SearchService.cs
│   │   │   ├── SetupWizardService.cs
│   │   │   ├── SmartRecommendationsService.cs
│   │   │   ├── StatusService.cs
│   │   │   ├── StorageAnalyticsService.cs
│   │   │   ├── StorageOptimizationAdvisorService.cs
│   │   │   ├── StorageService.cs
│   │   │   ├── SymbolGroupService.cs
│   │   │   ├── SymbolManagementService.cs
│   │   │   ├── SymbolMappingService.cs
│   │   │   ├── SystemHealthService.cs
│   │   │   ├── ThemeService.cs
│   │   │   ├── TimeSeriesAlignmentService.cs
│   │   │   ├── TooltipService.cs
│   │   │   ├── UwpAnalysisExportService.cs
│   │   │   ├── UwpDataQualityService.cs
│   │   │   ├── UwpJsonOptions.cs
│   │   │   ├── WatchlistService.cs
│   │   │   └── WorkspaceService.cs
│   │   ├── Styles/
│   │   │   ├── Animations.xaml
│   │   │   ├── AppStyles.xaml
│   │   │   └── IconResources.xaml
│   │   ├── ViewModels/
│   │   │   ├── BackfillViewModel.cs
│   │   │   ├── DashboardViewModel.cs
│   │   │   ├── DataExportViewModel.cs
│   │   │   ├── DataQualityViewModel.cs
│   │   │   └── MainViewModel.cs
│   │   ├── Views/
│   │   │   ├── AdminMaintenancePage.xaml
│   │   │   ├── AdminMaintenancePage.xaml.cs
│   │   │   ├── AdvancedAnalyticsPage.xaml
│   │   │   ├── AdvancedAnalyticsPage.xaml.cs
│   │   │   ├── AnalysisExportPage.xaml
│   │   │   ├── AnalysisExportPage.xaml.cs
│   │   │   ├── AnalysisExportWizardPage.xaml
│   │   │   ├── AnalysisExportWizardPage.xaml.cs
│   │   │   ├── ArchiveHealthPage.xaml
│   │   │   ├── ArchiveHealthPage.xaml.cs
│   │   │   ├── BackfillPage.xaml
│   │   │   ├── BackfillPage.xaml.cs
│   │   │   ├── ChartingPage.xaml
│   │   │   ├── ChartingPage.xaml.cs
│   │   │   ├── CollectionSessionPage.xaml
│   │   │   ├── CollectionSessionPage.xaml.cs
│   │   │   ├── DashboardPage.xaml
│   │   │   ├── DashboardPage.xaml.cs
│   │   │   ├── DataBrowserPage.xaml
│   │   │   ├── DataBrowserPage.xaml.cs
│   │   │   ├── DataCalendarPage.xaml
│   │   │   ├── DataCalendarPage.xaml.cs
│   │   │   ├── DataExportPage.xaml
│   │   │   ├── DataExportPage.xaml.cs
│   │   │   ├── DataQualityPage.xaml
│   │   │   ├── DataQualityPage.xaml.cs
│   │   │   ├── DataSamplingPage.xaml
│   │   │   ├── DataSamplingPage.xaml.cs
│   │   │   ├── DataSourcesPage.xaml
│   │   │   ├── DataSourcesPage.xaml.cs
│   │   │   ├── DiagnosticsPage.xaml
│   │   │   ├── DiagnosticsPage.xaml.cs
│   │   │   ├── EventReplayPage.xaml
│   │   │   ├── EventReplayPage.xaml.cs
│   │   │   ├── ExportPresetsPage.xaml
│   │   │   ├── ExportPresetsPage.xaml.cs
│   │   │   ├── HelpPage.xaml
│   │   │   ├── HelpPage.xaml.cs
│   │   │   ├── IndexSubscriptionPage.xaml
│   │   │   ├── IndexSubscriptionPage.xaml.cs
│   │   │   ├── KeyboardShortcutsPage.xaml
│   │   │   ├── KeyboardShortcutsPage.xaml.cs
│   │   │   ├── LeanIntegrationPage.xaml
│   │   │   ├── LeanIntegrationPage.xaml.cs
│   │   │   ├── LiveDataViewerPage.xaml
│   │   │   ├── LiveDataViewerPage.xaml.cs
│   │   │   ├── MainPage.xaml
│   │   │   ├── MainPage.xaml.cs
│   │   │   ├── MessagingHubPage.xaml
│   │   │   ├── MessagingHubPage.xaml.cs
│   │   │   ├── NotificationCenterPage.xaml
│   │   │   ├── NotificationCenterPage.xaml.cs
│   │   │   ├── OrderBookPage.xaml
│   │   │   ├── OrderBookPage.xaml.cs
│   │   │   ├── PackageManagerPage.xaml
│   │   │   ├── PackageManagerPage.xaml.cs
│   │   │   ├── PortfolioImportPage.xaml
│   │   │   ├── PortfolioImportPage.xaml.cs
│   │   │   ├── ProviderHealthPage.xaml
│   │   │   ├── ProviderHealthPage.xaml.cs
│   │   │   ├── ProviderPage.xaml
│   │   │   ├── ProviderPage.xaml.cs
│   │   │   ├── RetentionAssurancePage.xaml
│   │   │   ├── RetentionAssurancePage.xaml.cs
│   │   │   ├── ScheduleManagerPage.xaml
│   │   │   ├── ScheduleManagerPage.xaml.cs
│   │   │   ├── ServiceManagerPage.xaml
│   │   │   ├── ServiceManagerPage.xaml.cs
│   │   │   ├── SettingsPage.xaml
│   │   │   ├── SettingsPage.xaml.cs
│   │   │   ├── SetupWizardPage.xaml
│   │   │   ├── SetupWizardPage.xaml.cs
│   │   │   ├── StorageOptimizationPage.xaml
│   │   │   ├── StorageOptimizationPage.xaml.cs
│   │   │   ├── StoragePage.xaml
│   │   │   ├── StoragePage.xaml.cs
│   │   │   ├── SymbolMappingPage.xaml
│   │   │   ├── SymbolMappingPage.xaml.cs
│   │   │   ├── SymbolsPage.xaml
│   │   │   ├── SymbolsPage.xaml.cs
│   │   │   ├── SymbolStoragePage.xaml
│   │   │   ├── SymbolStoragePage.xaml.cs
│   │   │   ├── SystemHealthPage.xaml
│   │   │   ├── SystemHealthPage.xaml.cs
│   │   │   ├── TimeSeriesAlignmentPage.xaml
│   │   │   ├── TimeSeriesAlignmentPage.xaml.cs
│   │   │   ├── TradingHoursPage.xaml
│   │   │   ├── TradingHoursPage.xaml.cs
│   │   │   ├── WatchlistPage.xaml
│   │   │   ├── WatchlistPage.xaml.cs
│   │   │   ├── WelcomePage.xaml
│   │   │   ├── WelcomePage.xaml.cs
│   │   │   ├── WorkspacePage.xaml
│   │   │   └── WorkspacePage.xaml.cs
│   │   ├── app.manifest
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Build.Notifications.targets
│   │   ├── FEATURE_REFINEMENTS.md
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── MarketDataCollector.Uwp.csproj
│   │   └── Package.appxmanifest
│   └── MarketDataCollector.Wpf/
│       ├── Contracts/
│       │   ├── IConnectionService.cs
│       │   └── INavigationService.cs
│       ├── Services/
│       │   ├── AdminMaintenanceService.cs
│       │   ├── AdvancedAnalyticsService.cs
│       │   ├── ArchiveHealthService.cs
│       │   ├── BackgroundTaskSchedulerService.cs
│       │   ├── ConfigService.cs
│       │   ├── ConnectionService.cs
│       │   ├── FirstRunService.cs
│       │   ├── HttpClientFactoryProvider.cs
│       │   ├── IBackgroundTaskSchedulerService.cs
│       │   ├── IConfigService.cs
│       │   ├── IKeyboardShortcutService.cs
│       │   ├── ILoggingService.cs
│       │   ├── IMessagingService.cs
│       │   ├── INotificationService.cs
│       │   ├── IOfflineTrackingPersistenceService.cs
│       │   ├── IPendingOperationsQueueService.cs
│       │   ├── IThemeService.cs
│       │   ├── KeyboardShortcutService.cs
│       │   ├── LoggingService.cs
│       │   ├── MessagingService.cs
│       │   ├── NavigationService.cs
│       │   ├── NotificationService.cs
│       │   ├── OfflineTrackingPersistenceService.cs
│       │   ├── PendingOperationsQueueService.cs
│       │   ├── SchemaService.cs
│       │   ├── StatusService.cs
│       │   ├── ThemeService.cs
│       │   └── WatchlistService.cs
│       ├── Styles/
│       │   ├── Animations.xaml
│       │   ├── AppStyles.xaml
│       │   └── IconResources.xaml
│       ├── ViewModels/
│       │   └── BindableBase.cs
│       ├── Views/
│       │   ├── ActivityLogPage.xaml
│       │   ├── ActivityLogPage.xaml.cs
│       │   ├── AdminMaintenancePage.xaml
│       │   ├── AdminMaintenancePage.xaml.cs
│       │   ├── AdvancedAnalyticsPage.xaml
│       │   ├── AdvancedAnalyticsPage.xaml.cs
│       │   ├── AnalysisExportPage.xaml
│       │   ├── AnalysisExportPage.xaml.cs
│       │   ├── AnalysisExportWizardPage.xaml
│       │   ├── AnalysisExportWizardPage.xaml.cs
│       │   ├── ArchiveHealthPage.xaml
│       │   ├── ArchiveHealthPage.xaml.cs
│       │   ├── BackfillPage.xaml
│       │   ├── BackfillPage.xaml.cs
│       │   ├── ChartingPage.xaml
│       │   ├── ChartingPage.xaml.cs
│       │   ├── CollectionSessionPage.xaml
│       │   ├── CollectionSessionPage.xaml.cs
│       │   ├── DashboardPage.xaml
│       │   ├── DashboardPage.xaml.cs
│       │   ├── DataBrowserPage.xaml
│       │   ├── DataBrowserPage.xaml.cs
│       │   ├── DataCalendarPage.xaml
│       │   ├── DataCalendarPage.xaml.cs
│       │   ├── DataExportPage.xaml
│       │   ├── DataExportPage.xaml.cs
│       │   ├── DataQualityPage.xaml
│       │   ├── DataQualityPage.xaml.cs
│       │   ├── DataSamplingPage.xaml
│       │   ├── DataSamplingPage.xaml.cs
│       │   ├── DataSourcesPage.xaml
│       │   ├── DataSourcesPage.xaml.cs
│       │   ├── DiagnosticsPage.xaml
│       │   ├── DiagnosticsPage.xaml.cs
│       │   ├── EventReplayPage.xaml
│       │   ├── EventReplayPage.xaml.cs
│       │   ├── ExportPresetsPage.xaml
│       │   ├── ExportPresetsPage.xaml.cs
│       │   ├── HelpPage.xaml
│       │   ├── HelpPage.xaml.cs
│       │   ├── IndexSubscriptionPage.xaml
│       │   ├── IndexSubscriptionPage.xaml.cs
│       │   ├── KeyboardShortcutsPage.xaml
│       │   ├── KeyboardShortcutsPage.xaml.cs
│       │   ├── LeanIntegrationPage.xaml
│       │   ├── LeanIntegrationPage.xaml.cs
│       │   ├── LiveDataViewerPage.xaml
│       │   ├── LiveDataViewerPage.xaml.cs
│       │   ├── MainPage.xaml
│       │   ├── MainPage.xaml.cs
│       │   ├── MessagingHubPage.xaml
│       │   ├── MessagingHubPage.xaml.cs
│       │   ├── NotificationCenterPage.xaml
│       │   ├── NotificationCenterPage.xaml.cs
│       │   ├── OrderBookPage.xaml
│       │   ├── OrderBookPage.xaml.cs
│       │   ├── PackageManagerPage.xaml
│       │   ├── PackageManagerPage.xaml.cs
│       │   ├── Pages.cs
│       │   ├── PortfolioImportPage.xaml
│       │   ├── PortfolioImportPage.xaml.cs
│       │   ├── ProviderHealthPage.xaml
│       │   ├── ProviderHealthPage.xaml.cs
│       │   ├── ProviderPage.xaml
│       │   ├── ProviderPage.xaml.cs
│       │   ├── RetentionAssurancePage.xaml
│       │   ├── RetentionAssurancePage.xaml.cs
│       │   ├── ScheduleManagerPage.xaml
│       │   ├── ScheduleManagerPage.xaml.cs
│       │   ├── ServiceManagerPage.xaml
│       │   ├── ServiceManagerPage.xaml.cs
│       │   ├── SettingsPage.xaml
│       │   ├── SettingsPage.xaml.cs
│       │   ├── SetupWizardPage.xaml
│       │   ├── SetupWizardPage.xaml.cs
│       │   ├── StorageOptimizationPage.xaml
│       │   ├── StorageOptimizationPage.xaml.cs
│       │   ├── StoragePage.xaml
│       │   ├── StoragePage.xaml.cs
│       │   ├── SymbolMappingPage.xaml
│       │   ├── SymbolMappingPage.xaml.cs
│       │   ├── SymbolsPage.xaml
│       │   ├── SymbolsPage.xaml.cs
│       │   ├── SymbolStoragePage.xaml
│       │   ├── SymbolStoragePage.xaml.cs
│       │   ├── SystemHealthPage.xaml
│       │   ├── SystemHealthPage.xaml.cs
│       │   ├── TimeSeriesAlignmentPage.xaml
│       │   ├── TimeSeriesAlignmentPage.xaml.cs
│       │   ├── TradingHoursPage.xaml
│       │   ├── TradingHoursPage.xaml.cs
│       │   ├── WatchlistPage.xaml
│       │   ├── WatchlistPage.xaml.cs
│       │   ├── WelcomePage.xaml
│       │   ├── WelcomePage.xaml.cs
│       │   ├── WorkspacePage.xaml
│       │   └── WorkspacePage.xaml.cs
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       ├── MarketDataCollector.Wpf.csproj
│       └── README.md
├── tests/  # Test projects
│   ├── MarketDataCollector.FSharp.Tests/
│   │   ├── CalculationTests.fs
│   │   ├── DomainTests.fs
│   │   ├── MarketDataCollector.FSharp.Tests.fsproj
│   │   ├── PipelineTests.fs
│   │   ├── Program.fs
│   │   └── ValidationTests.fs
│   ├── MarketDataCollector.Tests/
│   │   ├── Application/
│   │   │   ├── Backfill/
│   │   │   │   ...
│   │   │   ├── Config/
│   │   │   │   ...
│   │   │   ├── Credentials/
│   │   │   │   ...
│   │   │   ├── Indicators/
│   │   │   │   ...
│   │   │   ├── Monitoring/
│   │   │   │   ...
│   │   │   ├── Pipeline/
│   │   │   │   ...
│   │   │   └── Services/
│   │   │       ...
│   │   ├── Domain/
│   │   │   ├── Collectors/
│   │   │   │   ...
│   │   │   └── Models/
│   │   │       ...
│   │   ├── Infrastructure/
│   │   │   ├── Providers/
│   │   │   │   ...
│   │   │   ├── Resilience/
│   │   │   │   ...
│   │   │   └── Shared/
│   │   │       ...
│   │   ├── Integration/
│   │   │   ├── ConnectionRetryIntegrationTests.cs
│   │   │   └── UwpCoreIntegrationTests.cs
│   │   ├── Serialization/
│   │   │   └── HighPerformanceJsonTests.cs
│   │   ├── Storage/
│   │   │   ├── AnalysisExportServiceTests.cs
│   │   │   ├── FilePermissionsServiceTests.cs
│   │   │   ├── JsonlBatchWriteTests.cs
│   │   │   ├── MemoryMappedJsonlReaderTests.cs
│   │   │   ├── PortableDataPackagerTests.cs
│   │   │   ├── StorageCatalogServiceTests.cs
│   │   │   ├── StorageOptionsDefaultsTests.cs
│   │   │   └── SymbolRegistryServiceTests.cs
│   │   ├── SymbolSearch/
│   │   │   ├── OpenFigiClientTests.cs
│   │   │   └── SymbolSearchServiceTests.cs
│   │   ├── TestHelpers/
│   │   │   └── TestMarketEventPublisher.cs
│   │   └── MarketDataCollector.Tests.csproj
│   └── coverlet.runsettings
├── .gitignore
├── .globalconfig
├── CLAUDE.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── LICENSE
├── Makefile
├── MarketDataCollector.sln
├── package-lock.json
├── package.json
├── PR_SUMMARY.md
├── README.md
├── UI_IMPROVEMENTS_SUMMARY.md
└── VISUAL_CODE_EXAMPLES.md
```

## CI/CD Workflow

**GitHub Actions:** 21 workflows in `.github/workflows/`

Key workflows include:
- `test-matrix.yml` - Multi-platform test matrix
- `pr-checks.yml` - PR validation checks
- `security.yml` - Security scanning (CodeQL, Trivy)
- `docker.yml` - Docker image building
- `release.yml` - Release automation

The main CI pipeline runs on pushes to `main` and pull requests:

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
