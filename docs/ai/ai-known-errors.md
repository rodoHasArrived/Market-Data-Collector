# AI Known Errors and Prevention Checklist

This file tracks repeated AI-agent mistakes so future agents can avoid introducing the same failures.

## How to use this file

1. Review this file before coding.
2. If your task intersects an item below, run its prevention checklist.
3. Add a new entry whenever an AI-caused issue is found and fixed.

## Automated issue intake workflow

AI regressions can be recorded automatically from GitHub issues:

1. Open an issue and apply the label `ai-known-error`.
2. Include these headings in the issue body so automation can extract fields:
   - `## Area`
   - `## Symptoms`
   - `## Root cause`
   - `## Prevention checklist`
   - `## Verification commands`
3. The `AI Known Errors Intake` job in `.github/workflows/documentation.yml` creates a PR that appends (or updates) an entry in this file.

If headings are missing, the workflow still creates an entry with safe defaults and links back to the source issue.

## Entry template

- **ID**: AI-YYYYMMDD-<short-tag>
- **Area**: (docs/build/tests/runtime/config)
- **Symptoms**: What failed.
- **Root cause**: Why the error was introduced.
- **Prevention checklist**:
  - [ ] Check 1
  - [ ] Check 2
- **Verification commands**:
  - `command`
- **Source issue**: #123
- **Status**: open | mitigated | closed

---

## Known issues

### AI-20260205-missing-error-memory
- **Area**: process/documentation
- **Symptoms**: Agents repeatedly reintroduced previously fixed mistakes because no persistent error memory existed.
- **Root cause**: No standardized location documenting recurrent AI errors and prevention steps.
- **Prevention checklist**:
  - [ ] Read this file at task start.
  - [ ] Cross-check your plan against existing known issues.
  - [ ] If a new AI-caused issue is fixed, add/update an entry before PR.
- **Verification commands**:
  - `test -f docs/ai/ai-known-errors.md`
  - `rg "AI-" docs/ai/ai-known-errors.md`
- **Source issue**: manual bootstrap
- **Status**: mitigated

### AI-20260205-wpf-grid-padding
- **Area**: build/WPF/XAML
- **Symptoms**: WPF builds fail with error MC3072: "The property 'Padding' does not exist in XML namespace". Build succeeds in UWP but fails in WPF.
- **Root cause**: Grid control doesn't support Padding property in WPF (unlike UWP/WinUI). This is a WPF/UWP API compatibility difference that agents may not be aware of when porting XAML code.
- **Prevention checklist**:
  - [ ] When working with WPF XAML, check that Grid elements don't use Padding, CornerRadius, BorderBrush, or BorderThickness properties
  - [ ] If padding is needed on a Grid in WPF, wrap it in a Border element instead
  - [ ] Search for `<Grid.*Padding=` pattern in WPF .xaml files before committing
  - [ ] Remember: Border, StackPanel, and DockPanel support Padding in WPF, but Grid does not
- **Verification commands**:
  - `grep -rn '<Grid.*Padding=' src/MarketDataCollector.Wpf --include="*.xaml"`
  - `dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release --no-restore -p:TargetFramework=net9.0-windows`
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21707017569/job/62600607213
- **Status**: fixed

### AI-20260205-nu1008-central-package-management
- **Area**: build/NuGet/CPM
- **Symptoms**: Build fails with error NU1008: "Projects that use central package version management should not define the version on the PackageReference items but on the PackageVersion items: <PackageName>". This occurs during `dotnet restore` or `dotnet build`.
- **Root cause**: The repository uses Central Package Management (CPM) with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Directory.Packages.props`. When a new package is added to a project's `.csproj` file without adding its version to `Directory.Packages.props`, NuGet restore fails with NU1008.
- **Prevention checklist**:
  - [ ] When adding a new `<PackageReference>` to any `.csproj` or `.fsproj` file, NEVER include a `Version` attribute
  - [ ] Always add the package version to `Directory.Packages.props` using `<PackageVersion Include="PackageName" Version="x.y.z" />`
  - [ ] Search for the appropriate section label in `Directory.Packages.props` (e.g., "Storage", "Testing", "WinUI / Desktop")
  - [ ] After adding package references, run `dotnet restore` to verify no NU1008 errors
  - [ ] Check existing packages in `Directory.Packages.props` for version compatibility before adding new ones
- **Verification commands**:
  - `dotnet restore MarketDataCollector.sln /p:EnableWindowsTargeting=true`
  - `dotnet build MarketDataCollector.sln -c Release --no-restore /p:EnableWindowsTargeting=true`
  - `grep -r 'PackageReference Include=".*" Version=' --include="*.csproj" --include="*.fsproj" src/ | grep -v '<!--'` (should return no results)
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21707148084/job/62601061503
- **Status**: fixed

### AI-20260206-provider-sdk-cross-file-type-resolution
- **Area**: build/ProviderSdk
- **Symptoms**: Build fails with CS0246 errors in `IProviderMetadata.cs`: "The type or namespace name 'ProviderType' could not be found" and "The type or namespace name 'Backfill' could not be found", even though the types exist in the same project and namespace.
- **Root cause**: When `IProviderMetadata.cs` was moved from the main MarketDataCollector project to the ProviderSdk project, it lost access to `ProviderType` (previously in `ProviderRegistry.cs`) and `HistoricalDataCapabilities` (previously in `IHistoricalDataProvider.cs`). A prior fix moved those types into standalone files in ProviderSdk but the cross-file namespace resolution for the relative `Backfill.HistoricalDataCapabilities` reference failed. The fix is to co-locate the `ProviderType` enum in the same file as its consumer and use an explicit `using` directive for the Backfill namespace.
- **Prevention checklist**:
  - [ ] When moving types between projects, verify all type references in the destination project resolve correctly
  - [ ] Use explicit `using` directives for sibling namespaces instead of relative namespace prefixes (e.g., `using X.Y.Backfill;` + `HistoricalDataCapabilities` instead of `Backfill.HistoricalDataCapabilities`)
  - [ ] Co-locate small types (enums, records) with their primary consumer when they are tightly coupled
  - [ ] After moving types, build the specific project in isolation: `dotnet build src/MarketDataCollector.ProviderSdk`
- **Verification commands**:
  - `dotnet build src/MarketDataCollector.ProviderSdk/MarketDataCollector.ProviderSdk.csproj -c Release`
  - `dotnet build MarketDataCollector.sln -c Release --no-restore /p:EnableWindowsTargeting=true`
- **Source issue**: PR #860 (incomplete fix)
- **Status**: fixed

### AI-20260207-storage-namespace-circular-dependency
- **Area**: build/Infrastructure
- **Symptoms**: Build fails with CS0234: "The type or namespace name 'Storage' does not exist in the namespace 'MarketDataCollector'" in `DataGapRepair.cs`. Three errors on the `using MarketDataCollector.Storage` lines.
- **Root cause**: During the layer assembly split (commit `ac7bd35`), `DataGapRepair.cs` was placed in the Infrastructure project but retained `using` statements for `MarketDataCollector.Storage`. The Infrastructure project does not (and should not) reference the Storage project to avoid circular dependencies. A prior fix only commented out the lines rather than removing them.
- **Prevention checklist**:
  - [ ] When splitting code into separate assemblies, verify all `using` directives resolve against the project's actual references
  - [ ] Infrastructure layer must never reference Storage layer directly; use abstractions (e.g., `IStorageSink`) injected via DI
  - [ ] Remove dead commented-out code entirely rather than leaving `// using` statements that obscure the issue
  - [ ] After moving files between projects, build the specific project: `dotnet build src/MarketDataCollector.Infrastructure`
- **Verification commands**:
  - `dotnet build src/MarketDataCollector.Infrastructure/MarketDataCollector.Infrastructure.csproj -c Release`
  - `grep -rn 'MarketDataCollector\.Storage' src/MarketDataCollector.Infrastructure --include="*.cs"` (should return no results)
- **Source issue**: CI build failure on main branch
- **Status**: fixed

### AI-20260210-cs0738-type-collision
- **Area**: build/namespaces
- **Symptoms**: Build fails with error CS0738: "'ConfigService' does not implement interface member 'IConfigService.ValidateConfigAsync(CancellationToken)' because it does not have the matching return type of 'Task<ConfigValidationResult>'". The interface method clearly returns `Task<ConfigValidationResult>` and the implementation returns the same type, yet the compiler rejects it.
- **Root cause**: Two classes with the same name (`ConfigValidationResult`) exist in parent and child namespaces: `MarketDataCollector.Ui.Services.ConfigValidationResult` (in DiagnosticsService.cs) and `MarketDataCollector.Ui.Services.Contracts.ConfigValidationResult` (in IConfigService.cs). When `ConfigService` (in the parent namespace) implements `IConfigService`, the compiler cannot disambiguate which `ConfigValidationResult` to use, even though the interface requires the one from the child namespace.
- **Prevention checklist**:
  - [ ] When creating new types, search for existing types with the same name in both parent and child namespaces
  - [ ] Use fully qualified type names in return types when ambiguity is possible: `Task<Contracts.ConfigValidationResult>`
  - [ ] When naming collision is detected, rename the type in the parent namespace with a descriptive prefix (e.g., `DiagnosticConfigValidationResult`)
  - [ ] After refactoring, verify no CS0738 errors: `dotnet build src/MarketDataCollector.Ui.Services -c Release`
  - [ ] Check for similar patterns: `grep -rn "class ConfigValidationResult" src --include="*.cs"` should show only one result per type
- **Verification commands**:
  - `dotnet build src/MarketDataCollector.Ui.Services/MarketDataCollector.Ui.Services.csproj -c Release`
  - `dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release -p:TargetFramework=net9.0-windows` (on Windows)
  - `dotnet build src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj -c Release -r win-x64 -p:Platform=x64` (on Windows)
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21851485930/job/63058846153
- **Status**: fixed (commit cec548e)

### AI-20260212-codecov-directory-mismatch
- **Area**: CI/GitHub Actions/codecov
- **Symptoms**: GitHub Actions step "Upload coverage reports" fails silently or reports no coverage found. The workflow completes but Codecov doesn't receive coverage data. In pr-checks.yml, step 5 (Upload coverage reports) fails to find coverage files.
- **Root cause**: The `dotnet test` command outputs coverage files to a directory specified by `--results-directory` parameter, but the `codecov-action` configuration uses a different directory path. For example, pr-checks.yml had `--results-directory ./artifacts/test-results` but codecov was configured with `directory: ./coverage`.
- **Prevention checklist**:
  - [ ] When modifying test commands with `--results-directory`, also update the codecov upload step
  - [ ] Use the `files:` parameter with glob pattern instead of `directory:` for codecov-action: `files: ./artifacts/test-results/**/coverage.cobertura.xml`
  - [ ] Verify consistency: the path in `files:` must match the path in `--results-directory`
  - [ ] Check that diagnostics artifact upload also references the correct coverage path
  - [ ] Search for all codecov-action usages: `grep -rn "codecov-action" .github/workflows/`
- **Verification commands**:
  - `grep -A10 "dotnet test" .github/workflows/pr-checks.yml | grep "results-directory"`
  - `grep -A3 "codecov-action" .github/workflows/pr-checks.yml | grep -E "(directory|files)"`
  - `dotnet test MarketDataCollector.sln --collect:"XPlat Code Coverage" --results-directory ./test-results && ls -la ./test-results/**/coverage.cobertura.xml`
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21938525658/job/63358083776#step:5:1
- **Status**: fixed (commit ad97ee2)

### AI-20260212-conditional-job-dependency
- **Area**: workflows/GitHub Actions
- **Symptoms**: Workflow jobs with `if: always()` fail to run when depending on jobs with conditional execution (`if: startsWith(...)`). The dependent job is skipped even though `always()` is specified, because GitHub Actions treats unmet dependencies (skipped jobs) as a blocking condition.
- **Root cause**: GitHub Actions job dependencies (`needs:`) require all listed jobs to complete successfully or be explicitly handled. When a job is conditionally skipped (e.g., `if: startsWith(github.ref, 'refs/tags/v')`), any job depending on it will also be skipped unless proper conditional logic is used. The `if: always()` condition means "run regardless of previous job failures" but not "run even if dependencies are skipped."
- **Prevention checklist**:
  - [ ] When a job has `needs:` dependencies, verify all dependent jobs run in the same conditions or have proper handling
  - [ ] Never depend on conditionally-executed jobs (with `if:` conditions) from jobs that always run
  - [ ] If cleanup jobs need to run `always()`, only depend on jobs that also run unconditionally
  - [ ] Use conditional expressions in dependencies: `if: always() && needs.job-name.result != 'skipped'`
  - [ ] Document job dependencies with comments explaining conditional logic
  - [ ] Test workflows on both tag and non-tag branches to verify all jobs execute as expected
- **Verification commands**:
  - `grep -A5 "needs:" .github/workflows/*.yml | grep -B5 "if:"`
  - `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/desktop-builds.yml'))"`
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21958711610/job/63429967664
- **Status**: fixed (commit 4f6088f)

---

### AI-20260212-wpf-globalusings-missing-directives
- **Area**: build/desktop-ui
- **Symptoms**: WPF project fails to compile with 80 CS0246 errors like "The type or namespace name 'ShortcutInvokedEventArgs' could not be found", "The type or namespace name 'ApiClientService' could not be found". NotificationService fails with CS0535 "does not implement interface member".
- **Root cause**: GlobalUsings.cs was updated (commit d9b43dc) to remove `global using MarketDataCollector.Ui.Services.Services;` to avoid namespace conflicts with WPF-specific services. Files that reference types from this namespace now need explicit using directives.
- **Prevention checklist**:
  - [ ] After modifying GlobalUsings.cs, verify all files in the project still compile
  - [ ] When removing global usings, search for all usages of types from that namespace
  - [ ] Add explicit using directives to files that need removed global types
  - [ ] For WPF files: add `using MarketDataCollector.Wpf.Services;` for WPF services
  - [ ] For shared types: add `using UiServices = MarketDataCollector.Ui.Services.Services;` and type aliases
  - [ ] Handle type ambiguities (e.g., NotificationType) with explicit aliases
  - [ ] Test both local and CI builds after GlobalUsings changes
- **Verification commands**:
  - `dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj`
  - `grep -r "using MarketDataCollector.Wpf.Services;" src/MarketDataCollector.Wpf/Views/`
  - `grep -r "using UiServices" src/MarketDataCollector.Wpf/Services/`
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21959216554/job/63431802067
- **Status**: fixed (commit 5ea62c8)

### AI-20260213-nullable-value-property-misuse
- **Area**: build/C#/nullable types
- **Symptoms**: Build fails with CS1061 errors: "'double' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'double' could be found". This occurs when accessing `.Value` on nullable value types after using the null-forgiving operator (`!`), or when using `out var` with generic methods that return `T?` where the compiler fails to properly infer the nullable type.
- **Root cause**: When `TryGetFromNewest` is called on a `CircularBuffer<double>`, the `out T? value` parameter becomes `out double? value`, making `fromValue` and `toValue` of type `double?` (nullable double). Two variants of this issue:
  1. **Original (commit 1e2ea1d)**: Code incorrectly used `fromValue!.Value` where the null-forgiving operator `!` suppresses nullable warnings but doesn't change the type. The compiler then sees `.Value` being accessed on what it thinks is a `double` (due to `!`), causing the error since `double` doesn't have a `.Value` property.
  2. **Variant (commit 5756479)**: Code used `out var fromValue` and the Windows C# compiler failed to properly infer the nullable type from generic `out T?` parameter, treating it as `double` instead of `double?`. Using explicit `out double?` ensures consistent type inference across compiler versions.
- **Prevention checklist**:
  - [ ] Understand that `T?` on value types creates a nullable wrapper (e.g., `double?` is `Nullable<double>`)
  - [ ] When using `out var` with generic methods that return `T?`, use explicit type `out double?` instead of `var` to ensure cross-platform compiler consistency
  - [ ] Never combine null-forgiving operator `!` with `.Value` accessor on nullable value types
  - [ ] Access `.Value` on nullable value types directly (e.g., `nullableDouble.Value`) without null-forgiving operator
  - [ ] Use null-forgiving operator (`!`) only on reference types or when you want to suppress nullable warnings, not to change types
  - [ ] After Try* pattern methods that return bool, the out parameter is guaranteed non-null, so `.Value` access is safe
  - [ ] Test builds on both Linux and Windows after any changes to nullable type handling
- **Verification commands**:
  - `dotnet build src/MarketDataCollector.Ui.Services/MarketDataCollector.Ui.Services.csproj -c Release`
  - `dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release --no-restore -p:TargetFramework=net9.0-windows`
  - `grep -n '\!\.Value' src/MarketDataCollector.Ui.Services/Collections/CircularBuffer.cs` (should return no matches)
  - `grep -n 'out var.*Value' src/MarketDataCollector.Ui.Services/Collections/CircularBuffer.cs` (should return no matches in methods using .Value)
- **Source issues**: 
  - https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21988186038/job/63527798918#step:5:1 (original)
  - https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21996212289/job/63556782046 (variant)
- **Status**: fixed (commits 1e2ea1d, 5756479)
