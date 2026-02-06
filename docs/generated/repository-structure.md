# Repository Structure

> Auto-generated on 2026-02-06 02:12:31 UTC

This document provides an overview of the Market Data Collector repository structure.

## Directory Layout

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
│   ├── IMPROVEMENTS.md
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
│       ├── Collections/
│       │   ├── BoundedObservableCollection.cs
│       │   └── CircularBuffer.cs
│       ├── Contracts/
│       │   ├── IConfigService.cs
│       │   ├── IConnectionService.cs
│       │   ├── INavigationService.cs
│       │   └── IStatusService.cs
│       ├── Models/
│       │   ├── AppConfig.cs
│       │   └── SharedModelAliases.cs
│       ├── Services/
│       │   ├── ActivityFeedService.cs
│       │   ├── AdminMaintenanceService.cs
│       │   ├── AdvancedAnalyticsService.cs
│       │   ├── AnalysisExportWizardService.cs
│       │   ├── ApiClientService.cs
│       │   ├── ArchiveBrowserService.cs
│       │   ├── ArchiveHealthService.cs
│       │   ├── BackfillService.cs
│       │   ├── BackgroundTaskSchedulerService.cs
│       │   ├── BatchExportSchedulerService.cs
│       │   ├── BrushRegistry.cs
│       │   ├── ChartingService.cs
│       │   ├── CollectionSessionService.cs
│       │   ├── ConfigService.cs
│       │   ├── ConnectionService.cs
│       │   ├── ContextMenuService.cs
│       │   ├── CredentialService.cs
│       │   ├── DataCalendarService.cs
│       │   ├── DataCompletenessService.cs
│       │   ├── DataSamplingService.cs
│       │   ├── DiagnosticsService.cs
│       │   ├── ErrorHandlingService.cs
│       │   ├── ErrorMessages.cs
│       │   ├── EventReplayService.cs
│       │   ├── ExportPresetService.cs
│       │   ├── FirstRunService.cs
│       │   ├── FormValidationService.cs
│       │   ├── HttpClientConfiguration.cs
│       │   ├── IBackgroundTaskSchedulerService.cs
│       │   ├── IConfigService.cs
│       │   ├── IKeyboardShortcutService.cs
│       │   ├── ILoggingService.cs
│       │   ├── IMessagingService.cs
│       │   ├── InfoBarService.cs
│       │   ├── INotificationService.cs
│       │   ├── IntegrityEventsService.cs
│       │   ├── IOfflineTrackingPersistenceService.cs
│       │   ├── IPendingOperationsQueueService.cs
│       │   ├── IThemeService.cs
│       │   ├── KeyboardShortcutService.cs
│       │   ├── LeanIntegrationService.cs
│       │   ├── LiveDataService.cs
│       │   ├── LoggingService.cs
│       │   ├── ManifestService.cs
│       │   ├── MessagingService.cs
│       │   ├── NavigationService.cs
│       │   ├── NotificationService.cs
│       │   ├── OAuthRefreshService.cs
│       │   ├── OfflineTrackingPersistenceService.cs
│       │   ├── OrderBookVisualizationService.cs
│       │   ├── PendingOperationsQueueService.cs
│       │   ├── PortablePackagerService.cs
│       │   ├── PortfolioImportService.cs
│       │   ├── ProviderHealthService.cs
│       │   ├── ProviderManagementService.cs
│       │   ├── RetentionAssuranceService.cs
│       │   ├── ScheduledMaintenanceService.cs
│       │   ├── ScheduleManagerService.cs
│       │   ├── SchemaService.cs
│       │   ├── SearchService.cs
│       │   ├── SetupWizardService.cs
│       │   ├── SmartRecommendationsService.cs
│       │   ├── StatusService.cs
│       │   ├── StorageAnalyticsService.cs
│       │   ├── StorageOptimizationAdvisorService.cs
│       │   ├── StorageService.cs
│       │   ├── SymbolGroupService.cs
│       │   ├── SymbolManagementService.cs
│       │   ├── SymbolMappingService.cs
│       │   ├── SystemHealthService.cs
│       │   ├── ThemeService.cs
│       │   ├── TimeSeriesAlignmentService.cs
│       │   ├── TooltipService.cs
│       │   ├── WatchlistService.cs
│       │   ├── WorkspaceService.cs
│       │   ├── WpfAnalysisExportService.cs
│       │   ├── WpfDataQualityService.cs
│       │   └── WpfJsonOptions.cs
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

## Key Directories

| Directory | Purpose |
|-----------|---------|
| `.github/` | GitHub configuration |
| `benchmarks/` | Performance benchmarks |
| `build-system/` | Build tooling |
| `config/` | Configuration files |
| `deploy/` | Deployment configurations |
| `docs/` | Documentation |
| `scripts/` | Automation scripts |
| `src/` | Source code |
| `tests/` | Test projects |
| `tools/` | Development tools |

## Source Code Organization

### Core Application (`src/MarketDataCollector/`)

| Directory | Purpose |
|-----------|---------|
| `Domain/` | Business logic, collectors, events, models |
| `Infrastructure/` | Provider implementations, clients |
| `Storage/` | Data persistence, sinks, archival |
| `Application/` | Startup, configuration, HTTP endpoints |
| `Messaging/` | MassTransit message publishers |
| `Integrations/` | External system integrations |

### Microservices (`src/Microservices/`)

| Service | Port | Purpose |
|---------|------|---------|
| Gateway | 5000 | API Gateway and routing |
| TradeIngestion | 5001 | Trade data processing |
| QuoteIngestion | 5002 | Quote data processing |
| OrderBookIngestion | 5003 | Order book processing |
| HistoricalDataIngestion | 5004 | Historical backfill |
| DataValidation | 5005 | Data validation |

---

*This file is auto-generated. Do not edit manually.*
