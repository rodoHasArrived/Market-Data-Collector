# Desktop Support Policy

## Scope

This policy defines contribution and validation expectations for desktop surfaces:

- `src/MarketDataCollector.Wpf` (**primary desktop surface**)
- `src/MarketDataCollector.Uwp` (**legacy compatibility surface**)
- `src/MarketDataCollector.Ui.Services` (shared services used by desktop clients)

## Support Levels

### 1) WPF (Primary)

WPF is the default and preferred desktop implementation for new features and developer workflow improvements.

Expected for WPF-affecting changes:
- Build validation of WPF project
- Desktop-focused service tests
- Documentation updates when behavior or workflow changes

### 2) UWP (Compatibility)

UWP remains supported for compatibility and existing user workflows, but is not the default target for new UI-first features unless explicitly required.

Expected for UWP-affecting changes:
- Build validation of UWP project on Windows
- Run UWP XAML diagnostic preflight when XAML/build system changes are involved
- Maintain behavior compatibility for shared API contracts used by UWP

## Required checks by change type

### WPF-only change
- `make build-wpf`
- `make test-desktop-services`

### UWP-only change
- `make build-uwp`
- `make uwp-xaml-diagnose`
- `make test-desktop-services`

### Shared desktop services change (`Ui.Services` or shared contracts)
- `make build-wpf`
- `make test-desktop-services`
- `make build-uwp` (recommended on Windows)

## Ownership and maintenance expectations

- New desktop investment should prioritize WPF path quality and iteration speed.
- UWP fixes should focus on stability, compatibility, and release safety.
- Avoid introducing new coupling from shared services into platform-specific UI layers.

---

## Related Documentation

- **Desktop Development:**
  - [Desktop Testing Guide](../desktop-testing-guide.md) - Testing procedures and requirements
  - [Desktop Development Workflow](../desktop-dev-workflow.md) - Quick commands
  - [WPF Implementation Notes](../wpf-implementation-notes.md) - WPF architecture details
  - [UWP to WPF Migration](../uwp-to-wpf-migration.md) - Migration strategy
  - [Desktop Platform Improvements](../desktop-platform-improvements-implementation-guide.md) - Improvement roadmap

- **Architecture and Quality:**
  - [Desktop Architecture Layers](../../architecture/desktop-layers.md) - Layer boundaries
  - [UI Fixture Mode Guide](../ui-fixture-mode-guide.md) - Offline development
  - [Repository Organization Guide](../repository-organization-guide.md) - Code structure

- **Workflows:**
  - [Desktop Builds Workflow](../../../.github/workflows/desktop-builds.yml) - CI configuration
  - [GitHub Actions Summary](../github-actions-summary.md) - CI/CD overview
