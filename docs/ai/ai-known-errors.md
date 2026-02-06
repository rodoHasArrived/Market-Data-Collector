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

### AI-20260206-missing-aspnetcore-mvc-testing
- **Area**: tests/dependencies
- **Symptoms**: Test project fails to compile with errors:
  - `error CS0234: The type or namespace name 'TestHost' does not exist in the namespace 'Microsoft.AspNetCore'`
  - `error CS0246: The type or namespace name 'IAsyncLifetime' could not be found`
- **Root cause**: The test project `MarketDataCollector.Tests` uses ASP.NET Core integration testing infrastructure (`TestServer`, `WebApplicationFactory`) but was missing the required `Microsoft.AspNetCore.Mvc.Testing` package. Additionally, the test file `EndpointTestFixture.cs` was missing the `using Xunit;` directive needed for `IAsyncLifetime`.
- **Prevention checklist**:
  - [ ] When adding integration tests that use ASP.NET Core `TestServer`, ensure `Microsoft.AspNetCore.Mvc.Testing` package is referenced
  - [ ] Add package version to `Directory.Packages.props` in the "ASP.NET Core" section matching existing ASP.NET package versions
  - [ ] Add package reference to test project without version number (CPM)
  - [ ] Ensure test files using xUnit fixtures include `using Xunit;` directive
  - [ ] Build and run tests after adding new test infrastructure: `dotnet test tests/MarketDataCollector.Tests`
- **Verification commands**:
  - `dotnet build tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj -c Release`
  - `dotnet test tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj -c Release`
  - `grep "Microsoft.AspNetCore.Mvc.Testing" Directory.Packages.props`
  - `grep "Microsoft.AspNetCore.Mvc.Testing" tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj`
- **Source issue**: https://github.com/rodoHasArrived/Market-Data-Collector/actions/runs/21769809198/job/62814640086
- **Status**: fixed
