# Desktop Platform Developer Experience: High-Value Improvements

## Goal

## Implementation status (current)

- ✅ Desktop dev bootstrap script: `scripts/dev/desktop-dev.ps1`
- ✅ Focused desktop Make targets: `build-wpf`, `build-uwp`, `test-desktop-services`, `desktop-dev-bootstrap`, `uwp-xaml-diagnose`
- ✅ UWP XAML diagnostics helper: `scripts/dev/diagnose-uwp-xaml.ps1`
- ✅ Desktop support policy: `docs/development/policies/desktop-support-policy.md`
- ✅ Desktop PR checklist template: `.github/pull_request_template_desktop.md`
- ✅ Desktop workflow doc: `docs/development/desktop-dev-workflow.md`

Improve day-to-day development velocity and confidence for desktop contributors (WPF primary, UWP legacy) by reducing build friction, tightening feedback loops, and standardizing local workflows.

## Priority 1 — Fastest ROI

### 1) Add a dedicated **Desktop Dev Bootstrap** script (`scripts/dev/desktop-dev.ps1`)

**Problem**
- Desktop setup requirements are spread across docs (SDK, Windows tooling, backend dependency, build commands), which increases onboarding time and drift.

**Improvement**
- Provide a single script that:
  - validates .NET SDK + Windows targeting packs,
  - validates Visual Studio Build Tools/XAML tooling presence,
  - restores desktop projects,
  - runs a smoke build for WPF (and optional UWP),
  - prints actionable fixes when checks fail.

**Why high value**
- Eliminates environment guesswork and shortens first-success time for new contributors.

### 2) Add **focused desktop test entrypoints** in `Makefile` and scripts

**Problem**
- The repository has broad build/test workflows, but desktop contributors need a minimal command set for UI-adjacent services and desktop-specific checks.

**Improvement**
- Add commands like:
  - `make test-desktop-services`
  - `make build-wpf`
  - `make build-uwp` (legacy/optional)
- Point these to narrow project/test scopes to keep cycle time low.

**Why high value**
- Faster local iteration and fewer full-solution builds for desktop-only changes.

### 3) Introduce a **WPF service/unit test baseline** (especially for singleton services)

**Problem**
- WPF notes call out unit tests as planned, but the current guidance is primarily manual testing.

**Improvement**
- Add initial tests for the most central services:
  - `NavigationService`
  - `ConfigService`
  - `StatusService`
  - `ConnectionService`
- Add seam interfaces/time providers where needed to make singleton-heavy code testable.

**Why high value**
- Catches regressions in non-visual behavior without launching UI and improves refactor safety.

## Priority 2 — CI and Reliability Improvements

### 4) Split desktop CI into **PR-fast** and **full desktop release** lanes

**Problem**
- Desktop workflows combine many responsibilities, while contributors often only need fast validation for changed areas.

**Improvement**
- Keep a lightweight PR lane (restore/build/test for touched desktop project + service tests).
- Keep full matrix publish/sign/package in a separate full lane (manual/tag/main).
- Add path-based skip logic for unchanged desktop areas.

**Why high value**
- Reduces CI wait time and preserves high assurance when shipping.

### 5) Publish and enforce a **desktop support policy** (WPF = primary, UWP = compatibility)

**Problem**
- The repo documents WPF as recommended and UWP as legacy, but contribution expectations are not expressed as a concise policy.

**Improvement**
- Add a short policy document defining:
  - required checks for WPF-only changes,
  - when UWP must also pass,
  - ownership/maintenance expectations for legacy UWP code.

**Why high value**
- Prevents over-testing or under-testing and clarifies review expectations.

### 6) Add an **automated XAML diagnostics helper** for UWP failures

**Problem**
- Historical XAML compiler failures are opaque and expensive to diagnose.

**Improvement**
- Provide a script that runs preflight checks against known failure modes:
  - WindowsAppSDK version sanity,
  - target framework/platform settings,
  - intermediate path pitfalls,
  - XAML parse validation.

**Why high value**
- Converts recurring “tribal knowledge” into deterministic checks.

## Priority 3 — Productivity Quality-of-Life

### 7) Establish **UI state fixtures** for repeatable desktop debugging

**Problem**
- Contributors depend on live backend status at `http://localhost:8080`, which slows or blocks UI work.

**Improvement**
- Add local fixture mode for `StatusService`/API services with canned payloads.
- Provide one command to boot fixture mode for dashboards/settings pages.

**Why high value**
- Enables offline UI development and deterministic bug reproduction.

### 8) Add a **desktop architecture map** with dependency boundaries

**Problem**
- Shared UI services + platform-specific services are documented but not captured as a concise contributor map.

**Improvement**
- Add one-page map showing:
  - `Ui.Services` (shared core),
  - WPF-specific layer,
  - UWP-specific layer,
  - forbidden dependency directions.

**Why high value**
- Reduces accidental cross-layer coupling and speeds code reviews.

### 9) Create a **PR checklist for desktop changes**

**Problem**
- Repeated review feedback for event unsubscription, navigation registration, configuration persistence, and graceful shutdown checks.

**Improvement**
- Add `.github/pull_request_template_desktop.md` with required validations for desktop-affecting PRs.

**Why high value**
- Standardizes quality gates and lowers review iteration count.

## Suggested implementation order (first 30 days)

1. Desktop bootstrap script + Make targets.
2. WPF service test baseline (first 10–15 tests).
3. CI split into fast and full lanes.
4. Desktop support policy + desktop PR checklist.
5. UWP XAML diagnostics helper.

## Success metrics

- New contributor first successful WPF run time drops below 20 minutes.
- Desktop PR median CI time reduced by at least 30%.
- Service-level regression bugs caught pre-merge (increasing test-detected vs. manual-detected issues).
- Fewer “cannot reproduce” desktop issues due to fixture mode adoption.

## High-value improvements snapshot (desktop ease-of-development)

For contributors focused on improving day-to-day desktop development speed, the highest-value opportunities are:

1. **One-command local desktop loop (`make desktop-loop`)**
   - Chain restore → targeted desktop build → service tests → optional app launch with fixture mode.
   - Keep this command under ~90 seconds for no-op and small-change scenarios.

2. **Deterministic fixture-first UI development by default**
   - Make fixture mode a first-class option in WPF startup profiles.
   - Include canned scenarios for disconnected backend, stale config, throttled provider, and partial data states.

3. **Desktop-focused architecture and dependency guardrails**
   - Add a simple dependency linter/check to prevent platform layers from leaking into shared `Ui.Services`.
   - Pair with architecture tests that fail fast when boundary violations occur.

4. **Fast-path CI tuned for desktop contributors**
   - Run only impacted desktop projects and service tests in PR workflows.
   - Gate expensive packaging/signing jobs behind labels, tags, or main branch merges.

5. **Golden-path diagnostics for historical desktop failures**
   - Keep expanding automated checks for the most common UWP/XAML and configuration drift failures.
   - Emit fix suggestions that map directly to docs and exact project files.

### Why these are the highest leverage now

- They reduce context switching and setup overhead for every contributor, every day.
- They improve confidence without requiring full application/manual validation cycles.
- They preserve support for legacy UWP while optimizing the main WPF workflow.

## Evidence in current repository

- WPF is the recommended desktop path, with UWP legacy support and explicit migration rationale.
- WPF implementation notes identify unit testing and persistence work as future enhancements.
- Desktop CI is consolidated and feature-rich, indicating opportunity for fast-path separation.
- UWP/XAML compiler failures are documented with recurring diagnostics and mitigation steps.
