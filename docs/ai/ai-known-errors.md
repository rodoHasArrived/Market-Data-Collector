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

### AI-20260206-wpf-missing-using-directives
- **Area**: build/WPF/C#
- **Symptoms**: WPF build fails with CS0246 errors: "The type or namespace name 'X' could not be found (are you missing a using directive or an assembly reference?)". Common missing types: `HttpClient`, `HttpResponseMessage`, `ConnectionState`, `BackfillApiService`, `ExportFormat`, `ZstdSharp`.
- **Root cause**: When adding new code to the WPF project, agents forget that WPF doesn't share assemblies with the main project or UWP project. Types must either be imported via `using` directives, defined locally, or included via shared source files from Contracts. Key patterns:
  - `System.Net.Http` namespace must be explicitly imported (not just `System.Net.Http.Headers`)
  - Types from `MarketDataCollector.Wpf.Contracts` (e.g., `ConnectionState`) need explicit `using` directives
  - Types from UWP (e.g., `BackfillApiService`) don't exist in WPF and must be recreated
  - Types from the main project (e.g., `ExportFormat`) aren't accessible and must be defined locally
  - NuGet packages used in code (e.g., `ZstdSharp`) must be added to the WPF `.csproj`
- **Prevention checklist**:
  - [ ] When adding `using System.Net.Http.Headers;`, also add `using System.Net.Http;` if `HttpClient` or `HttpResponseMessage` are used
  - [ ] When referencing types from `Contracts/` interfaces, add the appropriate `using MarketDataCollector.Wpf.Contracts;`
  - [ ] When porting code from UWP to WPF, verify all service classes exist in the WPF project
  - [ ] When using types from the main `MarketDataCollector` project, define local equivalents since WPF cannot reference the main project directly
  - [ ] When adding `using` for a NuGet package, ensure the package is in the WPF `.csproj` (without `Version` attribute per CPM)
  - [ ] Avoid namespace-qualifying types that are in the same file (e.g., use `MyType` not `Services.MyType` when `MyType` is defined in the current namespace)
- **Verification commands**:
  - `dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release`
- **Source issue**: CI desktop-builds workflow failure
- **Status**: fixed
