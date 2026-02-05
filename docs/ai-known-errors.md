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
  - `test -f docs/ai-known-errors.md`
  - `rg "AI-" docs/ai-known-errors.md`
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
