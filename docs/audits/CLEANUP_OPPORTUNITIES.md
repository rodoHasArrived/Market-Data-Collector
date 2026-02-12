# Cleanup Opportunities Audit (Deep Scan, WPF-Only Direction)

_Date: 2026-02-10_  
_Last Updated: 2026-02-12_  
_Status: Partially Completed — Major structural refactors done, UWP removal in progress_

This audit provides a **thorough, file-specific cleanup plan** with WPF as the only supported desktop platform.
The focus is to make each cleanup item directly actionable by naming **where** to change and **how** to validate.

**Update (2026-02-12):** This document has been updated to reflect completed work including UiServer endpoint extraction, H1-H3 repository hygiene cleanup, and root documentation relocation. See completion status markers (✅) throughout.

## Quick Status Summary

| Category | Status | Details |
|----------|--------|---------|
| **Repository Hygiene (H1-H3)** | ✅ Complete | Artifacts removed, .gitignore enhanced, root docs normalized |
| **UiServer Endpoint Extraction (S1)** | ✅ Complete | 3030 LOC → 191 LOC (93.7% reduction), 32 endpoint modules |
| **UWP Platform Removal (P1-P6)** | ⚠️ In Progress | Solution/CI cleanup pending, service migration partial |
| **HtmlTemplates Split (S2)** | 🔴 Pending | 2511 LOC monolith remains |
| **Storage Services Split (S3)** | 🔴 Pending | Not started |
| **Architecture Debt (A1-A2)** | 🔴 Pending | DataGapRepair DI bug, SubscriptionManager naming |

**Key Achievements:**
- 93,564 bytes of artifacts removed (build logs, scratch files)
- 2,839 LOC removed from UiServer via endpoint extraction
- Comprehensive .gitignore with documentation
- Root directory cleaned of historical narrative docs

**Critical Remaining Work:**
- Remove UWP from solution and CI workflows
- Complete WPF/UWP service deduplication (6 complex services)
- Fix DataGapRepair DI boundary violation (data loss bug)
- Split HtmlTemplates and storage service monoliths

## 1) Scan methods and evidence sources

### Commands used

1. `rg --files` for broad inventory.
2. `rg -n "TODO|FIXME|HACK" src --glob '!**/bin/**' --glob '!**/obj/**'` for technical-debt markers.
3. `rg -n "MarketDataCollector\.Uwp|Uwp|UWP" ...` across solution, workflows, tests, docs for platform coupling.
4. `git ls-files | xargs -I{} du -k "{}" | sort -nr` to spot large tracked artifacts.
5. Python line-count scan to identify monolith files.
6. Python similarity scan for UWP/WPF same-name services.

### What this enables

- Complete UWP removal sequencing without breaking WPF builds.
- Cleaner review scope for large classes/files by decomposing into smaller PRs.
- Predictable migration of overlapping UWP/WPF logic into shared/WPF code.

---

## 2) Immediate repository hygiene — ✅ COMPLETED

**Status:** All hygiene tasks (H1, H2, H3) completed. See `docs/audits/CLEANUP_SUMMARY.md` for detailed completion report.

### H1. Remove accidental artifact file — ✅ DONE

- **File:** `...`
- **Issue:** Tracked scratch output (`Line 319: 65`) appears non-source and non-doc.
- **Status:** ✅ Removed in commit 77179ec
- **Verification:** File no longer tracked; `.gitignore` patterns added to prevent future scratch artifacts.

### H2. Untrack local build logs — ✅ DONE

- **File:** `build-output.log`
- **Issue:** Machine-generated log in version control increases history churn.
- **Status:** ✅ Removed in commit 77179ec (93,549 bytes deleted)
- **Improvements:** Comprehensive `.gitignore` patterns added with inline documentation for build logs, temporary files, and artifacts.
- **Verification:** No log files tracked; `*.log` pattern prevents future accidental commits.

### H3. Normalize root documentation — ✅ DONE

- **Files:** `PR_SUMMARY.md`, `UI_IMPROVEMENTS_SUMMARY.md`, `VISUAL_CODE_EXAMPLES.md`
- **Issue:** Root-level narrative docs dilute discoverability of canonical docs structure.
- **Status:** ✅ All files moved to `docs/archived/` with date prefixes (2026-02_*)
- **Verification:** Root directory contains only intentional top-level docs (`README.md`, `LICENSE`, `CLAUDE.md`, etc.).

---

## 3) WPF-only platform migration (fully scoped) — ⚠️ IN PROGRESS

This section explicitly lists all key files currently coupling the repo to UWP and how to clean each area.

**Status:** UWP project still exists in repository. Detailed removal plan tracked in ROADMAP.md Phase 6.

### P1. Solution and project graph cleanup — 🔴 PENDING

- **Files to change:**
  - `MarketDataCollector.sln`
  - any `.props`/`.targets` or project refs referencing `src/MarketDataCollector.Uwp`
- **Current evidence:** ✅ Verified — UWP project still in solution: `Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "MarketDataCollector.Uwp"`
- **Status:** 🔴 **NOT STARTED**
- **Action:**
  1. Remove UWP project entry from `.sln`.
  2. Remove any transitive references to UWP project path.
  3. Keep WPF + shared/core projects only.
- **Validation:**
  - `dotnet sln MarketDataCollector.sln list` has no UWP project.
  - `dotnet restore` succeeds.

### P2. CI/CD workflow cleanup — 🔴 PENDING

- **Files to change:**
  - `.github/workflows/desktop-builds.yml`
  - `.github/workflows/scheduled-maintenance.yml`
  - `.github/workflows/README.md`
  - `.github/QUICKSTART.md`
  - `.github/labeler.yml`
- **Current evidence:** ✅ Verified — workflow references found:
  - `UWP_PROJECT: 'src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj'`
  - `uwp-generate-assets` job
  - Path filters: `'src/MarketDataCollector.Uwp/**'`
- **Status:** 🔴 **NOT STARTED**
- **Action:**
  1. Remove UWP jobs, matrices, env vars (`UWP_PROJECT`), and path filters.
  2. Keep/expand WPF pipeline stages to preserve desktop CI signal.
  3. Update workflow docs/quickstart/labeler to WPF-only terminology.
- **Validation:**
  - `grep -rn "MarketDataCollector\.Uwp\|UWP_PROJECT\|uwp-generate-assets" .github/workflows` returns no matches.

### P3. Source tree cleanup (delete UWP project safely) — 🔴 BLOCKED

- **Files/directories to change:**
  - `src/MarketDataCollector.Uwp/**` (14 subdirectories confirmed present)
  - any direct consumers of UWP namespaces/classes
- **Current evidence:** ✅ Verified — UWP directory exists: `drwxrwxr-x 14 runner runner 4096 Feb 12 18:13 MarketDataCollector.Uwp`
- **Status:** 🔴 **BLOCKED** — waiting on P6 service migration completion
- **Action sequence:**
  1. Compare overlapping UWP/WPF services and identify behavior that exists only in UWP.
  2. Port shared logic into `src/MarketDataCollector.Ui.Services/Services/`.
  3. Keep platform adapters only in WPF where required.
  4. Delete `src/MarketDataCollector.Uwp/**` once references are removed.
- **Validation:**
  - `grep -rn "MarketDataCollector\.Uwp|namespace .*Uwp" src tests` returns no active references.
  - full solution build succeeds.

### P4. Tests and coverage cleanup — 🔴 PENDING

- **Files to change:**
  - `tests/MarketDataCollector.Tests/Integration/UwpCoreIntegrationTests.cs`
  - `tests/coverlet.runsettings`
  - any UWP-specific test filters in workflow commands
- **Current evidence:** ✅ Verified — UWP integration test file exists
- **Status:** 🔴 **NOT STARTED**
- **Action:**
  1. Remove/replace UWP integration tests with WPF-focused integration coverage where needed.
  2. Remove UWP-specific coverage exclusions; tune exclusions for remaining projects only.
  3. Update CI test filters to remove UWP categories/names.
- **Validation:** `dotnet test` and coverage steps pass with no UWP references.

### P5. Documentation cleanup for platform reality

- **Files to update (non-exhaustive but high-priority):**
  - `docs/development/github-actions-summary.md`
  - `docs/ai/claude/CLAUDE.actions.md`
  - `docs/generated/repository-structure.md` (regenerated)
  - `.github/workflows/README.md`
  - `.github/QUICKSTART.md`
- **Action:**
  1. Replace dual-platform wording (“UWP + WPF”, “desktop-app.yml UWP”, etc.) with WPF-only wording.
  2. Move historical UWP process docs to `docs/archived/` and mark as superseded.
  3. Regenerate generated repository structure docs after code removal.
- **Validation:** platform docs consistently describe WPF-only desktop support.

### P6. Detailed migration checklist for overlapping services — ⚠️ PARTIALLY COMPLETE

Near-duplicate same-name service pairs already detected (high similarity):

- `src/MarketDataCollector.Uwp/Services/BrushRegistry.cs` ↔ `src/MarketDataCollector.Wpf/Services/BrushRegistry.cs`
- `src/MarketDataCollector.Uwp/Services/StorageService.cs` ↔ `src/MarketDataCollector.Wpf/Services/StorageService.cs`
- `src/MarketDataCollector.Uwp/Services/RetentionAssuranceService.cs` ↔ `src/MarketDataCollector.Wpf/Services/RetentionAssuranceService.cs`
- `src/MarketDataCollector.Uwp/Services/ExportPresetService.cs` ↔ `src/MarketDataCollector.Wpf/Services/ExportPresetService.cs`

Triple-implementation candidates for ownership decisions (UWP/WPF/shared):

- ✅ `FormValidationService.cs` — extracted to shared (ROADMAP Phase 6C.1)
- ✅ `ThemeService.cs` — base class in shared, platform adapters in WPF/UWP (ROADMAP Phase 6C.2)
- ✅ `SchemaService.cs` — base class in shared, WPF extends base (ROADMAP Phase 6C.3)
- 🔴 `CredentialService.cs` — pending Phase 6C.3
- 🔴 `ConfigService.cs` — pending Phase 6C.2 (WPF uses different interface)
- 🔴 `LoggingService.cs` — pending Phase 6C.3
- 🔴 `NotificationService.cs` — pending Phase 6C.3
- 🔴 `ArchiveHealthService.cs` — pending Phase 6C.4
- 🔴 `WatchlistService.cs` — pending Phase 6C.3

**Migration rule:**

1. Canonical behavior target is WPF + shared services.
2. If UWP contains unique behavior, port to shared/WPF before deletion.
3. Delete UWP counterpart only after regression checks pass.

**Progress:** See ROADMAP.md Phase 6C for detailed status and remaining work (~6 complex services).

---

## 4) High-impact structural refactors (non-platform) — PARTIALLY COMPLETED

### S1. Decompose `UiServer` endpoint monolith — ✅ DONE

- **File:** `src/MarketDataCollector.Application/Http/UiServer.cs` (was ~3030 LOC, now **191 LOC** — 93.7% reduction).
- **Status:** ✅ **COMPLETED** in commit 540f5bd
- **Achievement:** All endpoint logic extracted to 32 dedicated endpoint modules in `src/MarketDataCollector.Ui.Shared/Endpoints/`:
  - `AdminEndpoints`, `AlignmentEndpoints`, `AnalyticsEndpoints`, `ApiKeyMiddleware`
  - `BackfillEndpoints`, `BackfillScheduleEndpoints`, `ConfigEndpoints`, `CronEndpoints`
  - `DiagnosticsEndpoints`, `ExportEndpoints`, `FailoverEndpoints`, `HealthEndpoints`
  - `IBEndpoints`, `IndexEndpoints`, `LeanEndpoints`, `LiveDataEndpoints`
  - `MaintenanceScheduleEndpoints`, `MessagingEndpoints`, `PathValidation`
  - `ProviderEndpoints`, `ProviderExtendedEndpoints`, `QualityDropsEndpoints`
  - `ReplayEndpoints`, `SamplingEndpoints`, `StatusEndpoints`, `StorageEndpoints`
  - `StorageQualityEndpoints`, `StubEndpoints`, `SubscriptionEndpoints`
  - `SymbolEndpoints`, `SymbolMappingEndpoints`, `UiEndpoints`
- **Removed methods:** `ConfigureStorageOrganizationRoutes` (443 LOC), `ConfigureSymbolManagementRoutes` (649 LOC), `ConfigureNewFeatureRoutes` (375 LOC), `ConfigureCredentialManagementRoutes` (451 LOC), `ConfigureBulkSymbolManagementRoutes` (582 LOC)
- **Validation:** Endpoint behavior parity maintained; all endpoints functional via modular pattern.

### S2. Break apart HTML template monolith — ⚠️ PENDING

- **File:** `src/MarketDataCollector.Ui.Shared/HtmlTemplates.cs` (**2511 LOC** — still monolithic).
- **Status:** ⚠️ **NOT STARTED** — remains high priority for Phase 8
- **Action:**
  1. Move static CSS/JS into versioned files under `wwwroot`.
  2. Keep C# template code for dynamic fragments only.
  3. Split into composable rendering functions (layout/navigation/status/forms).
- **Validation:** page renders unchanged; no regression in escaping or path interpolation.

### S3. Split storage workflow mega-services — ⚠️ PENDING

- **Files:**
  - `src/MarketDataCollector.Storage/Packaging/PortableDataPackager.cs`
  - `src/MarketDataCollector.Storage/Export/AnalysisExportService.cs`
  - `src/MarketDataCollector.Storage/Services/StorageCatalogService.cs`
- **Status:** ⚠️ **NOT STARTED** — deferred to Phase 8
- **Action:** isolate orchestration vs IO vs validation vs report writing into dedicated classes.
- **Validation:** existing tests continue to pass; add focused unit tests around extracted collaborators.

---

## 5) Architecture debt cleanup — ⚠️ PENDING

### A1. Resolve DI boundary TODO in gap repair — ⚠️ PENDING

- **File:** `src/MarketDataCollector.Infrastructure/Providers/Historical/GapAnalysis/DataGapRepair.cs`
- **Known marker:** `TODO: Implement via dependency injection - Infrastructure cannot reference Storage`
- **Status:** ⚠️ **NOT STARTED** — tracked in Phase 0 (Critical Fixes) of ROADMAP.md
- **Action:** introduce abstraction in appropriate layer; inject implementation at composition root.
- **Validation:** storage dependency no longer directly referenced from forbidden layer.
- **Priority:** P1 — This is a data loss bug (gap repair fetches data but discards it due to circular dependency)

### A2. Clarify `SubscriptionManager` role boundaries — ⚠️ PENDING

- **Files:**
  - `src/MarketDataCollector.Application/Subscriptions/SubscriptionManager.cs` (19 LOC class declaration)
  - `src/MarketDataCollector.Infrastructure/Shared/SubscriptionManager.cs`
- **Issue:** same class name across neighboring layers obscures intent.
- **Status:** ⚠️ **PARTIALLY ADDRESSED** — Phase 6D.1 in ROADMAP marked as completed but verification shows:
  - Application layer still has `SubscriptionManager` (not renamed to `SubscriptionCoordinator`)
  - Infrastructure layer has separate `SubscriptionManager` in Shared/
  - Naming collision still exists between layers
- **Action:** rename to role-specific classes and enforce dependency direction.
  - Application: `SubscriptionCoordinator` (orchestration)
  - Infrastructure: `ProviderSubscriptionAdapter` or similar (provider-specific logic)
- **Validation:** naming reflects function (coordinator/adapter/helper), and cross-layer usage is explicit.
- **Priority:** P2 — Confusing but not blocking

---

## 6) Generated docs and diagram churn control

### G1. Keep generated artifacts but isolate review noise

- **Likely noisy areas:** `docs/generated/*`, `docs/diagrams/*`, `docs/uml/*`.
- **Action:**
  1. Keep generation deterministic.
  2. Prefer dedicated docs-generation PRs.
  3. Use PR labels or commit conventions separating code vs generated refresh.
- **Validation:** functional PRs stay focused; generated asset updates are intentional and easy to review.

---

## 7) Execution roadmap (recommended PR sequence) — UPDATED

**Progress as of 2026-02-12:**

1. **PR-1 Hygiene:** ✅ **COMPLETED** — removed `...`, untracked `build-output.log`, relocated root summary docs to `docs/archived/`. See `docs/audits/CLEANUP_SUMMARY.md`.

2. **PR-2 Platform references:** ⚠️ **IN PROGRESS** — UWP still in solution, CI workflows, and active codebase
   - **Remaining work:**
     - Remove UWP project from `MarketDataCollector.sln`
     - Remove UWP jobs/stages from `.github/workflows/desktop-builds.yml`
     - Remove UWP references from `.github/workflows/scheduled-maintenance.yml`
     - Update `.github/labeler.yml` to remove UWP paths
     - Update `.github/QUICKSTART.md` to WPF-only wording
   - See Phase 6 in ROADMAP.md for detailed UWP removal plan

3. **PR-3 Service migration:** ⚠️ **PARTIALLY COMPLETE** — see Phase 6C in ROADMAP.md
   - ✅ Done: FormValidationService, ThemeService base classes, SchemaService base class
   - ⚠️ Remaining: ~6 complex services (AdminMaintenance, AdvancedAnalytics, ArchiveHealth, BackgroundTaskScheduler, OfflineTracking, PendingOperations)

4. **PR-4 UWP deletion:** ⚠️ **BLOCKED** — waiting on PR-2 and PR-3 completion
   - Cannot delete `src/MarketDataCollector.Uwp/` until all unique UWP logic is ported

5. **PR-5 Structural refactors:** ✅ **PARTIALLY COMPLETE**
   - ✅ UiServer endpoint extraction (93.7% reduction, 191 LOC remaining)
   - ⚠️ HtmlTemplates split (2511 LOC, not started)
   - ⚠️ Storage mega-services (not started)

6. **PR-6 Architecture debt:** ⚠️ **NOT STARTED**
   - A1: DataGapRepair DI TODO (P1 — data loss bug)
   - A2: SubscriptionManager naming collision (P2 — confusing but not blocking)

7. **PR-7 Generated docs refresh:** ⚠️ **NOT STARTED** — regenerate inventories/diagrams after structural changes.

## 8) Definition of done — UPDATED

**Current Status (2026-02-12):**

### ✅ Completed:
- ✅ Root directory is free of accidental artifacts and local logs
- ✅ Root documentation normalized (historical docs in `docs/archived/`)
- ✅ UiServer endpoint extraction complete (93.7% reduction to 191 LOC)
- ✅ Repository hygiene (H1, H2, H3) fully addressed

### ⚠️ In Progress:
- ⚠️ UWP references still present in solution, CI, and source tree
- ⚠️ WPF/UWP service deduplication partially complete (FormValidationService, base classes done; 6 complex services remain)

### 🔴 Pending:
- 🔴 No UWP references in active solution, CI, tests, and non-archived docs
- 🔴 WPF desktop build/test path is green in CI (currently blocked on service deduplication)
- 🔴 Large-file refactors have clear ownership boundaries (HtmlTemplates and storage services still monolithic)
- 🔴 Architecture TODOs are converted into issues/PRs and closed with explicit layer boundaries (DataGapRepair DI issue, SubscriptionManager naming collision)

**Next Critical Steps:**
1. Complete Phase 6C service deduplication (6 complex services)
2. Remove UWP from solution and CI (Phase 6, PR-2)
3. Delete UWP project after migration complete (Phase 6, PR-4)
4. Resolve DataGapRepair DI boundary TODO (Phase 0 critical fix)
5. Split HtmlTemplates monolith (2511 LOC → modular components)
