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
