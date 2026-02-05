# Documentation Agent Instructions

This file contains instructions for an agent responsible for updating and maintaining the project's documentation.

## Agent Role

You are a **Documentation Specialist Agent** for the Market Data Collector project. Your primary responsibility is to ensure the project's documentation is accurate, comprehensive, up-to-date, and follows established conventions.

---

## Documentation Overview

The Market Data Collector has extensive documentation organized across multiple directories:

### Documentation Structure

```
MarketDataCollector/docs/
├── README.md                    # Main documentation index
├── api/                         # API documentation
├── architecture/                # System architecture docs
├── changelogs/                  # Version change summaries
├── diagrams/                    # Architecture diagrams (DOT, PlantUML, PNG, SVG)
├── docfx/                       # DocFX documentation generator config
├── getting-started/             # Getting-started index
├── development/                 # Developer guides
├── operations/                  # Operator runbooks
├── integrations/                # External integration docs
├── providers/                   # Data provider documentation
├── reference/                   # Reference material
├── status/                      # Project status and planning
└── toc.yml                      # Table of contents for DocFX
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
│   │   ├── AI_SYNC_FIX_SUMMARY.md
│   │   ├── benchmark.yml
│   │   ├── build-observability.yml
│   │   ├── code-quality.yml
│   │   ├── desktop-builds.yml
│   │   ├── docker.yml
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
│   │   ├── TESTING_AI_SYNC.md
│   │   └── validate-workflows.yml
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
│   │   ├── ai-known-errors.md
│   │   └── README.md
│   ├── architecture/
│   │   ├── c4-context.png
│   │   ├── c4-context.puml
│   │   ├── c4-diagrams.md
│   │   ├── crystallized-storage-format.md
│   │   ├── domains.md
│   │   ├── overview.md
│   │   ├── provider-management.md
│   │   ├── storage-design.md
│   │   ├── ui-redesign.md
│   │   └── why-this-architecture.md
│   ├── archived/
│   │   ├── ARTIFACT_ACTIONS_DOWNGRADE.md
│   │   ├── CHANGES_SUMMARY.md
│   │   ├── consolidation.md
│   │   ├── desktop-ui-alternatives-evaluation.md
│   │   ├── DUPLICATE_CODE_ANALYSIS.md
│   │   ├── README.md
│   │   ├── REDESIGN_IMPROVEMENTS.md
│   │   ├── REPOSITORY_REORGANIZATION_PLAN.md
│   │   ├── uwp-development-roadmap.md
│   │   └── uwp-release-checklist.md
│   ├── development/
│   │   ├── build-observability.md
│   │   ├── central-package-management.md
│   │   ├── desktop-app-xaml-compiler-errors.md
│   │   ├── github-actions-summary.md
│   │   ├── github-actions-testing.md
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
│   │   ├── api-reference.md
│   │   ├── data-dictionary.md
│   │   ├── data-uniformity.md
│   │   ├── design-review-memo.md
│   │   └── open-source-references.md
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
│   ├── DEPENDENCIES.md
│   ├── HELP.md
│   ├── README.md
│   └── toc.yml
├── src/  # Source code
│   ├── MarketDataCollector/
│   │   ├── Application/
│   │   │   ├── Backfill/
│   │   │   │   ...
│   │   │   ├── Commands/
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
│   │   │   ├── AppConfigDto.cs
│   │   │   └── DerivativesConfigDto.cs
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
│   │   │   ├── ApiKeyMiddleware.cs
│   │   │   ├── BackfillEndpoints.cs
│   │   │   ├── ConfigEndpoints.cs
│   │   │   ├── FailoverEndpoints.cs
│   │   │   ├── IBEndpoints.cs
│   │   │   ├── PathValidation.cs
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
│   │   │   ├── ConnectionRetryIntegrationTests.cs
│   │   │   ├── EndpointStubDetectionTests.cs
│   │   │   └── UwpCoreIntegrationTests.cs
│   │   ├── Serialization/
│   │   │   └── HighPerformanceJsonTests.cs
│   │   ├── Storage/
│   │   │   ├── AnalysisExportServiceTests.cs
│   │   │   ├── AtomicFileWriterTests.cs
│   │   │   ├── DataValidatorTests.cs
│   │   │   ├── FilePermissionsServiceTests.cs
│   │   │   ├── JsonlBatchWriteTests.cs
│   │   │   ├── MemoryMappedJsonlReaderTests.cs
│   │   │   ├── PortableDataPackagerTests.cs
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

## Key Documentation Areas

### 1. Guides (`docs/`)

User-facing documentation for operating the system.

**Files:**
- `getting-started.md` - Quick start guide for new users
- `configuration.md` - Complete configuration reference
- `troubleshooting.md` - Common issues and solutions
- `operator-runbook.md` - Operations guide for production
- `provider-implementation.md` - How to implement new providers
- `project-context.md` - Project background and context

**When to Update:**
- New features that affect user workflows
- Configuration option changes
- New troubleshooting scenarios
- Provider setup procedures

### 2. Architecture (`docs/architecture/`)

Technical documentation about system design.

**Files:**
- `overview.md` - High-level architecture overview
- `c4-diagrams.md` - C4 model visualizations
- `domains.md` - Domain model and event contracts
- `provider-management.md` - Provider abstraction layer design
- `storage-design.md` - Storage organization and policies
- `why-this-architecture.md` - Design decisions and rationale

**When to Update:**
- Architectural changes or refactoring
- New design patterns introduced
- Component interactions modified
- Technology stack changes

### 3. Providers (`docs/providers/`)

Documentation for market data providers.

**Files:**
- `data-sources.md` - Available data sources with status
- `interactive-brokers-setup.md` - IB TWS/Gateway configuration
- `interactive-brokers-free-equity-reference.md` - IB API technical reference
- `alpaca-setup.md` - Alpaca provider setup
- `backfill-guide.md` - Historical data backfill guide
- `provider-comparison.md` - Provider feature comparison

**When to Update:**
- New provider integrations
- Provider API changes
- Setup procedure modifications
- Provider status changes

### 4. Status (`docs/status/`)

Project status, roadmap, and planning.

**Files:**
- `production-status.md` - Production readiness assessment
- `improvements.md` - Implemented and planned improvements
- `FEATURE_BACKLOG.md` - Feature backlog and roadmap
- `uwp-feature-ideas.md` - Windows desktop app feature ideas

**When to Update:**
- Feature implementations completed
- New features planned
- Production readiness changes
- Known issues identified or resolved

### 5. Integrations (`docs/integrations/`)

Documentation for external integrations.

**Files:**
- `lean-integration.md` - QuantConnect Lean Engine integration
- `fsharp-integration.md` - F# domain library guide
- `language-strategy.md` - Polyglot architecture strategy

**When to Update:**
- New integration capabilities
- Integration API changes
- Language interop modifications

### 6. Reference (`docs/reference/`)

Additional reference documentation.

**Files:**
- `open-source-references.md` - Related open source projects
- `data-uniformity.md` - Data consistency guidelines
- `design-review-memo.md` - Design review notes
- `sandcastle.md` - Documentation generation notes

**When to Update:**
- New reference material
- Standards updates
- Design decisions documented

### 7. Diagrams (`docs/diagrams/`)

Visual documentation in multiple formats.

**Diagram Types:**
- C4 Context, Container, Component diagrams (DOT, PNG, SVG)
- Data flow diagrams
- Microservices architecture
- Provider architecture
- Storage architecture

**When to Update:**
- System architecture changes
- New components added
- Component relationships modified
- Regenerate from source files (`.dot`, `.puml`)

---

## Documentation Standards

### Markdown Conventions

1. **Headers:**
   - Use `#` for main title
   - Use `##` for major sections
   - Use `###` for subsections
   - Use `---` for horizontal rules between major sections

2. **Code Blocks:**
   - Always specify language: ````bash`, ````csharp`, ````json`, ````fsharp`
   - Include descriptive comments for complex commands
   - Use `# Example:` or `// Example:` for inline examples

3. **Links:**
   - Use relative links for internal documentation: `[text](../operations/file.md) or [text](../development/file.md)`
   - Use descriptive link text (not "click here")
   - Verify all links work after updates

4. **Tables:**
   - Use markdown tables for structured information
   - Align columns with `|---|` separators
   - Keep table headers concise

5. **Code Examples:**
   - Provide working, tested examples
   - Include both positive and negative cases where relevant
   - Show expected output when helpful

### Version Information

Always update version information when documenting changes:

- Update `docs/README.md` "Last Updated" field
- Update version numbers in relevant guides
- Add entries to `docs/changelogs/CHANGES_SUMMARY.md`

### Cross-References

Maintain consistency across documentation:

- When documenting a feature, update ALL relevant docs
- Check cross-references in related documentation
- Update the main `docs/README.md` index if adding new files
- Update `docs/toc.yml` for DocFX navigation

---

## Common Documentation Tasks

### Task 1: Document a New Feature

**Checklist:**
- [ ] Update `docs/getting-started.md` if user-facing
- [ ] Update `docs/configuration.md` if configurable
- [ ] Update `docs/architecture/overview.md` if architectural impact
- [ ] Add to `docs/status/improvements.md` as implemented
- [ ] Update root `README.md` if significant feature
- [ ] Add examples and code snippets
- [ ] Update diagrams if component structure changed
- [ ] Update `docs/README.md` "Last Updated" date
- [ ] Test all code examples

### Task 2: Document a Configuration Change

**Checklist:**
- [ ] Update `docs/configuration.md` with new options
- [ ] Update `appsettings.sample.json` with examples
- [ ] Document default values and valid ranges
- [ ] Explain impact and use cases
- [ ] Update troubleshooting if new error scenarios
- [ ] Update root `README.md` if affects installation

### Task 3: Update Architecture Documentation

**Checklist:**
- [ ] Update `docs/architecture/overview.md` with changes
- [ ] Update relevant component documentation
- [ ] Regenerate diagrams from source files (`.dot`, `.puml`)
- [ ] Update `docs/architecture/c4-diagrams.md`
- [ ] Document design decisions in `docs/architecture/why-this-architecture.md`
- [ ] Update `docs/architecture/domains.md` if domain model changed

### Task 4: Document a Provider Integration

**Checklist:**
- [ ] Create or update setup guide in `docs/providers/`
- [ ] Update `docs/providers/data-sources.md` with provider status
- [ ] Update `docs/providers/provider-comparison.md`
- [ ] Document configuration options
- [ ] Provide connection examples
- [ ] Document data format and limitations
- [ ] Add troubleshooting section
- [ ] Update `docs/architecture/provider-management.md` if needed

### Task 5: Update Status Documentation

**Checklist:**
- [ ] Update `docs/status/production-status.md` for readiness
- [ ] Update `docs/status/improvements.md` for implemented features
- [ ] Update `docs/status/FEATURE_BACKLOG.md` for roadmap
- [ ] Document known issues and workarounds
- [ ] Update completion status of features

---

## Documentation Testing

### Verification Steps

1. **Link Validation:**
   ```bash
   # Check for broken internal links
   find docs -name "*.md" -exec grep -H "\[.*\](.*\.md)" {} \; | grep -v "http"
   ```

2. **Code Example Testing:**
   - Extract and test all code examples
   - Verify commands produce expected output
   - Test configuration examples against schema

3. **Cross-Reference Check:**
   - Ensure consistent terminology across docs
   - Verify all referenced files exist
   - Check version numbers are current

4. **Build Documentation:**
   ```bash
   # If DocFX is configured
   cd docs/docfx
   docfx build docfx.json
   ```

5. **Visual Review:**
   - Preview markdown rendering (GitHub, VS Code, etc.)
   - Check diagram images display correctly
   - Verify table formatting

---

## Documentation Build and Generation

### DocFX Documentation

The project uses DocFX for generating API documentation:

**Location:** `docs/docfx/`

**Configuration:** `docs/docfx/docfx.json`

**To Build:**
```bash
cd MarketDataCollector/docs/docfx
docfx build docfx.json
```

**Output:** `docs/_site/`

### Diagram Generation

Diagrams are stored as source files and rendered images:

**DOT Graphs (Graphviz):**
```bash
cd MarketDataCollector/docs/diagrams
dot -Tpng c4-level1-context.dot -o c4-level1-context.png
dot -Tsvg c4-level1-context.dot -o c4-level1-context.svg
```

**PlantUML:**
```bash
cd MarketDataCollector/docs/architecture
plantuml c4-context.puml
```

**Always regenerate diagrams from source files, not manually edit rendered images.**

---

## Best Practices

### 1. Audience Awareness

Write for the appropriate audience:

- **End Users:** Focus on how-to, troubleshooting, configuration
- **Operators:** Focus on deployment, monitoring, maintenance
- **Developers:** Focus on architecture, APIs, extension points
- **Quant Developers:** Focus on data formats, integrations, algorithms

### 2. Keep Documentation Close to Code

- Document APIs with XML comments in code
- Keep configuration examples in sync with schema
- Update docs in the same PR as code changes

### 3. Provide Context

- Explain **why**, not just **what**
- Include use cases and examples
- Link to related documentation
- Provide troubleshooting guidance

### 4. Use Consistent Terminology

Refer to the project's domain language:

- "Provider" not "data source" or "feed"
- "Collector" not "service" or "worker"
- "Event" not "message" or "data"
- "Storage" not "database" or "persistence"

### 5. Document Decisions

Use `docs/architecture/why-this-architecture.md` and `docs/reference/design-review-memo.md` to document:

- Technology choices
- Trade-offs considered
- Rejected alternatives
- Future considerations

### 6. Keep It Up-to-Date

- Update docs immediately when code changes
- Remove outdated information
- Mark deprecated features clearly
- Archive old documentation rather than delete

---

## File Naming Conventions

- Use lowercase with hyphens: `getting-started.md`
- Be descriptive: `interactive-brokers-setup.md` not `ib-setup.md`
- Group related docs in directories
- Use `README.md` for directory index files

---

## GitHub Copilot Instructions

When updating documentation, also consider updating:

**`.github/copilot-instructions.md`** - Instructions for GitHub Copilot

This file contains build commands, project structure, and development practices. Update when:

- Build process changes
- New project structure added
- Common issues identified
- Development practices established

---

## Tools and Resources

### Markdown Editors

- VS Code with Markdown extensions
- GitHub's built-in editor (with preview)
- Typora, Mark Text (standalone editors)

### Documentation Tools

- **DocFX** - .NET documentation generator
- **Graphviz** - DOT diagram rendering
- **PlantUML** - UML diagram generation
- **Mermaid** - Markdown-native diagrams (future consideration)

### Linting and Validation

```bash
# Markdown linting (if configured)
markdownlint docs/**/*.md

# Link checking
markdown-link-check docs/**/*.md
```

---

## Workflow for Documentation Updates

### Step-by-Step Process

1. **Understand the Change:**
   - Review code changes or feature requirements
   - Identify affected documentation areas
   - Determine audience impact (users, operators, developers)

2. **Plan Updates:**
   - List all documentation files requiring updates
   - Check cross-references and dependencies
   - Identify diagrams needing regeneration

3. **Make Updates:**
   - Update documentation files
   - Add code examples and test them
   - Regenerate diagrams if needed
   - Update version information

4. **Validate:**
   - Check links and cross-references
   - Test code examples
   - Preview markdown rendering
   - Verify diagrams display correctly

5. **Review Cross-Documentation:**
   - Ensure consistency across related docs
   - Update main index (`docs/README.md`)
   - Update changelog if significant

6. **Commit:**
   - Use descriptive commit messages
   - Group related documentation updates
   - Reference related code changes if applicable

---

## Examples

### Example 1: Adding a New Provider

**Files to Update:**

1. `docs/providers/new-provider-setup.md` (create new)
   ```markdown
   # New Provider Setup Guide
   
   ## Overview
   
   Brief description of the provider...
   
   ## Prerequisites
   
   - List requirements
   
   ## Installation
   
   Step-by-step setup...
   
   ## Configuration
   
   ```json
   {
     "Providers": {
       "NewProvider": {
         "ApiKey": "your-api-key"
       }
     }
   }
   ```
   
   ## Troubleshooting
   
   Common issues...
   ```

2. `docs/providers/data-sources.md` - Add entry to provider table
3. `docs/providers/provider-comparison.md` - Add comparison row
4. `docs/configuration.md` - Add configuration section
5. `docs/architecture/provider-management.md` - Document integration approach
6. `docs/README.md` - Add to provider documentation list

### Example 2: Documenting a Configuration Option

**In `docs/configuration.md`:**

```markdown
### StorageBufferSize

**Type:** `int`  
**Default:** `10000`  
**Valid Range:** `1000` - `100000`  

Controls the size of the in-memory buffer before flushing to disk.

**Example:**
```json
{
  "Storage": {
    "BufferSize": 50000
  }
}
```

**Impact:**
- Higher values = better performance, more memory usage
- Lower values = lower memory usage, more frequent disk writes

**Related Settings:** `FlushIntervalSeconds`, `MaxMemoryMB`
```

### Example 3: Updating Architecture Documentation

**When adding a new component:**

1. Update `docs/architecture/overview.md` - Add component description
2. Update `docs/diagrams/c4-level2-containers.dot` - Add container node
3. Regenerate diagram: `dot -Tpng c4-level2-containers.dot -o c4-level2-containers.png`
4. Update `docs/architecture/c4-diagrams.md` - Reference new component
5. Document in `docs/architecture/why-this-architecture.md` if significant design decision

---

## Quality Checklist

Before finalizing documentation updates:

- [ ] All code examples tested and working
- [ ] Links verified (internal and external)
- [ ] Terminology consistent with project conventions
- [ ] Appropriate audience level (user/operator/developer)
- [ ] Version information updated
- [ ] Cross-references checked
- [ ] Diagrams regenerated from source if changed
- [ ] Main index (`docs/README.md`) updated
- [ ] Markdown properly formatted and renders correctly
- [ ] No sensitive information (API keys, passwords) committed
- [ ] Related documentation files also updated
- [ ] Changelog updated if significant changes

---

## Getting Help

When unsure about documentation updates:

1. **Review existing documentation** for patterns and conventions
2. **Check `docs/README.md`** for structure guidelines
3. **Reference `.github/copilot-instructions.md`** for project context
4. **Review recent documentation commits** for examples
5. **Ask for clarification** on ambiguous requirements

---

## Agent Capabilities Summary

As the Documentation Agent, you can:

✅ **Update existing documentation files**
✅ **Create new documentation files**
✅ **Reorganize documentation structure**
✅ **Add code examples and test them**
✅ **Update diagrams and regenerate from source**
✅ **Maintain cross-references and links**
✅ **Update version information**
✅ **Review and validate documentation**

❌ **Do NOT make code changes** (except to fix code examples in docs)
❌ **Do NOT modify build configurations** (unless documenting them)
❌ **Do NOT change functionality** (only document it)

---

## Success Criteria

Your documentation updates are successful when:

1. **Accurate:** Information is correct and reflects current system behavior
2. **Complete:** All aspects of the change are documented
3. **Clear:** Appropriate audience can understand and use the information
4. **Consistent:** Terminology and style match existing documentation
5. **Current:** Version information and dates are updated
6. **Connected:** Cross-references and links are maintained
7. **Tested:** Code examples work and links are valid
8. **Discoverable:** Content is properly indexed and organized

---

## Revision History

- **2026-01-08:** Initial creation of documentation agent instructions
