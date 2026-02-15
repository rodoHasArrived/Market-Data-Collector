# CLAUDE.md - AI Assistant Guide for Market Data Collector

This document provides essential context for AI assistants (Claude, Copilot, etc.) working with the Market Data Collector codebase.

## Project Overview

Market Data Collector is a high-performance, cross-platform market data collection system built on **.NET 9.0** using **C# 13** and **F# 8.0**. It captures real-time and historical market microstructure data from multiple providers and persists it for downstream research, backtesting, and algorithmic trading.

**Version:** 1.6.1 | **Status:** Development / Pilot Ready | **Files:** 807 source files

### Key Capabilities
- Real-time streaming from Interactive Brokers, Alpaca, NYSE, Polygon, StockSharp (90+ data sources)
- Historical backfill from 10+ providers with automatic fallback chain
- Symbol search from 5 providers (Alpaca, Finnhub, Polygon, OpenFIGI, StockSharp)
- Comprehensive data quality monitoring with SLA enforcement
- Archival-first storage with Write-Ahead Logging (WAL) and tiered storage
- Portable data packaging for sharing and archival
- Web dashboard and WPF desktop app (Windows)
- QuantConnect Lean Engine integration for backtesting
- Scheduled maintenance and archive management

### Project Statistics
| Metric | Count |
|--------|-------|
| Total Source Files | 767 |
| C# Files | 755 |
| F# Files | 12 |
| Test Files | 135 |
| Documentation Files | 87 |
| Main Projects | 14 (+ 4 test + 1 benchmark) |
| Provider Implementations | 5 streaming, 10 historical |
| Symbol Search Providers | 5 |
| CI/CD Workflows | 17 |
| Makefile Targets | 67 |

---

## Quick Commands

```bash
# Build the project (from repo root)
dotnet build -c Release

# Run tests
dotnet test tests/MarketDataCollector.Tests

# Run F# tests
dotnet test tests/MarketDataCollector.FSharp.Tests

# Run WPF desktop service tests (Windows only)
dotnet test tests/MarketDataCollector.Wpf.Tests

# Run desktop UI service tests (Windows only)
dotnet test tests/MarketDataCollector.Ui.Tests

# Run all desktop tests
make test-desktop-services

# Run with web dashboard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --ui --http-port 8080

# Run benchmarks
dotnet run --project benchmarks/MarketDataCollector.Benchmarks -c Release

# Using Makefile (from repo root)
make build                   # Build the project
make test                    # Run tests
make run-ui                  # Run with web dashboard
make docker                  # Build and run Docker container
make docs                    # Generate documentation
make help                    # Show all available commands

# Desktop Development (via Makefile)
make desktop-dev-bootstrap   # Validate desktop development environment
make build-wpf               # Build WPF desktop app (Windows only)
make test-desktop-services   # Run desktop-focused tests

# Diagnostics (via Makefile)
make doctor      # Run full diagnostic check
make diagnose    # Build diagnostics
make metrics     # Show build metrics

# First-Time Setup (Auto-Configuration)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --wizard           # Interactive configuration wizard
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --auto-config     # Quick auto-configuration from env vars
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --detect-providers # Show available providers
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --validate-credentials # Validate API credentials

# Dry-Run Mode (validation without starting)
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --dry-run         # Full validation
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --dry-run --offline  # Skip connectivity checks

# Deployment Modes
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode web        # Web dashboard mode
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode headless   # Headless/service mode
dotnet run --project src/MarketDataCollector/MarketDataCollector.csproj -- --mode desktop    # Desktop UI mode
```

---

## AI Error Prevention (Read Before Editing)

To reduce repeated mistakes across agents, always review and update:

- `docs/ai/ai-known-errors.md` — canonical log of recurring agent mistakes, root causes, and prevention checks.
- `.github/workflows/documentation.yml (AI Known Errors Intake job)` — automation that ingests labeled GitHub issues into the known-error registry via PR.

### Required workflow for AI agents

1. **Before making changes**: scan `docs/ai/ai-known-errors.md` and apply listed prevention checks.
2. **After fixing a bug caused by agent error**: add a new entry with:
   - symptoms
   - root cause
   - prevention checklist
   - verification command(s)
3. **Before opening PR**: confirm your change does not repeat any open/known pattern in that file.

If no similar issue exists, create a concise new entry so future agents can avoid repeating it.

If the issue is tracked on GitHub, label it `ai-known-error` so the intake workflow can propose an update to `docs/ai/ai-known-errors.md`.

---

## Command-Line Reference

### Symbol Management
```bash
--symbols                    # Show all symbols (monitored + archived)
--symbols-monitored          # List symbols configured for monitoring
--symbols-archived           # List symbols with archived data
--symbols-add SPY,AAPL       # Add symbols to configuration
--symbols-remove TSLA        # Remove symbols from configuration
--symbol-status SPY          # Detailed status for a symbol
--no-trades                  # Don't subscribe to trade data
--no-depth                   # Don't subscribe to depth/L2 data
--depth-levels 10            # Number of depth levels to capture
```

### Configuration & Validation
```bash
--quick-check                # Fast configuration health check
--test-connectivity          # Test connectivity to all providers
--show-config                # Display current configuration
--error-codes                # Show error code reference guide
--check-schemas              # Check stored data schema compatibility
--validate-schemas           # Run schema check during startup
--strict-schemas             # Exit if schema incompatibilities found
--watch-config               # Enable hot-reload of configuration
```

### Data Packaging
```bash
--package                    # Create a portable data package
--import-package pkg.zip     # Import a package into storage
--list-package pkg.zip       # List package contents
--validate-package pkg.zip   # Validate package integrity
--package-symbols SPY,AAPL   # Symbols to include
--package-from 2024-01-01    # Start date
--package-to 2024-12-31      # End date
--package-format zip         # Format: zip, tar.gz
```

### Backfill Operations
```bash
--backfill                   # Run historical data backfill
--backfill-provider stooq    # Provider to use
--backfill-symbols SPY,AAPL  # Symbols to backfill
--backfill-from 2024-01-01   # Start date
--backfill-to 2024-01-05     # End date
```

---

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
│   ├── instructions/
│   │   ├── docs.instructions.md
│   │   └── dotnet-tests.instructions.md
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
│   │   ├── AI_SYNC_FIX_SUMMARY.md
│   │   ├── benchmark.yml
│   │   ├── build-observability.yml
│   │   ├── code-quality.yml
│   │   ├── copilot-setup-steps.yml
│   │   ├── desktop-builds.yml
│   │   ├── docker.yml
│   │   ├── documentation.yml
│   │   ├── dotnet-desktop.yml
│   │   ├── labeling.yml
│   │   ├── nightly.yml
│   │   ├── pr-checks.yml
│   │   ├── prompt-generation.yml
│   │   ├── README.md
│   │   ├── release.yml
│   │   ├── reusable-dotnet-build.yml
│   │   ├── scheduled-maintenance.yml
│   │   ├── security.yml
│   │   ├── SKIPPED_JOBS_EXPLAINED.md
│   │   ├── stale.yml
│   │   ├── test-matrix.yml
│   │   ├── TESTING_AI_SYNC.md
│   │   ├── ticker-data-collection.yml
│   │   ├── update-diagrams.yml
│   │   ├── update-uml-diagrams.yml
│   │   └── validate-workflows.yml
│   ├── copilot-instructions.md
│   ├── CS0101_FIX_SUMMARY.md
│   ├── dependabot.yml
│   ├── labeler.yml
│   ├── labels.yml
│   ├── markdown-link-check-config.json
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── pull_request_template_desktop.md
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
│   ├── rules/
│   │   └── doc-rules.yaml
│   └── scripts/  # Automation scripts
│       ├── docs/  # Documentation
│       │   ├── add-todos.py
│       │   ├── create-todo-issues.py
│       │   ├── generate-changelog.py
│       │   ├── generate-coverage.py
│       │   ├── generate-dependency-graph.py
│       │   ├── generate-health-dashboard.py
│       │   ├── generate-metrics-dashboard.py
│       │   ├── generate-prompts.py
│       │   ├── generate-structure-docs.py
│       │   ├── README.md
│       │   ├── repair-links.py
│       │   ├── rules-engine.py
│       │   ├── run-docs-automation.py
│       │   ├── scan-todos.py
│       │   ├── sync-readme-badges.py
│       │   ├── test-scripts.py
│       │   ├── update-claude-md.py
│       │   ├── validate-api-docs.py
│       │   └── validate-examples.py
│       ├── hooks/
│       │   ├── install-hooks.sh
│       │   └── pre-commit
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
│   │   ├── alert-rules.yml
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
│   │   ├── 006-domain-events-polymorphic-payload.md
│   │   ├── 007-write-ahead-log-durability.md
│   │   ├── 008-multi-format-composite-storage.md
│   │   ├── 009-fsharp-interop.md
│   │   ├── 010-httpclient-factory.md
│   │   ├── 011-centralized-configuration-and-credentials.md
│   │   ├── 012-monitoring-and-alerting-pipeline.md
│   │   ├── 013-bounded-channel-policy.md
│   │   ├── 014-json-source-generators.md
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
│   │   ├── ai-known-errors.md
│   │   └── README.md
│   ├── architecture/
│   │   ├── c4-context.png
│   │   ├── c4-context.puml
│   │   ├── c4-diagrams.md
│   │   ├── crystallized-storage-format.md
│   │   ├── desktop-layers.md
│   │   ├── domains.md
│   │   ├── layer-boundaries.md
│   │   ├── overview.md
│   │   ├── provider-management.md
│   │   ├── storage-design.md
│   │   ├── ui-redesign.md
│   │   └── why-this-architecture.md
│   ├── archived/
│   │   ├── 2026-02_PR_SUMMARY.md
│   │   ├── 2026-02_UI_IMPROVEMENTS_SUMMARY.md
│   │   ├── 2026-02_VISUAL_CODE_EXAMPLES.md
│   │   ├── ARTIFACT_ACTIONS_DOWNGRADE.md
│   │   ├── CHANGES_SUMMARY.md
│   │   ├── consolidation.md
│   │   ├── desktop-ui-alternatives-evaluation.md
│   │   ├── DUPLICATE_CODE_ANALYSIS.md
│   │   ├── IMPROVEMENTS_2026-02.md
│   │   ├── INDEX.md
│   │   ├── README.md
│   │   ├── REDESIGN_IMPROVEMENTS.md
│   │   ├── REPOSITORY_REORGANIZATION_PLAN.md
│   │   ├── STRUCTURAL_IMPROVEMENTS_2026-02.md
│   │   ├── uwp-development-roadmap.md
│   │   └── uwp-release-checklist.md
│   ├── audits/
│   │   ├── CLEANUP_OPPORTUNITIES.md
│   │   ├── CLEANUP_SUMMARY.md
│   │   ├── H3_DEBUG_CODE_ANALYSIS.md
│   │   ├── README.md
│   │   └── UWP_COMPREHENSIVE_AUDIT.md
│   ├── development/
│   │   ├── policies/
│   │   │   └── desktop-support-policy.md
│   │   ├── build-observability.md
│   │   ├── central-package-management.md
│   │   ├── desktop-app-xaml-compiler-errors.md
│   │   ├── desktop-dev-workflow.md
│   │   ├── desktop-devex-high-value-improvements.md
│   │   ├── desktop-improvements-executive-summary.md
│   │   ├── desktop-improvements-quick-reference.md
│   │   ├── desktop-platform-improvements-implementation-guide.md
│   │   ├── desktop-testing-guide.md
│   │   ├── documentation-contribution-guide.md
│   │   ├── github-actions-summary.md
│   │   ├── github-actions-testing.md
│   │   ├── provider-implementation.md
│   │   ├── refactor-map.md
│   │   ├── repository-cleanup-action-plan.md
│   │   ├── repository-organization-guide.md
│   │   ├── ROADMAP_UPDATE_SUMMARY.md
│   │   ├── ui-fixture-mode-guide.md
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
│   │   ├── data-flow.svg
│   │   ├── deployment-options.dot
│   │   ├── deployment-options.png
│   │   ├── deployment-options.svg
│   │   ├── event-pipeline-sequence.dot
│   │   ├── event-pipeline-sequence.png
│   │   ├── event-pipeline-sequence.svg
│   │   ├── onboarding-flow.dot
│   │   ├── onboarding-flow.png
│   │   ├── onboarding-flow.svg
│   │   ├── project-dependencies.dot
│   │   ├── project-dependencies.png
│   │   ├── project-dependencies.svg
│   │   ├── provider-architecture.dot
│   │   ├── provider-architecture.png
│   │   ├── provider-architecture.svg
│   │   ├── README.md
│   │   ├── resilience-patterns.dot
│   │   ├── resilience-patterns.png
│   │   ├── resilience-patterns.svg
│   │   ├── storage-architecture.dot
│   │   ├── storage-architecture.png
│   │   └── storage-architecture.svg
│   ├── docfx/
│   │   ├── docfx.json
│   │   └── README.md
│   ├── evaluations/
│   │   ├── data-quality-monitoring-evaluation.md
│   │   ├── desktop-end-user-improvements-shortlist.md
│   │   ├── desktop-end-user-improvements.md
│   │   ├── historical-data-providers-evaluation.md
│   │   ├── ingestion-orchestration-evaluation.md
│   │   ├── operational-readiness-evaluation.md
│   │   ├── realtime-streaming-architecture-evaluation.md
│   │   ├── storage-architecture-evaluation.md
│   │   └── windows-desktop-provider-configurability-assessment.md
│   ├── generated/
│   │   ├── adr-index.md
│   │   ├── configuration-schema.md
│   │   ├── project-context.md
│   │   ├── provider-registry.md
│   │   ├── README.md
│   │   ├── repository-structure.md
│   │   └── workflows-overview.md
│   ├── getting-started/
│   │   └── README.md
│   ├── guides/
│   │   ├── adding-custom-rules.md
│   │   ├── documentation-automation.md
│   │   └── expanding-scripts.md
│   ├── integrations/
│   │   ├── fsharp-integration.md
│   │   ├── language-strategy.md
│   │   └── lean-integration.md
│   ├── operations/
│   │   ├── deployment.md
│   │   ├── high-availability.md
│   │   ├── msix-packaging.md
│   │   ├── operator-runbook.md
│   │   ├── performance-tuning.md
│   │   └── portable-data-packager.md
│   ├── providers/
│   │   ├── alpaca-setup.md
│   │   ├── backfill-guide.md
│   │   ├── data-sources.md
│   │   ├── interactive-brokers-free-equity-reference.md
│   │   ├── interactive-brokers-setup.md
│   │   └── provider-comparison.md
│   ├── reference/
│   │   ├── api-reference.md
│   │   ├── data-dictionary.md
│   │   ├── data-uniformity.md
│   │   ├── design-review-memo.md
│   │   └── open-source-references.md
│   ├── security/
│   │   └── known-vulnerabilities.md
│   ├── status/
│   │   ├── CHANGELOG.md
│   │   ├── health-dashboard.md
│   │   ├── IMPROVEMENTS.md
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
│   ├── DEPENDENCIES.md
│   ├── HELP.md
│   ├── README.md
│   └── toc.yml
├── scripts/  # Automation scripts
│   └── dev/
│       ├── desktop-dev.ps1
│       └── diagnose-uwp-xaml.ps1
├── src/  # Source code
│   ├── MarketDataCollector/
│   │   ├── Integrations/
│   │   │   └── Lean/
│   │   │       ...
│   │   ├── Tools/
│   │   │   └── DataValidator.cs
│   │   ├── wwwroot/
│   │   │   └── templates/
│   │   │       ...
│   │   ├── app.manifest
│   │   ├── GlobalUsings.cs
│   │   ├── MarketDataCollector.csproj
│   │   ├── Program.cs
│   │   ├── runtimeconfig.template.json
│   │   └── UiServer.cs
│   ├── MarketDataCollector.Application/
│   │   ├── Backfill/
│   │   │   ├── BackfillRequest.cs
│   │   │   ├── BackfillResult.cs
│   │   │   ├── BackfillStatusStore.cs
│   │   │   └── HistoricalBackfillService.cs
│   │   ├── Commands/
│   │   │   ├── CliArguments.cs
│   │   │   ├── CommandDispatcher.cs
│   │   │   ├── ConfigCommands.cs
│   │   │   ├── DiagnosticsCommands.cs
│   │   │   ├── DryRunCommand.cs
│   │   │   ├── HelpCommand.cs
│   │   │   ├── ICliCommand.cs
│   │   │   ├── PackageCommands.cs
│   │   │   ├── SchemaCheckCommand.cs
│   │   │   ├── SelfTestCommand.cs
│   │   │   ├── SymbolCommands.cs
│   │   │   └── ValidateConfigCommand.cs
│   │   ├── Composition/
│   │   │   ├── HostAdapters.cs
│   │   │   ├── HostStartup.cs
│   │   │   └── ServiceCompositionRoot.cs
│   │   ├── Config/
│   │   │   ├── Credentials/
│   │   │   │   ...
│   │   │   ├── AppConfigJsonOptions.cs
│   │   │   ├── ConfigDtoMapper.cs
│   │   │   ├── ConfigurationPipeline.cs
│   │   │   ├── ConfigValidationHelper.cs
│   │   │   ├── ConfigValidatorCli.cs
│   │   │   ├── ConfigWatcher.cs
│   │   │   ├── DeploymentContext.cs
│   │   │   ├── IConfigValidator.cs
│   │   │   ├── SensitiveValueMasker.cs
│   │   │   └── StorageConfigExtensions.cs
│   │   ├── Credentials/
│   │   │   └── ICredentialStore.cs
│   │   ├── Filters/
│   │   │   └── MarketEventFilter.cs
│   │   ├── Http/
│   │   │   ├── Endpoints/
│   │   │   │   ...
│   │   │   ├── BackfillCoordinator.cs
│   │   │   ├── ConfigStore.cs
│   │   │   ├── HtmlTemplateLoader.cs
│   │   │   └── HtmlTemplates.cs
│   │   ├── Indicators/
│   │   │   └── TechnicalIndicatorService.cs
│   │   ├── Monitoring/
│   │   │   ├── Core/
│   │   │   │   ...
│   │   │   ├── DataQuality/
│   │   │   │   ...
│   │   │   ├── BackpressureAlertService.cs
│   │   │   ├── BadTickFilter.cs
│   │   │   ├── ConnectionHealthMonitor.cs
│   │   │   ├── ConnectionStatusWebhook.cs
│   │   │   ├── DetailedHealthCheck.cs
│   │   │   ├── ErrorRingBuffer.cs
│   │   │   ├── IEventMetrics.cs
│   │   │   ├── Metrics.cs
│   │   │   ├── PrometheusMetrics.cs
│   │   │   ├── ProviderDegradationScorer.cs
│   │   │   ├── ProviderLatencyService.cs
│   │   │   ├── ProviderMetricsStatus.cs
│   │   │   ├── SchemaValidationService.cs
│   │   │   ├── SpreadMonitor.cs
│   │   │   ├── StatusHttpServer.cs
│   │   │   ├── StatusSnapshot.cs
│   │   │   ├── StatusWriter.cs
│   │   │   ├── SystemHealthChecker.cs
│   │   │   ├── TickSizeValidator.cs
│   │   │   └── TimestampMonotonicityChecker.cs
│   │   ├── Pipeline/
│   │   │   ├── DroppedEventAuditTrail.cs
│   │   │   └── EventPipeline.cs
│   │   ├── Results/
│   │   │   ├── ErrorCode.cs
│   │   │   ├── OperationError.cs
│   │   │   └── Result.cs
│   │   ├── Scheduling/
│   │   │   ├── BackfillExecutionLog.cs
│   │   │   ├── BackfillSchedule.cs
│   │   │   ├── BackfillScheduleManager.cs
│   │   │   ├── IOperationalScheduler.cs
│   │   │   ├── OperationalScheduler.cs
│   │   │   └── ScheduledBackfillService.cs
│   │   ├── Services/
│   │   │   ├── ApiDocumentationService.cs
│   │   │   ├── AutoConfigurationService.cs
│   │   │   ├── CanonicalSymbolRegistry.cs
│   │   │   ├── CliModeResolver.cs
│   │   │   ├── ConfigEnvironmentOverride.cs
│   │   │   ├── ConfigTemplateGenerator.cs
│   │   │   ├── ConfigurationService.cs
│   │   │   ├── ConfigurationServiceCredentialAdapter.cs
│   │   │   ├── ConfigurationWizard.cs
│   │   │   ├── ConnectivityTestService.cs
│   │   │   ├── CredentialValidationService.cs
│   │   │   ├── DailySummaryWebhook.cs
│   │   │   ├── DiagnosticBundleService.cs
│   │   │   ├── DryRunService.cs
│   │   │   ├── ErrorTracker.cs
│   │   │   ├── FriendlyErrorFormatter.cs
│   │   │   ├── GracefulShutdownHandler.cs
│   │   │   ├── GracefulShutdownService.cs
│   │   │   ├── HistoricalDataQueryService.cs
│   │   │   ├── PreflightChecker.cs
│   │   │   ├── ProgressDisplayService.cs
│   │   │   ├── SampleDataGenerator.cs
│   │   │   ├── ServiceRegistry.cs
│   │   │   ├── StartupSummary.cs
│   │   │   └── TradingCalendar.cs
│   │   ├── Subscriptions/
│   │   │   ├── Services/
│   │   │   │   ...
│   │   │   └── SubscriptionOrchestrator.cs
│   │   ├── Testing/
│   │   │   └── DepthBufferSelfTests.cs
│   │   ├── Tracing/
│   │   │   ├── OpenTelemetrySetup.cs
│   │   │   └── TracedEventMetrics.cs
│   │   ├── GlobalUsings.cs
│   │   └── MarketDataCollector.Application.csproj
│   ├── MarketDataCollector.Contracts/
│   │   ├── Api/
│   │   │   ├── BackfillApiModels.cs
│   │   │   ├── ClientModels.cs
│   │   │   ├── ErrorResponse.cs
│   │   │   ├── LiveDataModels.cs
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
│   │   │   ├── ICanonicalSymbolRegistry.cs
│   │   │   ├── StorageCatalog.cs
│   │   │   └── SymbolRegistry.cs
│   │   ├── Configuration/
│   │   │   ├── AppConfigDto.cs
│   │   │   ├── DerivativesConfigDto.cs
│   │   │   └── SymbolConfig.cs
│   │   ├── Credentials/
│   │   │   ├── CredentialModels.cs
│   │   │   └── ISecretProvider.cs
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
│   ├── MarketDataCollector.Core/
│   │   ├── Config/
│   │   │   ├── AlpacaOptions.cs
│   │   │   ├── AppConfig.cs
│   │   │   ├── BackfillConfig.cs
│   │   │   ├── DataSourceConfig.cs
│   │   │   ├── DataSourceKind.cs
│   │   │   ├── DataSourceKindConverter.cs
│   │   │   ├── DerivativesConfig.cs
│   │   │   ├── IConfigurationProvider.cs
│   │   │   ├── StockSharpConfig.cs
│   │   │   └── ValidatedConfig.cs
│   │   ├── Exceptions/
│   │   │   ├── ConfigurationException.cs
│   │   │   ├── ConnectionException.cs
│   │   │   ├── DataProviderException.cs
│   │   │   ├── MarketDataCollectorException.cs
│   │   │   ├── OperationTimeoutException.cs
│   │   │   ├── RateLimitException.cs
│   │   │   ├── SequenceValidationException.cs
│   │   │   ├── StorageException.cs
│   │   │   └── ValidationException.cs
│   │   ├── Logging/
│   │   │   └── LoggingSetup.cs
│   │   ├── Monitoring/
│   │   │   ├── Core/
│   │   │   │   ...
│   │   │   ├── EventSchemaValidator.cs
│   │   │   ├── IConnectionHealthMonitor.cs
│   │   │   └── MigrationDiagnostics.cs
│   │   ├── Performance/
│   │   │   └── Performance/
│   │   │       ...
│   │   ├── Pipeline/
│   │   │   └── EventPipelinePolicy.cs
│   │   ├── Scheduling/
│   │   │   └── CronExpressionParser.cs
│   │   ├── Serialization/
│   │   │   └── MarketDataJsonContext.cs
│   │   ├── Services/
│   │   │   └── IFlushable.cs
│   │   ├── Subscriptions/
│   │   │   └── Models/
│   │   │       ...
│   │   ├── GlobalUsings.cs
│   │   └── MarketDataCollector.Core.csproj
│   ├── MarketDataCollector.Domain/
│   │   ├── Collectors/
│   │   │   ├── IQuoteStateStore.cs
│   │   │   ├── MarketDepthCollector.cs
│   │   │   ├── QuoteCollector.cs
│   │   │   ├── SymbolSubscriptionTracker.cs
│   │   │   └── TradeDataCollector.cs
│   │   ├── Events/
│   │   │   ├── Publishers/
│   │   │   │   ...
│   │   │   ├── IMarketEventPublisher.cs
│   │   │   ├── MarketEvent.cs
│   │   │   └── MarketEventPayload.cs
│   │   ├── Models/
│   │   │   ├── AggregateBar.cs
│   │   │   ├── MarketDepthUpdate.cs
│   │   │   └── MarketTradeUpdate.cs
│   │   ├── BannedReferences.txt
│   │   ├── GlobalUsings.cs
│   │   └── MarketDataCollector.Domain.csproj
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
│   ├── MarketDataCollector.Infrastructure/
│   │   ├── Contracts/
│   │   │   ├── ContractVerificationExtensions.cs
│   │   │   └── ContractVerificationService.cs
│   │   ├── DataSources/
│   │   │   ├── DataSourceBase.cs
│   │   │   └── DataSourceConfiguration.cs
│   │   ├── Http/
│   │   │   ├── HttpClientConfiguration.cs
│   │   │   └── SharedResiliencePolicies.cs
│   │   ├── Providers/
│   │   │   ├── Backfill/
│   │   │   │   ...
│   │   │   ├── Core/
│   │   │   │   ...
│   │   │   ├── Historical/
│   │   │   │   ...
│   │   │   ├── Streaming/
│   │   │   │   ...
│   │   │   └── SymbolSearch/
│   │   │       ...
│   │   ├── Resilience/
│   │   │   ├── HttpResiliencePolicy.cs
│   │   │   ├── WebSocketConnectionConfig.cs
│   │   │   ├── WebSocketConnectionManager.cs
│   │   │   └── WebSocketResiliencePolicy.cs
│   │   ├── Shared/
│   │   │   ├── ISymbolStateStore.cs
│   │   │   ├── SubscriptionManager.cs
│   │   │   ├── TaskSafetyExtensions.cs
│   │   │   ├── WebSocketProviderBase.cs
│   │   │   └── WebSocketReconnectionHelper.cs
│   │   ├── Utilities/
│   │   │   ├── HttpResponseHandler.cs
│   │   │   ├── JsonElementExtensions.cs
│   │   │   └── SymbolNormalization.cs
│   │   ├── GlobalUsings.cs
│   │   ├── MarketDataCollector.Infrastructure.csproj
│   │   └── NoOpMarketDataClient.cs
│   ├── MarketDataCollector.ProviderSdk/
│   │   ├── CredentialValidator.cs
│   │   ├── DataSourceAttribute.cs
│   │   ├── DataSourceRegistry.cs
│   │   ├── HistoricalDataCapabilities.cs
│   │   ├── IDataSource.cs
│   │   ├── IHistoricalBarWriter.cs
│   │   ├── IHistoricalDataSource.cs
│   │   ├── IMarketDataClient.cs
│   │   ├── ImplementsAdrAttribute.cs
│   │   ├── IProviderMetadata.cs
│   │   ├── IProviderModule.cs
│   │   ├── IRealtimeDataSource.cs
│   │   ├── MarketDataCollector.ProviderSdk.csproj
│   │   └── ProviderHttpUtilities.cs
│   ├── MarketDataCollector.Storage/
│   │   ├── Archival/
│   │   │   ├── ArchivalStorageService.cs
│   │   │   ├── AtomicFileWriter.cs
│   │   │   ├── CompressionProfileManager.cs
│   │   │   ├── SchemaVersionManager.cs
│   │   │   └── WriteAheadLog.cs
│   │   ├── Export/
│   │   │   ├── AnalysisExportService.cs
│   │   │   ├── AnalysisExportService.Formats.cs
│   │   │   ├── AnalysisExportService.IO.cs
│   │   │   ├── AnalysisQualityReport.cs
│   │   │   ├── ExportProfile.cs
│   │   │   ├── ExportRequest.cs
│   │   │   └── ExportResult.cs
│   │   ├── Interfaces/
│   │   │   ├── ISourceRegistry.cs
│   │   │   ├── IStorageCatalogService.cs
│   │   │   ├── IStoragePolicy.cs
│   │   │   ├── IStorageSink.cs
│   │   │   └── ISymbolRegistryService.cs
│   │   ├── Maintenance/
│   │   │   ├── ArchiveMaintenanceModels.cs
│   │   │   ├── ArchiveMaintenanceScheduleManager.cs
│   │   │   ├── IArchiveMaintenanceScheduleManager.cs
│   │   │   ├── IArchiveMaintenanceService.cs
│   │   │   ├── IMaintenanceExecutionHistory.cs
│   │   │   └── ScheduledArchiveMaintenanceService.cs
│   │   ├── Packaging/
│   │   │   ├── PackageManifest.cs
│   │   │   ├── PackageOptions.cs
│   │   │   ├── PackageResult.cs
│   │   │   ├── PortableDataPackager.Creation.cs
│   │   │   ├── PortableDataPackager.cs
│   │   │   ├── PortableDataPackager.Scripts.cs
│   │   │   └── PortableDataPackager.Validation.cs
│   │   ├── Policies/
│   │   │   └── JsonlStoragePolicy.cs
│   │   ├── Replay/
│   │   │   ├── JsonlReplayer.cs
│   │   │   └── MemoryMappedJsonlReader.cs
│   │   ├── Services/
│   │   │   ├── DataLineageService.cs
│   │   │   ├── DataQualityScoringService.cs
│   │   │   ├── DataQualityService.cs
│   │   │   ├── EventBuffer.cs
│   │   │   ├── FileMaintenanceService.cs
│   │   │   ├── FilePermissionsService.cs
│   │   │   ├── LifecyclePolicyEngine.cs
│   │   │   ├── MaintenanceScheduler.cs
│   │   │   ├── MetadataTagService.cs
│   │   │   ├── QuotaEnforcementService.cs
│   │   │   ├── SourceRegistry.cs
│   │   │   ├── StorageCatalogService.cs
│   │   │   ├── StorageChecksumService.cs
│   │   │   ├── StorageSearchService.cs
│   │   │   ├── SymbolRegistryService.cs
│   │   │   └── TierMigrationService.cs
│   │   ├── Sinks/
│   │   │   ├── CompositeSink.cs
│   │   │   ├── JsonlStorageSink.cs
│   │   │   └── ParquetStorageSink.cs
│   │   ├── GlobalUsings.cs
│   │   ├── MarketDataCollector.Storage.csproj
│   │   ├── StorageOptions.cs
│   │   └── StorageProfiles.cs
│   ├── MarketDataCollector.Ui/
│   │   ├── wwwroot/
│   │   │   └── static/
│   │   │       ...
│   │   ├── app.manifest
│   │   ├── MarketDataCollector.Ui.csproj
│   │   └── Program.cs
│   ├── MarketDataCollector.Ui.Services/
│   │   ├── Collections/
│   │   │   ├── BoundedObservableCollection.cs
│   │   │   └── CircularBuffer.cs
│   │   ├── Contracts/
│   │   │   ├── ConnectionTypes.cs
│   │   │   ├── IAdminMaintenanceService.cs
│   │   │   ├── IArchiveHealthService.cs
│   │   │   ├── IBackgroundTaskSchedulerService.cs
│   │   │   ├── IConfigService.cs
│   │   │   ├── ICredentialService.cs
│   │   │   ├── ILoggingService.cs
│   │   │   ├── IMessagingService.cs
│   │   │   ├── INotificationService.cs
│   │   │   ├── IOfflineTrackingPersistenceService.cs
│   │   │   ├── IPendingOperationsQueueService.cs
│   │   │   ├── ISchemaService.cs
│   │   │   ├── IStatusService.cs
│   │   │   ├── IThemeService.cs
│   │   │   ├── IWatchlistService.cs
│   │   │   └── NavigationTypes.cs
│   │   ├── Services/
│   │   │   ├── ActivityFeedService.cs
│   │   │   ├── AdminMaintenanceModels.cs
│   │   │   ├── AdminMaintenanceServiceBase.cs
│   │   │   ├── AdvancedAnalyticsModels.cs
│   │   │   ├── AdvancedAnalyticsServiceBase.cs
│   │   │   ├── AnalysisExportWizardService.cs
│   │   │   ├── ApiClientService.cs
│   │   │   ├── ArchiveBrowserService.cs
│   │   │   ├── ArchiveHealthService.cs
│   │   │   ├── BackfillApiService.cs
│   │   │   ├── BackfillCheckpointService.cs
│   │   │   ├── BackfillService.cs
│   │   │   ├── BatchExportSchedulerService.cs
│   │   │   ├── ChartingService.cs
│   │   │   ├── CollectionSessionService.cs
│   │   │   ├── ColorPalette.cs
│   │   │   ├── ConfigService.cs
│   │   │   ├── ConnectionServiceBase.cs
│   │   │   ├── CredentialService.cs
│   │   │   ├── DataCalendarService.cs
│   │   │   ├── DataCompletenessService.cs
│   │   │   ├── DataSamplingService.cs
│   │   │   ├── DesktopJsonOptions.cs
│   │   │   ├── DiagnosticsService.cs
│   │   │   ├── ErrorHandlingService.cs
│   │   │   ├── ErrorMessages.cs
│   │   │   ├── EventReplayService.cs
│   │   │   ├── ExportPresetServiceBase.cs
│   │   │   ├── FixtureDataService.cs
│   │   │   ├── FormValidationRules.cs
│   │   │   ├── HttpClientConfiguration.cs
│   │   │   ├── InfoBarConstants.cs
│   │   │   ├── IntegrityEventsService.cs
│   │   │   ├── LeanIntegrationService.cs
│   │   │   ├── LiveDataService.cs
│   │   │   ├── LoggingService.cs
│   │   │   ├── ManifestService.cs
│   │   │   ├── NavigationServiceBase.cs
│   │   │   ├── NotificationService.cs
│   │   │   ├── OAuthRefreshService.cs
│   │   │   ├── OperationResult.cs
│   │   │   ├── OrderBookVisualizationService.cs
│   │   │   ├── PortablePackagerService.cs
│   │   │   ├── PortfolioImportService.cs
│   │   │   ├── ProviderHealthService.cs
│   │   │   ├── ProviderManagementService.cs
│   │   │   ├── RetentionAssuranceModels.cs
│   │   │   ├── ScheduledMaintenanceService.cs
│   │   │   ├── ScheduleManagerService.cs
│   │   │   ├── SchemaService.cs
│   │   │   ├── SchemaServiceBase.cs
│   │   │   ├── SearchService.cs
│   │   │   ├── SetupWizardService.cs
│   │   │   ├── SmartRecommendationsService.cs
│   │   │   ├── StorageAnalyticsService.cs
│   │   │   ├── StorageModels.cs
│   │   │   ├── StorageOptimizationAdvisorService.cs
│   │   │   ├── StorageServiceBase.cs
│   │   │   ├── SymbolGroupService.cs
│   │   │   ├── SymbolManagementService.cs
│   │   │   ├── SymbolMappingService.cs
│   │   │   ├── SystemHealthService.cs
│   │   │   ├── ThemeServiceBase.cs
│   │   │   ├── TimeSeriesAlignmentService.cs
│   │   │   ├── TooltipContent.cs
│   │   │   ├── WatchlistService.cs
│   │   │   └── WorkspaceModels.cs
│   │   ├── GlobalUsings.cs
│   │   └── MarketDataCollector.Ui.Services.csproj
│   ├── MarketDataCollector.Ui.Shared/
│   │   ├── Endpoints/
│   │   │   ├── AdminEndpoints.cs
│   │   │   ├── AlignmentEndpoints.cs
│   │   │   ├── AnalyticsEndpoints.cs
│   │   │   ├── ApiKeyMiddleware.cs
│   │   │   ├── BackfillEndpoints.cs
│   │   │   ├── BackfillScheduleEndpoints.cs
│   │   │   ├── ConfigEndpoints.cs
│   │   │   ├── CronEndpoints.cs
│   │   │   ├── DiagnosticsEndpoints.cs
│   │   │   ├── ExportEndpoints.cs
│   │   │   ├── FailoverEndpoints.cs
│   │   │   ├── HealthEndpoints.cs
│   │   │   ├── HistoricalEndpoints.cs
│   │   │   ├── IBEndpoints.cs
│   │   │   ├── IndexEndpoints.cs
│   │   │   ├── LeanEndpoints.cs
│   │   │   ├── LiveDataEndpoints.cs
│   │   │   ├── MaintenanceScheduleEndpoints.cs
│   │   │   ├── MessagingEndpoints.cs
│   │   │   ├── PathValidation.cs
│   │   │   ├── ProviderEndpoints.cs
│   │   │   ├── ProviderExtendedEndpoints.cs
│   │   │   ├── QualityDropsEndpoints.cs
│   │   │   ├── ReplayEndpoints.cs
│   │   │   ├── SamplingEndpoints.cs
│   │   │   ├── StatusEndpoints.cs
│   │   │   ├── StorageEndpoints.cs
│   │   │   ├── StorageQualityEndpoints.cs
│   │   │   ├── StubEndpoints.cs
│   │   │   ├── SubscriptionEndpoints.cs
│   │   │   ├── SymbolEndpoints.cs
│   │   │   ├── SymbolMappingEndpoints.cs
│   │   │   └── UiEndpoints.cs
│   │   ├── Services/
│   │   │   ├── BackfillCoordinator.cs
│   │   │   └── ConfigStore.cs
│   │   ├── DtoExtensions.cs
│   │   ├── HtmlTemplateGenerator.cs
│   │   ├── HtmlTemplateGenerator.Scripts.cs
│   │   ├── HtmlTemplateGenerator.Styles.cs
│   │   └── MarketDataCollector.Ui.Shared.csproj
│   └── MarketDataCollector.Wpf/
│       ├── Contracts/
│       │   ├── IConnectionService.cs
│       │   └── INavigationService.cs
│       ├── Models/
│       │   ├── AppConfig.cs
│       │   └── StorageDisplayModels.cs
│       ├── Services/
│       │   ├── AdminMaintenanceService.cs
│       │   ├── AdvancedAnalyticsService.cs
│       │   ├── ArchiveHealthService.cs
│       │   ├── BackendServiceManager.cs
│       │   ├── BackfillApiService.cs
│       │   ├── BackgroundTaskSchedulerService.cs
│       │   ├── BrushRegistry.cs
│       │   ├── ConfigService.cs
│       │   ├── ConnectionService.cs
│       │   ├── ContextMenuService.cs
│       │   ├── CredentialService.cs
│       │   ├── ExportFormat.cs
│       │   ├── ExportPresetService.cs
│       │   ├── FirstRunService.cs
│       │   ├── FormValidationService.cs
│       │   ├── InfoBarService.cs
│       │   ├── KeyboardShortcutService.cs
│       │   ├── LoggingService.cs
│       │   ├── MessagingService.cs
│       │   ├── NavigationService.cs
│       │   ├── NotificationService.cs
│       │   ├── OfflineTrackingPersistenceService.cs
│       │   ├── PendingOperationsQueueService.cs
│       │   ├── RetentionAssuranceService.cs
│       │   ├── SchemaService.cs
│       │   ├── StatusService.cs
│       │   ├── StorageService.cs
│       │   ├── ThemeService.cs
│       │   ├── TooltipService.cs
│       │   ├── WatchlistService.cs
│       │   ├── WorkspaceService.cs
│       │   ├── WpfAnalysisExportService.cs
│       │   └── WpfDataQualityService.cs
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
│       ├── GlobalUsings.cs
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
│   │   └── ValidationTests.fs
│   ├── MarketDataCollector.Tests/
│   │   ├── Application/
│   │   │   ├── Backfill/
│   │   │   │   ...
│   │   │   ├── Commands/
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
│   │   │   ├── DataSources/
│   │   │   │   ...
│   │   │   ├── Providers/
│   │   │   │   ...
│   │   │   ├── Resilience/
│   │   │   │   ...
│   │   │   └── Shared/
│   │   │       ...
│   │   ├── Integration/
│   │   │   ├── EndpointTests/
│   │   │   │   ...
│   │   │   ├── ConfigurableTickerDataCollectionTests.cs
│   │   │   ├── ConnectionRetryIntegrationTests.cs
│   │   │   ├── EndpointStubDetectionTests.cs
│   │   │   ├── FixtureProviderTests.cs
│   │   │   └── YahooFinancePcgPreferredIntegrationTests.cs
│   │   ├── ProviderSdk/
│   │   │   ├── CredentialValidatorTests.cs
│   │   │   ├── DataSourceAttributeTests.cs
│   │   │   ├── DataSourceRegistryTests.cs
│   │   │   └── ExceptionTypeTests.cs
│   │   ├── Serialization/
│   │   │   └── HighPerformanceJsonTests.cs
│   │   ├── Storage/
│   │   │   ├── AnalysisExportServiceTests.cs
│   │   │   ├── AtomicFileWriterTests.cs
│   │   │   ├── CanonicalSymbolRegistryTests.cs
│   │   │   ├── CompositeSinkTests.cs
│   │   │   ├── DataLineageServiceTests.cs
│   │   │   ├── DataQualityScoringServiceTests.cs
│   │   │   ├── DataValidatorTests.cs
│   │   │   ├── FilePermissionsServiceTests.cs
│   │   │   ├── JsonlBatchWriteTests.cs
│   │   │   ├── LifecyclePolicyEngineTests.cs
│   │   │   ├── MemoryMappedJsonlReaderTests.cs
│   │   │   ├── MetadataTagServiceTests.cs
│   │   │   ├── PortableDataPackagerTests.cs
│   │   │   ├── QuotaEnforcementServiceTests.cs
│   │   │   ├── StorageCatalogServiceTests.cs
│   │   │   ├── StorageChecksumServiceTests.cs
│   │   │   ├── StorageOptionsDefaultsTests.cs
│   │   │   ├── SymbolRegistryServiceTests.cs
│   │   │   └── WriteAheadLogTests.cs
│   │   ├── SymbolSearch/
│   │   │   ├── OpenFigiClientTests.cs
│   │   │   └── SymbolSearchServiceTests.cs
│   │   ├── TestHelpers/
│   │   │   └── TestMarketEventPublisher.cs
│   │   ├── GlobalUsings.cs
│   │   └── MarketDataCollector.Tests.csproj
│   ├── MarketDataCollector.Ui.Tests/
│   │   ├── Collections/
│   │   │   ├── BoundedObservableCollectionTests.cs
│   │   │   └── CircularBufferTests.cs
│   │   ├── Services/
│   │   │   ├── ApiClientServiceTests.cs
│   │   │   ├── BackfillServiceTests.cs
│   │   │   ├── ChartingServiceTests.cs
│   │   │   ├── FixtureDataServiceTests.cs
│   │   │   ├── FormValidationServiceTests.cs
│   │   │   ├── LeanIntegrationServiceTests.cs
│   │   │   ├── OrderBookVisualizationServiceTests.cs
│   │   │   ├── PortfolioImportServiceTests.cs
│   │   │   ├── SchemaServiceTests.cs
│   │   │   ├── SystemHealthServiceTests.cs
│   │   │   ├── TimeSeriesAlignmentServiceTests.cs
│   │   │   └── WatchlistServiceTests.cs
│   │   ├── MarketDataCollector.Ui.Tests.csproj
│   │   └── README.md
│   ├── MarketDataCollector.Wpf.Tests/
│   │   ├── Services/
│   │   │   ├── ConfigServiceTests.cs
│   │   │   ├── ConnectionServiceTests.cs
│   │   │   ├── NavigationServiceTests.cs
│   │   │   ├── StatusServiceTests.cs
│   │   │   └── WpfDataQualityServiceTests.cs
│   │   ├── GlobalUsings.cs
│   │   └── MarketDataCollector.Wpf.Tests.csproj
│   └── coverlet.runsettings
├── .gitignore
├── .globalconfig
├── .markdownlint.json
├── CLAUDE.md
├── CONFIG_CONSOLIDATION_REPORT.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── LICENSE
├── Makefile
├── MarketDataCollector.sln
├── package-lock.json
├── package.json
└── README.md
```

## Critical Rules

When contributing to this project, **always follow these rules**:

### Must-Follow Rules
- **ALWAYS** use `CancellationToken` on async methods
- **NEVER** store secrets in code or config files - use environment variables
- **ALWAYS** use structured logging with semantic parameters: `_logger.LogInformation("Received {Count} bars for {Symbol}", count, symbol)`
- **PREFER** `IAsyncEnumerable<T>` for streaming data over collections
- **ALWAYS** mark classes as `sealed` unless designed for inheritance
- **NEVER** log sensitive data (API keys, credentials)
- **NEVER** use `Task.Run` for I/O-bound operations (wastes thread pool)
- **NEVER** block async code with `.Result` or `.Wait()` (causes deadlocks)
- **ALWAYS** add `[ImplementsAdr]` attributes when implementing ADR contracts

### Architecture Principles
1. **Provider Independence** - All providers implement `IMarketDataClient` interface
2. **No Vendor Lock-in** - Provider-agnostic interfaces with failover
3. **Security First** - Environment variables for credentials
4. **Observability** - Structured logging, Prometheus metrics, health endpoints
5. **Simplicity** - Monolithic core with optional UI projects
6. **ADR Compliance** - Follow Architecture Decision Records in `docs/adr/`

---

## Data Providers

### Streaming Providers (IMarketDataClient)

| Provider | Class | Trades | Quotes | Depth | Features |
|----------|-------|--------|--------|-------|----------|
| Alpaca | `AlpacaMarketDataClient` | Yes | Yes | No | WebSocket streaming |
| Polygon | `PolygonMarketDataClient` | Yes | Yes | Yes | Circuit breaker, retry |
| Interactive Brokers | `IBMarketDataClient` | Yes | Yes | Yes | TWS/Gateway, conditional |
| StockSharp | `StockSharpMarketDataClient` | Yes | Yes | Yes | 90+ data sources |
| NYSE | `NYSEDataSource` | Yes | Yes | L1/L2 | Hybrid streaming + historical |
| Failover | `FailoverAwareMarketDataClient` | - | - | - | Automatic provider switching |
| IB Simulation | `IBSimulationClient` | - | - | - | IB testing without live connection |
| NoOp | `NoOpMarketDataClient` | - | - | - | Placeholder |

### Historical Providers (IHistoricalDataProvider)

| Provider | Free Tier | Data Types | Rate Limits |
|----------|-----------|------------|-------------|
| Alpaca | Yes (with account) | Bars, trades, quotes | 200/min |
| Polygon | Limited | Bars, trades, quotes, aggregates | Varies |
| Tiingo | Yes | Daily bars | 500/hour |
| Yahoo Finance | Yes | Daily bars | Unofficial |
| Stooq | Yes | Daily bars | Low |
| StockSharp | Yes (with account) | Various | Varies |
| Finnhub | Yes | Daily bars | 60/min |
| Alpha Vantage | Yes | Daily bars | 5/min |
| Nasdaq Data Link | Limited | Various | Varies |
| Interactive Brokers | Yes (with account) | All types | IB pacing rules |

**CompositeHistoricalDataProvider** provides automatic multi-provider routing with:
- Priority-based fallback chain
- Rate limit tracking
- Provider health monitoring
- Symbol resolution across providers

### Symbol Search Providers (ISymbolSearchProvider)

| Provider | Class | Exchanges | Rate Limit |
|----------|-------|-----------|------------|
| Alpaca | `AlpacaSymbolSearchProviderRefactored` | US, Crypto | 200/min |
| Finnhub | `FinnhubSymbolSearchProviderRefactored` | US, International | 60/min |
| Polygon | `PolygonSymbolSearchProvider` | US | 5/min (free) |
| OpenFIGI | `OpenFigiClient` | Global (ID mapping) | - |
| StockSharp | `StockSharpSymbolSearchProvider` | Multi-exchange | - |

---

## Key Interfaces

### IMarketDataClient (Streaming)
Location: `src/MarketDataCollector/Infrastructure/IMarketDataClient.cs`

```csharp
[ImplementsAdr("ADR-001", "Core streaming data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IMarketDataClient : IAsyncDisposable
{
    bool IsEnabled { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    int SubscribeMarketDepth(SymbolConfig cfg);
    void UnsubscribeMarketDepth(int subscriptionId);
    int SubscribeTrades(SymbolConfig cfg);
    void UnsubscribeTrades(int subscriptionId);
}
```

### IHistoricalDataProvider (Backfill)
Location: `src/MarketDataCollector/Infrastructure/Providers/Backfill/IHistoricalDataProvider.cs`

```csharp
[ImplementsAdr("ADR-001", "Core historical data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IHistoricalDataProvider
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }

    // Capabilities
    HistoricalDataCapabilities Capabilities { get; }
    int Priority { get; }

    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    // Extended methods for tick data, quotes, trades, auctions
}
```

---

## HTTP API Reference

The application exposes a REST API when running with `--ui` or `--mode web`.

**Implementation Note:** The codebase declares ~269 route constants in `UiApiRoutes.cs`, but approximately 136 endpoints have full handler implementations. Core endpoints (status, health, config, backfill) are fully functional. Some advanced endpoints may return stub responses or 501 Not Implemented.

### Core Endpoints
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/` | GET | HTML dashboard (auto-refreshing) |
| `/api/status` | GET | Full status with metrics |
| `/api/health` | GET | Comprehensive health status |
| `/healthz`, `/readyz`, `/livez` | GET | Kubernetes health probes |
| `/api/metrics` | GET | Prometheus metrics |
| `/api/errors` | GET | Error log with filtering |
| `/api/backpressure` | GET | Backpressure status |

### Configuration API (`/api/config/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/config` | GET | Full configuration |
| `/api/config/data-source` | POST | Update active data source |
| `/api/config/symbols` | POST | Add/update symbol |
| `/api/config/symbols/{symbol}` | DELETE | Remove symbol |
| `/api/config/data-sources` | GET/POST | Manage data sources |
| `/api/config/data-sources/{id}/toggle` | POST | Toggle source enabled |

### Provider API (`/api/providers/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/providers/status` | GET | All provider status |
| `/api/providers/metrics` | GET | Provider metrics |
| `/api/providers/latency` | GET | Latency metrics |
| `/api/providers/catalog` | GET | Provider catalog with metadata |
| `/api/providers/comparison` | GET | Feature comparison |
| `/api/connections` | GET | Connection health |

### Failover API (`/api/failover/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/failover/config` | GET/POST | Failover configuration |
| `/api/failover/rules` | GET/POST | Failover rules |
| `/api/failover/health` | GET | Provider health status |
| `/api/failover/force/{ruleId}` | POST | Force failover |

### Backfill API (`/api/backfill/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/backfill/providers` | GET | Available providers |
| `/api/backfill/status` | GET | Last backfill status |
| `/api/backfill/run` | POST | Execute backfill |
| `/api/backfill/run/preview` | POST | Preview backfill |
| `/api/backfill/schedules` | GET/POST | Manage schedules |
| `/api/backfill/schedules/{id}/trigger` | POST | Trigger schedule |
| `/api/backfill/executions` | GET | Execution history |
| `/api/backfill/gap-fill` | POST | Immediate gap fill |
| `/api/backfill/statistics` | GET | Backfill statistics |

### Data Quality API (`/api/quality/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/quality/dashboard` | GET | Quality dashboard |
| `/api/quality/metrics` | GET | Real-time metrics |
| `/api/quality/completeness` | GET | Completeness scores |
| `/api/quality/gaps` | GET | Gap analysis |
| `/api/quality/gaps/{symbol}` | GET | Symbol gaps |
| `/api/quality/errors` | GET | Sequence errors |
| `/api/quality/anomalies` | GET | Detected anomalies |
| `/api/quality/latency` | GET | Latency distributions |
| `/api/quality/comparison/{symbol}` | GET | Cross-provider comparison |
| `/api/quality/health` | GET | Quality health status |
| `/api/quality/reports/daily` | GET | Daily quality report |

### SLA Monitoring API (`/api/sla/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/sla/status` | GET | SLA compliance status |
| `/api/sla/violations` | GET | SLA violations |
| `/api/sla/health` | GET | SLA health |
| `/api/sla/metrics` | GET | SLA metrics |

### Maintenance API (`/api/maintenance/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/maintenance/schedules` | GET/POST | Manage schedules |
| `/api/maintenance/schedules/{id}/trigger` | POST | Trigger maintenance |
| `/api/maintenance/executions` | GET | Execution history |
| `/api/maintenance/execute` | POST | Immediate execution |
| `/api/maintenance/task-types` | GET | Available task types |

### Packaging API (`/api/packaging/`)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/packaging/create` | POST | Create package |
| `/api/packaging/import` | POST | Import package |
| `/api/packaging/validate` | POST | Validate package |
| `/api/packaging/list` | GET | List packages |
| `/api/packaging/download/{fileName}` | GET | Download package |

---

## Data Quality Monitoring

The system includes comprehensive data quality monitoring in `Application/Monitoring/DataQuality/`:

### Quality Services
| Service | Purpose |
|---------|---------|
| `DataQualityMonitoringService` | Orchestrates all quality checks |
| `CompletenessScoreCalculator` | Calculates data completeness scores |
| `GapAnalyzer` | Detects and analyzes data gaps |
| `SequenceErrorTracker` | Tracks sequence/integrity errors |
| `AnomalyDetector` | Detects data anomalies |
| `LatencyHistogram` | Tracks latency distribution |
| `CrossProviderComparisonService` | Compares data across providers |
| `PriceContinuityChecker` | Checks price continuity |
| `DataFreshnessSlaMonitor` | Monitors data freshness SLA |
| `DataQualityReportGenerator` | Generates quality reports |

### Quality Metrics
- **Completeness Score** - Percentage of expected data received
- **Gap Analysis** - Missing data periods with duration
- **Sequence Errors** - Out-of-order or duplicate events
- **Anomaly Detection** - Unusual price/volume patterns
- **Latency Distribution** - End-to-end latency percentiles
- **Cross-Provider Comparison** - Data consistency across providers
- **SLA Compliance** - Data freshness within thresholds

---

## Application Services

### Core Services
| Service | Location | Purpose |
|---------|----------|---------|
| `ConfigurationService` | `Application/Config/` | Configuration loading with self-healing |
| `ConfigurationWizard` | `Application/Services/` | Interactive configuration setup |
| `AutoConfigurationService` | `Application/Services/` | Auto-config from environment |
| `PreflightChecker` | `Application/Services/` | Pre-startup validation |
| `GracefulShutdownService` | `Application/Services/` | Graceful shutdown coordination |
| `DryRunService` | `Application/Services/` | Dry-run validation mode |
| `DiagnosticBundleService` | `Application/Services/` | Comprehensive diagnostics |
| `TradingCalendar` | `Application/Services/` | Market hours and holidays |

### Monitoring Services
| Service | Location | Purpose |
|---------|----------|---------|
| `ConnectionHealthMonitor` | `Application/Monitoring/` | Provider connection health |
| `ProviderLatencyService` | `Application/Monitoring/` | Latency tracking |
| `SpreadMonitor` | `Application/Monitoring/` | Bid-ask spread monitoring |
| `BackpressureAlertService` | `Application/Monitoring/` | Backpressure alerts |
| `ErrorTracker` | `Application/Monitoring/` | Error categorization |
| `PrometheusMetrics` | `Application/Monitoring/` | Metrics export |

### Storage Services
| Service | Location | Purpose |
|---------|----------|---------|
| `WriteAheadLog` | `Storage/Archival/` | WAL for durability |
| `PortableDataPackager` | `Storage/Packaging/` | Data package creation |
| `TierMigrationService` | `Storage/Services/` | Hot/warm/cold tier migration |
| `ScheduledArchiveMaintenanceService` | `Storage/Maintenance/` | Scheduled maintenance |
| `HistoricalDataQueryService` | `Application/Services/` | Query stored data |

---

## Architecture Decision Records (ADRs)

ADRs document significant architectural decisions. Located in `docs/adr/`:

| ADR | Title | Key Points |
|-----|-------|------------|
| ADR-001 | Provider Abstraction | Interface contracts for data providers |
| ADR-002 | Tiered Storage | Hot/cold storage architecture |
| ADR-003 | Microservices Decomposition | Rejected in favor of monolith |
| ADR-004 | Async Streaming Patterns | CancellationToken, IAsyncEnumerable |
| ADR-005 | Attribute-Based Discovery | `[DataSource]`, `[ImplementsAdr]` attributes |
| ADR-006 | Domain Events Polymorphic Payload | Sealed record wrapper with static factories |
| ADR-007 | WAL + Event Pipeline Durability | Write-Ahead Log for crash-safe persistence |
| ADR-008 | Multi-Format Composite Storage | JSONL + Parquet simultaneous writes |
| ADR-009 | F# Type-Safe Domain | F# discriminated unions with C# interop |
| ADR-010 | HttpClient Factory | HttpClientFactory lifecycle management |
| ADR-011 | Centralized Configuration | Configuration and credentials management |
| ADR-012 | Monitoring & Alerting Pipeline | Unified health checks and alerts |
| ADR-013 | Bounded Channel Pipeline Policy | Consistent backpressure with static presets |
| ADR-014 | JSON Source Generators | High-performance serialization without reflection |

Use `[ImplementsAdr("ADR-XXX", "reason")]` attribute when implementing ADR contracts.

---

## Testing

### Test Framework Stack
- **xUnit** - Test framework
- **FluentAssertions** - Fluent assertions
- **Moq** / **NSubstitute** - Mocking frameworks
- **coverlet** - Code coverage

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/MarketDataCollector.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run F# tests
dotnet test tests/MarketDataCollector.FSharp.Tests
```

### Test Organization (98 test files total)
| Directory | Purpose | Files |
|-----------|---------|-------|
| `tests/MarketDataCollector.Tests/Application/Backfill/` | Backfill provider tests | 7 |
| `tests/MarketDataCollector.Tests/Application/Commands/` | Command tests | 2 |
| `tests/MarketDataCollector.Tests/Application/Config/` | Configuration tests | 2 |
| `tests/MarketDataCollector.Tests/Application/Credentials/` | Credential provider tests | 3 |
| `tests/MarketDataCollector.Tests/Application/Indicators/` | Technical indicator tests | 1 |
| `tests/MarketDataCollector.Tests/Application/Monitoring/` | Monitoring/quality tests | 9 |
| `tests/MarketDataCollector.Tests/Application/Pipeline/` | Event pipeline tests | 6 |
| `tests/MarketDataCollector.Tests/Application/Services/` | Application service tests | 4 |
| `tests/MarketDataCollector.Tests/Domain/Collectors/` | Domain collector tests | 4 |
| `tests/MarketDataCollector.Tests/Domain/Models/` | Domain model tests | 11 |
| `tests/MarketDataCollector.Tests/Infrastructure/DataSources/` | Data source tests | 1 |
| `tests/MarketDataCollector.Tests/Infrastructure/Providers/` | Provider-specific tests | 4 |
| `tests/MarketDataCollector.Tests/Infrastructure/Resilience/` | Resilience tests | 2 |
| `tests/MarketDataCollector.Tests/Infrastructure/Shared/` | Shared infra tests | 2 |
| `tests/MarketDataCollector.Tests/Integration/` | End-to-end tests | 3 |
| `tests/MarketDataCollector.Tests/Serialization/` | JSON serialization tests | 1 |
| `tests/MarketDataCollector.Tests/Storage/` | Storage and archival tests | 12 |
| `tests/MarketDataCollector.Tests/SymbolSearch/` | Symbol resolution tests | 2 |
| `tests/MarketDataCollector.FSharp.Tests/` | F# domain tests | 5 |
| `tests/MarketDataCollector.Wpf.Tests/Services/` | WPF desktop service tests | 4 |
| `tests/MarketDataCollector.Ui.Tests/Services/` | Desktop UI service tests | 6 |
| `tests/MarketDataCollector.Ui.Tests/Collections/` | UI collection tests | 2 |

**WPF Desktop Service Tests (58 tests, Windows only):**
- `NavigationServiceTests` - 14 tests for page navigation, registration, history
- `ConfigServiceTests` - 13 tests for configuration management, validation
- `StatusServiceTests` - 13 tests for status tracking, events, HTTP client mocking
- `ConnectionServiceTests` - 18 tests for connection management, monitoring, auto-reconnect

**Desktop UI Service Tests (71 tests, Windows only):**
- `ApiClientServiceTests` - API client configuration and HTTP interactions
- `BackfillServiceTests` - Backfill coordination and scheduling
- `FixtureDataServiceTests` - Mock data generation for offline development
- `FormValidationServiceTests` - Form validation rules and helpers
- `SystemHealthServiceTests` - System health monitoring
- `WatchlistServiceTests` - Watchlist management
- `BoundedObservableCollectionTests` - Bounded collection behavior
- `CircularBufferTests` - Circular buffer operations

### Benchmarks
Located in `benchmarks/MarketDataCollector.Benchmarks/` using BenchmarkDotNet.

---

## Configuration

### Environment Variables
API credentials should be set via environment variables:
```bash
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key
export NYSE__APIKEY=your-api-key
export POLYGON__APIKEY=your-api-key
export TIINGO__TOKEN=your-token
export FINNHUB__TOKEN=your-token
export ALPHAVANTAGE__APIKEY=your-api-key
```

Note: Use double underscore (`__`) for nested configuration (maps to `Alpaca:KeyId`).

### appsettings.json
Configuration file should be copied from template:
```bash
cp config/appsettings.sample.json config/appsettings.json
```

Key sections:
- `DataSource` - Active provider (IB, Alpaca, NYSE, Polygon)
- `Symbols` - List of symbols to subscribe
- `Storage` - File organization, retention, compression, tiers
- `Backfill` - Historical data settings, provider priority
- `DataQuality` - Quality monitoring thresholds
- `Sla` - Data freshness SLA configuration
- `Maintenance` - Archive maintenance schedules

---

## Coding Conventions

### Logging
Use structured logging with semantic parameters:
```csharp
// Good
_logger.LogInformation("Received {Count} bars for {Symbol}", bars.Count, symbol);

// Bad - don't interpolate
_logger.LogInformation($"Received {bars.Count} bars for {symbol}");
```

### Error Handling
- Log all errors with context (symbol, provider, timestamp)
- Use exponential backoff for retries
- Throw `ArgumentException` for bad inputs
- Throw `InvalidOperationException` for state errors
- Use `Result<T, TError>` in F# code

#### Custom Exception Types (in `Application/Exceptions/`)
| Exception | Purpose |
|-----------|---------|
| `ConfigurationException` | Invalid configuration |
| `ConnectionException` | Connection failures |
| `DataProviderException` | Provider errors |
| `RateLimitException` | Rate limit exceeded |
| `SequenceValidationException` | Data sequence issues |
| `StorageException` | Storage/persistence errors |
| `ValidationException` | Data validation failures |
| `OperationTimeoutException` | Operation timeouts |

### Naming Conventions
- Async methods end with `Async`
- Cancellation token parameter named `ct` or `cancellationToken`
- Private fields prefixed with `_`
- Interfaces prefixed with `I`

### Performance
- Avoid allocations in hot paths
- Use object pooling for frequently created objects
- Prefer `Span<T>` and `Memory<T>` for buffer operations
- Use `System.Threading.Channels` for producer-consumer patterns
- Consider lock-free alternatives for high-contention scenarios

---

## Domain Models

### Core Event Types
- `Trade` - Tick-by-tick trade prints with sequence validation
- `LOBSnapshot` - Full L2 order book state
- `BboQuote` - Best bid/offer with spread and mid-price
- `OrderFlowStatistics` - Rolling VWAP, imbalance, volume splits
- `IntegrityEvent` - Sequence anomalies (gaps, out-of-order)
- `HistoricalBar` - OHLCV bars from backfill providers

### Key Classes
| Class | Location | Purpose |
|-------|----------|---------|
| `TradeDataCollector` | `Domain/Collectors/` | Tick-by-tick trade processing |
| `MarketDepthCollector` | `Domain/Collectors/` | L2 order book maintenance |
| `QuoteCollector` | `Domain/Collectors/` | BBO state tracking |
| `EventPipeline` | `Application/Pipeline/` | Bounded channel event routing |
| `JsonlStorageSink` | `Storage/Sinks/` | JSONL file persistence |
| `ParquetStorageSink` | `Storage/Sinks/` | Parquet file persistence |
| `AlpacaMarketDataClient` | `Infrastructure/Providers/Alpaca/` | Alpaca WebSocket client |
| `CompositeHistoricalDataProvider` | `Infrastructure/Providers/Backfill/` | Multi-provider backfill with fallback |
| `BackfillWorkerService` | `Infrastructure/Providers/Backfill/` | Background backfill service |
| `DataQualityMonitoringService` | `Application/Monitoring/DataQuality/` | Data quality monitoring |
| `GracefulShutdownService` | `Application/Services/` | Graceful shutdown handling |
| `ConfigurationWizard` | `Application/Services/` | Interactive configuration setup |
| `TechnicalIndicatorService` | `Application/Indicators/` | Technical indicators (via Skender) |
| `WriteAheadLog` | `Storage/Archival/` | WAL for data durability |
| `PortableDataPackager` | `Storage/Packaging/` | Data package creation |
| `TradingCalendar` | `Application/Services/` | Market hours and holidays |

*All locations relative to `src/MarketDataCollector/`*

---

## Storage Architecture

### File Organization
```
data/
├── live/                    # Real-time data (hot tier)
│   ├── {provider}/
│   │   └── {date}/
│   │       ├── {symbol}_trades.jsonl.gz
│   │       └── {symbol}_quotes.jsonl.gz
├── historical/              # Backfill data
│   └── {provider}/
│       └── {date}/
│           └── {symbol}_bars.jsonl
├── _wal/                    # Write-ahead log
└── _archive/                # Compressed archives (cold tier)
    └── parquet/
```

### Naming Conventions
- **BySymbol** (default, recommended): `{root}/{symbol}/{type}/{date}.jsonl` - Organized by symbol, then data type
- **ByDate**: `{root}/{date}/{symbol}/{type}.jsonl` - Organized by date
- **ByType**: `{root}/{type}/{symbol}/{date}.jsonl` - Organized by event type
- **Flat**: `{root}/{symbol}_{type}_{date}.jsonl` - All files in root directory

### Compression Profiles
| Profile | Algorithm | Use Case |
|---------|-----------|----------|
| RealTime | LZ4 | Live streaming data |
| Standard | Gzip | General purpose |
| Archive | ZSTD-19 | Long-term storage |

### Tiered Storage
| Tier | Purpose | Retention |
|------|---------|-----------|
| Hot | Recent data, fast access | 7 days default |
| Warm | Older data, compressed | 30 days default |
| Cold | Archive, maximum compression | Indefinite |

---

## Common Tasks

### Adding a New Data Provider
1. Create client class in `src/MarketDataCollector/Infrastructure/Providers/{ProviderName}/`
2. Implement `IMarketDataClient` interface
3. Add `[DataSource("provider-name")]` attribute
4. Add `[ImplementsAdr("ADR-001", "reason")]` attribute
5. Register in DI container in `Program.cs`
6. Add configuration section in `config/appsettings.sample.json`
7. Add tests in `tests/MarketDataCollector.Tests/`

See `docs/development/provider-implementation.md` for detailed patterns.

### Adding a New Historical Provider
1. Create provider in `src/MarketDataCollector/Infrastructure/Providers/Backfill/`
2. Implement `IHistoricalDataProvider`
3. Add `[ImplementsAdr]` attributes
4. Register in `CompositeHistoricalDataProvider`
5. Add to provider priority list

### Running Backfill
```bash
# Via command line
dotnet run --project src/MarketDataCollector -- \
  --backfill --backfill-provider stooq \
  --backfill-symbols SPY,AAPL \
  --backfill-from 2024-01-01 --backfill-to 2024-01-05

# Via Makefile
make run-backfill SYMBOLS=SPY,AAPL
```

### Creating Data Packages
```bash
# Create a portable package
dotnet run --project src/MarketDataCollector -- \
  --package \
  --package-symbols SPY,AAPL \
  --package-from 2024-01-01 --package-to 2024-12-31 \
  --package-output ./packages \
  --package-name "2024-equities"

# Import a package
dotnet run --project src/MarketDataCollector -- \
  --import-package ./packages/2024-equities.zip \
  --merge
```

See `docs/operations/portable-data-packager.md` for details.

---

## CI/CD Pipelines

The project uses GitHub Actions with 17 workflows in `.github/workflows/`:

| Workflow | Purpose |
|----------|---------|
| `test-matrix.yml` | Multi-platform test matrix (Windows, Linux, macOS) |
| `code-quality.yml` | Code quality checks (formatting, analyzers) |
| `security.yml` | Security scanning (CodeQL, dependency audit) |
| `benchmark.yml` | Performance benchmarks |
| `docker.yml` | Docker image building and publishing |
| `dotnet-desktop.yml` | Desktop application builds |
| `desktop-builds.yml` | Desktop app builds (WPF) |
| `documentation.yml` | Documentation generation, AI instruction sync, TODO scanning |
| `release.yml` | Release automation |
| `pr-checks.yml` | PR validation checks |
| `labeling.yml` | PR auto-labeling |
| `nightly.yml` | Nightly builds |
| `scheduled-maintenance.yml` | Scheduled maintenance tasks |
| `stale.yml` | Stale issue management |
| `validate-workflows.yml` | Workflow validation |
| `build-observability.yml` | Build metrics collection |
| `reusable-dotnet-build.yml` | Reusable .NET build workflow |

---

## Build Requirements

- .NET 9.0 SDK
- `EnableWindowsTargeting=true` for cross-platform builds (set in `Directory.Build.props`)
- Python 3 for build tooling (`build/python/`)
- Node.js for diagram generation (optional)

---

## Central Package Management (CPM)

This repository uses **Central Package Management** to ensure consistent package versions across all projects.

### Key Rules

1. **All package versions** are defined in `Directory.Packages.props`
2. **Project files** reference packages WITHOUT version numbers
3. **Never** add `Version` attributes to `<PackageReference>` items

### Correct Usage

```xml
<!-- ✅ CORRECT - In .csproj file -->
<PackageReference Include="Serilog" />

<!-- ❌ INCORRECT - Will cause NU1008 error -->
<PackageReference Include="Serilog" Version="4.3.0" />
```

### Error NU1008

If you see this error during restore/build:
```
error NU1008: Projects that use central package version management should not define 
the version on the PackageReference items...
```

**Fix**: Remove all `Version="..."` attributes from `<PackageReference>` items in the failing project file.

### Adding New Packages

1. Add version to `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="NewPackage" Version="1.0.0" />
   ```
2. Reference in project file (no version):
   ```xml
   <PackageReference Include="NewPackage" />
   ```

See [Central Package Management Guide](docs/development/central-package-management.md) for complete documentation.

---

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad |
|--------------|--------------|
| Swallowing exceptions silently | Hides bugs, makes debugging impossible |
| Hardcoding credentials | Security risk, inflexible deployment |
| Using `Task.Run` for I/O | Wastes thread pool threads |
| Blocking async with `.Result` | Causes deadlocks |
| Creating new `HttpClient` instances | Socket exhaustion, DNS issues |
| Logging with string interpolation | Loses structured logging benefits |
| Missing CancellationToken | Prevents graceful shutdown |
| Missing `[ImplementsAdr]` attribute | Loses ADR traceability |
| Adding Version to PackageReference | Violates Central Package Management (NU1008 error) |

---

## Desktop Application Architecture

### WPF Desktop App (Recommended)

The WPF desktop application (`MarketDataCollector.Wpf`) is the recommended Windows desktop client:
- Works on Windows 7+ with standard .exe deployment
- Direct assembly references (no WinRT limitations)
- Uses standard WPF XAML with full .NET 9.0 support
- Shares UI services via `MarketDataCollector.Ui.Services` project

See `src/MarketDataCollector.Wpf/README.md` for details.

### UWP Desktop App (Removed)

The UWP desktop application (`MarketDataCollector.Uwp`) was deprecated and fully removed in Phase 6 cleanup. WPF is the sole desktop client. Historical documentation is available in `docs/archived/`.

---

## Documentation

### Core Documentation
| File | Purpose |
|------|---------|
| `docs/HELP.md` | Complete user guide with FAQ |
| `docs/getting-started/README.md` | Quick start index |
| `docs/operations/operator-runbook.md` | Production operations |
| `docs/development/provider-implementation.md` | Adding new providers |
| `docs/operations/portable-data-packager.md` | Data packaging guide |

### Architecture Documentation
| File | Purpose |
|------|---------|
| `docs/architecture/overview.md` | System architecture |
| `docs/architecture/domains.md` | Event contracts |
| `docs/architecture/storage-design.md` | Storage organization |
| `docs/architecture/why-this-architecture.md` | Design rationale |
| `docs/adr/` | Architecture Decision Records |

### Provider Documentation
| File | Purpose |
|------|---------|
| `docs/providers/backfill-guide.md` | Historical data guide |
| `docs/providers/data-sources.md` | Available data sources |
| `docs/providers/provider-comparison.md` | Feature comparison |

### Development Guides
| File | Purpose |
|------|---------|
| `docs/development/uwp-to-wpf-migration.md` | WPF desktop app migration |
| `docs/development/wpf-implementation-notes.md` | WPF implementation details |
| `docs/development/github-actions-summary.md` | CI/CD workflows |

### AI Assistant Guides
| File | Purpose |
|------|---------|
| `docs/ai/claude/CLAUDE.providers.md` | Provider implementation |
| `docs/ai/claude/CLAUDE.storage.md` | Storage system |
| `docs/ai/claude/CLAUDE.fsharp.md` | F# domain library |
| `docs/ai/claude/CLAUDE.testing.md` | Testing guide |
| `.github/agents/documentation-agent.md` | Documentation maintenance |

### Reference Materials
| File | Purpose |
|------|---------|
| `docs/reference/data-dictionary.md` | Field definitions |
| `docs/reference/data-uniformity.md` | Consistency guidelines |
| `docs/DEPENDENCIES.md` | Package documentation |

---

## Troubleshooting

### Build Issues
```bash
# Run build diagnostics
make diagnose

# Or call the buildctl CLI directly
python3 build-system/cli/buildctl.py build --project src/MarketDataCollector/MarketDataCollector.csproj --configuration Release

# Use build control CLI
make doctor

# Manual restore with diagnostics
dotnet restore /p:EnableWindowsTargeting=true -v diag
```

### Common Issues
1. **NETSDK1100 error** - Ensure `EnableWindowsTargeting=true` is set
2. **Credential errors** - Check environment variables are set
3. **Connection failures** - Verify API keys and network connectivity
4. **High memory** - Check channel capacity in `EventPipeline`
5. **Provider rate limits** - Check `ProviderRateLimitTracker` logs

See `docs/HELP.md#troubleshooting` for detailed solutions.

---

## Related Resources

- [README.md](README.md) - Project overview
- [docs/HELP.md](docs/HELP.md) - Complete user guide with FAQ
- [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) - Package documentation
- [docs/adr/](docs/adr/) - Architecture Decision Records
- [docs/ai/](docs/ai/) - Specialized AI guides
- [docs/providers/provider-comparison.md](docs/providers/provider-comparison.md) - Provider comparison
- [docs/ai/copilot/instructions.md](docs/ai/copilot/instructions.md) - Copilot instructions
- [.github/agents/documentation-agent.md](.github/agents/documentation-agent.md) - Documentation agent

---

*Last Updated: 2026-02-15*
