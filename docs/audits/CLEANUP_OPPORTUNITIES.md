# Cleanup Opportunities Audit (Deep Scan, WPF-Only Direction)

_Date: 2026-02-10_

This audit provides a **thorough, file-specific cleanup plan** with WPF as the only supported desktop platform.
The focus is to make each cleanup item directly actionable by naming **where** to change and **how** to validate.

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

## 2) Immediate repository hygiene

### H1. Remove accidental artifact file

- **File:** `...`
- **Issue:** Tracked scratch output (`Line 319: 65`) appears non-source and non-doc.
- **Action:**
  1. Remove file from git.
  2. Add script guardrails to write temporary output into `build/` or system temp.
  3. Add CI check (optional) to prevent accidental root scratch artifacts.
- **Validation:** `git ls-files | rg '^\.\.\.$'` returns no matches.

### H2. Untrack local build logs

- **File:** `build-output.log`
- **Issue:** Machine-generated log in version control increases history churn.
- **Action:**
  1. Remove from git history moving forward (untrack now).
  2. Add ignore rule for `build-output.log` (or targeted `*.log` pattern with exceptions).
- **Validation:** `git ls-files | rg 'build-output\.log'` returns no matches.

### H3. Normalize root documentation

- **Files:** `PR_SUMMARY.md`, `UI_IMPROVEMENTS_SUMMARY.md`, `VISUAL_CODE_EXAMPLES.md`
- **Issue:** Root-level narrative docs dilute discoverability of canonical docs structure.
- **Action:**
  1. Move durable guidance to `docs/development/`.
  2. Move historical snapshots to `docs/archived/`.
  3. Update `README.md` links if these docs remain important entry points.
- **Validation:** root contains only intentionally top-level docs (`README`, license, contribution, etc.).

---

## 3) WPF-only platform migration (fully scoped)

This section explicitly lists all key files currently coupling the repo to UWP and how to clean each area.

### P1. Solution and project graph cleanup

- **Files to change:**
  - `MarketDataCollector.sln`
  - any `.props`/`.targets` or project refs referencing `src/MarketDataCollector.Uwp`
- **Current evidence:** solution currently includes `MarketDataCollector.Uwp` project entry.
- **Action:**
  1. Remove UWP project entry from `.sln`.
  2. Remove any transitive references to UWP project path.
  3. Keep WPF + shared/core projects only.
- **Validation:**
  - `dotnet sln MarketDataCollector.sln list` has no UWP project.
  - `dotnet restore` succeeds.

### P2. CI/CD workflow cleanup

- **Files to change:**
  - `.github/workflows/desktop-builds.yml`
  - `.github/workflows/scheduled-maintenance.yml`
  - `.github/workflows/README.md`
  - `.github/QUICKSTART.md`
  - `.github/labeler.yml`
- **Current evidence:** workflow/jobs/env/path filters explicitly reference UWP project and UWP build stages.
- **Action:**
  1. Remove UWP jobs, matrices, env vars (`UWP_PROJECT`), and path filters.
  2. Keep/expand WPF pipeline stages to preserve desktop CI signal.
  3. Update workflow docs/quickstart/labeler to WPF-only terminology.
- **Validation:**
  - `rg -n "MarketDataCollector\.Uwp|\bUWP\b|\buwp\b" .github` has no active workflow matches (except archived notes if intentionally retained).

### P3. Source tree cleanup (delete UWP project safely)

- **Files/directories to change:**
  - `src/MarketDataCollector.Uwp/**`
  - any direct consumers of UWP namespaces/classes
- **Action sequence:**
  1. Compare overlapping UWP/WPF services and identify behavior that exists only in UWP.
  2. Port shared logic into `src/MarketDataCollector.Ui.Services/Services/`.
  3. Keep platform adapters only in WPF where required.
  4. Delete `src/MarketDataCollector.Uwp/**` once references are removed.
- **Validation:**
  - `rg -n "MarketDataCollector\.Uwp|namespace .*Uwp" src tests` returns no active references.
  - full solution build succeeds.

### P4. Tests and coverage cleanup

- **Files to change:**
  - `tests/MarketDataCollector.Tests/Integration/UwpCoreIntegrationTests.cs`
  - `tests/coverlet.runsettings`
  - any UWP-specific test filters in workflow commands
- **Current evidence:** explicit UWP integration test file and coverage exclusion for `MarketDataCollector.Uwp`.
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

### P6. Detailed migration checklist for overlapping services

Near-duplicate same-name service pairs already detected (high similarity):

- `src/MarketDataCollector.Uwp/Services/BrushRegistry.cs` ↔ `src/MarketDataCollector.Wpf/Services/BrushRegistry.cs`
- `src/MarketDataCollector.Uwp/Services/StorageService.cs` ↔ `src/MarketDataCollector.Wpf/Services/StorageService.cs`
- `src/MarketDataCollector.Uwp/Services/RetentionAssuranceService.cs` ↔ `src/MarketDataCollector.Wpf/Services/RetentionAssuranceService.cs`
- `src/MarketDataCollector.Uwp/Services/ExportPresetService.cs` ↔ `src/MarketDataCollector.Wpf/Services/ExportPresetService.cs`

Also review triple-implementation candidates for ownership decisions (UWP/WPF/shared):

- `CredentialService.cs`
- `ConfigService.cs`
- `LoggingService.cs`
- `NotificationService.cs`
- `SchemaService.cs`
- `ArchiveHealthService.cs`
- `WatchlistService.cs`

**Migration rule:**

1. Canonical behavior target is WPF + shared services.
2. If UWP contains unique behavior, port to shared/WPF before deletion.
3. Delete UWP counterpart only after regression checks pass.

---

## 4) High-impact structural refactors (non-platform)

### S1. Decompose `UiServer` endpoint monolith

- **File:** `src/MarketDataCollector.Application/Http/UiServer.cs` (~3030 LOC, ~155 endpoint mappings).
- **Action:** split by endpoint domain into extension methods/modules:
  - Health, Config, Storage, Symbols, Diagnostics, Credentials.
- **Recommended implementation:** `app.MapHealthEndpoints()`, `app.MapStorageEndpoints()`, etc.
- **Validation:** endpoint behavior parity via integration tests and OpenAPI/doc endpoint checks.

### S2. Break apart HTML template monolith

- **File:** `src/MarketDataCollector.Ui.Shared/HtmlTemplates.cs` (~2510 LOC).
- **Action:**
  1. Move static CSS/JS into versioned files under `wwwroot`.
  2. Keep C# template code for dynamic fragments only.
  3. Split into composable rendering functions (layout/navigation/status/forms).
- **Validation:** page renders unchanged; no regression in escaping or path interpolation.

### S3. Split storage workflow mega-services

- **Files:**
  - `src/MarketDataCollector.Storage/Packaging/PortableDataPackager.cs`
  - `src/MarketDataCollector.Storage/Export/AnalysisExportService.cs`
  - `src/MarketDataCollector.Storage/Services/StorageCatalogService.cs`
- **Action:** isolate orchestration vs IO vs validation vs report writing into dedicated classes.
- **Validation:** existing tests continue to pass; add focused unit tests around extracted collaborators.

---

## 5) Architecture debt cleanup

### A1. Resolve DI boundary TODO in gap repair

- **File:** `src/MarketDataCollector.Infrastructure/Providers/Historical/GapAnalysis/DataGapRepair.cs`
- **Known marker:** `TODO: Implement via dependency injection - Infrastructure cannot reference Storage`
- **Action:** introduce abstraction in appropriate layer; inject implementation at composition root.
- **Validation:** storage dependency no longer directly referenced from forbidden layer.

### A2. Clarify `SubscriptionManager` role boundaries

- **Files:**
  - `src/MarketDataCollector.Application/Subscriptions/SubscriptionManager.cs`
  - `src/MarketDataCollector.Infrastructure/Providers/SubscriptionManager.cs`
  - `src/MarketDataCollector.Infrastructure/Shared/SubscriptionManager.cs`
- **Issue:** same class name across neighboring layers obscures intent.
- **Action:** rename to role-specific classes and enforce dependency direction.
- **Validation:** naming reflects function (coordinator/adapter/helper), and cross-layer usage is explicit.

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

## 7) Execution roadmap (recommended PR sequence)

1. **PR-1 Hygiene:** remove `...`, untrack `build-output.log`, relocate root summary docs.
2. **PR-2 Platform references:** remove UWP from `.sln`, CI workflows, labels, quickstart, and active docs.
3. **PR-3 Service migration:** port any UWP-only behavior into WPF/shared services.
4. **PR-4 UWP deletion:** delete `src/MarketDataCollector.Uwp/**` and resolve remaining references.
5. **PR-5 Structural refactors:** split `UiServer`, `HtmlTemplates`, and storage mega-services.
6. **PR-6 Architecture debt:** resolve `DataGapRepair` DI TODO and `SubscriptionManager` boundaries.
7. **PR-7 Generated docs refresh:** regenerate inventories/diagrams after structural changes.

## 8) Definition of done

- No UWP references in active solution, CI, tests, and non-archived docs.
- WPF desktop build/test path is green in CI.
- Root directory is free of accidental artifacts and local logs.
- Large-file refactors have clear ownership boundaries and passing tests.
- Architecture TODOs are converted into issues/PRs and closed with explicit layer boundaries.
