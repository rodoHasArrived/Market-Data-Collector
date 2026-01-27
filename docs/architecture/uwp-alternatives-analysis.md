# UWP/WinUI 3 Application: Technology Alternatives Analysis

**Date:** 2026-01-27
**Status:** Analysis Document
**Scope:** Desktop application technology stack evaluation

---

## Executive Summary

The current Windows desktop application (`MarketDataCollector.Uwp`) uses **WinUI 3 / Windows App SDK** - the successor to UWP. While feature-rich (43 pages, 60 services), this technology choice presents significant constraints. This document evaluates alternative approaches that could provide cross-platform support, simpler builds, and better maintainability.

**Recommendation:** Migrate to **Avalonia UI** for a cross-platform native experience, or **Blazor Hybrid** for maximum code sharing with potential future web deployment.

---

## Current Architecture Analysis

### What We Have

| Metric | Value |
|--------|-------|
| Framework | WinUI 3 (Windows App SDK 1.7.x) |
| Target | .NET 9.0-windows10.0.19041.0 |
| Source Files | 189 (.xaml + .cs) |
| Pages | 43 XAML views |
| Services | 60 specialized services |
| ViewModels | 5 MVVM view models |
| Custom Controls | 8 reusable controls |

### Current Architecture Strengths

1. **Native Windows Performance** - Direct WinRT interop, hardware acceleration
2. **Modern UI** - Fluent Design System with Windows 11 aesthetics
3. **Windows Integration** - Toast notifications, live tiles, system themes
4. **MVVM Architecture** - Clean separation using CommunityToolkit.Mvvm
5. **Resilient HTTP** - Polly-based retry/circuit breaker patterns
6. **Background Tasks** - Windows background task infrastructure

### Current Architecture Pain Points

| Issue | Impact |
|-------|--------|
| **Windows-Only** | No macOS/Linux support; excludes developer segments |
| **Complex Build** | Requires Windows SDK, special MSBuild targets |
| **Assembly Restrictions** | Cannot reference `MarketDataCollector.Contracts` due to XAML compiler |
| **MSIX Packaging** | Complex deployment and signing requirements |
| **SDK Version Lock** | Tied to specific Windows SDK versions |
| **Cross-Platform CI** | Builds fail on Linux/macOS runners without stubs |
| **Model Duplication** | Must mirror DTOs locally instead of sharing |

---

## Alternative Technologies Evaluated

### 1. Avalonia UI

**Overview:** Cross-platform .NET UI framework with XAML syntax, inspired by WPF.

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows, macOS, Linux, iOS, Android, WebAssembly, Embedded |
| **UI Paradigm** | XAML + MVVM (very similar to current WinUI) |
| **Performance** | Native rendering via Skia, excellent performance |
| **Maturity** | Stable (v11.x), used by JetBrains, Warp terminal |
| **Migration Effort** | Medium - XAML syntax differences, but concepts transfer |

**Pros:**
- Nearly identical XAML/MVVM patterns to current codebase
- True cross-platform with native look-and-feel options
- Can share `MarketDataCollector.Contracts` assembly directly
- Active community, commercial support available
- Single codebase for all platforms
- Works with existing CommunityToolkit.Mvvm

**Cons:**
- No native Windows notification integration (needs community packages)
- Fluent Design requires explicit theming
- Smaller ecosystem than WPF/WinUI

**Migration Path:**
1. Convert XAML namespaces from WinUI to Avalonia
2. Replace WinUI-specific controls with Avalonia equivalents
3. Swap `Microsoft.WindowsAppSDK` for `Avalonia.Desktop`
4. Add platform-specific notification plugins
5. Test on each target platform

**Recommendation:** **Strongly Recommended** - Best balance of code reuse and cross-platform support.

---

### 2. Blazor Hybrid (MAUI Blazor)

**Overview:** Web technologies (Razor/HTML/CSS) hosted in native container via .NET MAUI.

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows, macOS, iOS, Android (Linux via community) |
| **UI Paradigm** | Razor components, HTML/CSS styling |
| **Performance** | Good - native container with WebView rendering |
| **Maturity** | Stable, Microsoft-backed |
| **Migration Effort** | High - complete UI rewrite from XAML to Razor |

**Pros:**
- Share code with potential web version of dashboard
- Massive ecosystem of web UI libraries (Tailwind, MudBlazor, etc.)
- Familiar to web developers
- Hot reload development experience
- C# throughout (no JavaScript required)
- Full access to .NET libraries and shared contracts

**Cons:**
- Complete rewrite of all 43 pages
- WebView overhead vs native rendering
- MAUI workload installation required
- Linux support is community-maintained

**Migration Path:**
1. Create new MAUI Blazor project
2. Define Blazor component equivalents for each page
3. Port services (mostly reusable as-is)
4. Implement responsive CSS-based layouts
5. Add platform-specific features via MAUI APIs

**Recommendation:** **Recommended** if future web dashboard expansion is planned.

---

### 3. .NET MAUI (Native)

**Overview:** Microsoft's official cross-platform framework using .NET and XAML.

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows, macOS, iOS, Android |
| **UI Paradigm** | XAML + MVVM |
| **Performance** | Native controls per platform |
| **Maturity** | Stable but historically buggy |
| **Migration Effort** | Medium-High - different XAML dialect |

**Pros:**
- Microsoft-supported long-term
- XAML syntax (familiar to current codebase)
- Native controls on each platform
- Visual Studio tooling support
- Shares MVVM patterns

**Cons:**
- **No Linux support** (major gap for developers)
- Historical stability issues (memory leaks, rendering bugs)
- Slower release cadence than community projects
- Platform abstractions can be leaky
- XAML differs from WinUI XAML

**Recommendation:** **Not Recommended** - Linux exclusion is a dealbreaker; stability concerns.

---

### 4. Uno Platform

**Overview:** Cross-platform framework using UWP/WinUI XAML syntax.

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows, macOS, Linux, iOS, Android, WebAssembly |
| **UI Paradigm** | WinUI XAML (almost 1:1 with current code) |
| **Performance** | Skia rendering (similar to Avalonia) |
| **Maturity** | Stable, enterprise-focused |
| **Migration Effort** | Low - designed for WinUI migration |

**Pros:**
- **Highest code compatibility** with current WinUI XAML
- Same control names, properties, and behaviors
- WebAssembly support for web deployment
- Figma design integration
- Hot reload support

**Cons:**
- Smaller community than Avalonia
- Commercial focus (free tier available)
- Some WinUI features require platform-specific code
- Build times can be slow

**Migration Path:**
1. Add Uno Platform NuGet packages
2. Create platform head projects (minimal changes)
3. Adjust Windows-specific APIs with conditionals
4. Test cross-platform rendering

**Recommendation:** **Recommended** if minimizing migration effort is the priority.

---

### 5. Electron + React/Vue

**Overview:** Web technologies in Chromium container (like VS Code, Slack).

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows, macOS, Linux |
| **UI Paradigm** | HTML/CSS/JavaScript + Framework |
| **Performance** | Moderate (Chromium overhead, ~150MB+ RAM) |
| **Maturity** | Very mature, massive ecosystem |
| **Migration Effort** | Very High - complete rewrite |

**Pros:**
- Huge ecosystem of UI components
- Familiar to web developers
- Same code runs in browser
- Excellent debugging tools

**Cons:**
- **Large bundle size** (~150-200MB minimum)
- **High memory usage** (Chromium per app)
- Complete rewrite from C# to JavaScript/TypeScript
- Cannot share existing service code
- Two technology stacks to maintain

**Recommendation:** **Not Recommended** - resource overhead and rewrite cost too high.

---

### 6. Tauri

**Overview:** Lightweight Electron alternative using Rust + system WebView.

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows, macOS, Linux |
| **UI Paradigm** | HTML/CSS/JavaScript (any framework) |
| **Performance** | Excellent - small binaries (~10MB), low RAM |
| **Maturity** | v2.x stable |
| **Migration Effort** | Very High - complete rewrite |

**Pros:**
- Tiny binaries vs Electron
- Uses system WebView (no bundled Chromium)
- Security-focused architecture
- Rust backend for performance-critical code

**Cons:**
- Complete frontend rewrite required
- Backend in Rust (new language to maintain)
- Cannot share C# service logic
- Less mature than Electron

**Recommendation:** **Not Recommended** - requires Rust expertise; no C# code sharing.

---

### 7. WPF (Windows Presentation Foundation)

**Overview:** Traditional Windows desktop framework.

| Aspect | Assessment |
|--------|------------|
| **Platforms** | Windows only |
| **UI Paradigm** | XAML + MVVM |
| **Performance** | Excellent, mature |
| **Maturity** | Very mature (15+ years) |
| **Migration Effort** | Low-Medium - similar XAML |

**Pros:**
- Stable, well-understood
- Similar XAML to WinUI
- No Windows SDK version issues
- Large ecosystem of controls
- Can reference any .NET assembly

**Cons:**
- **Windows-only** (same constraint as current)
- Older visual style (not Fluent by default)
- No active feature development
- Considered legacy by some

**Recommendation:** **Conditionally Recommended** - only if Windows-only is acceptable.

---

## Comparison Matrix

| Criteria | WinUI 3 (Current) | Avalonia | Blazor Hybrid | MAUI | Uno Platform | Electron | Tauri | WPF |
|----------|-------------------|----------|---------------|------|--------------|----------|-------|-----|
| **Windows** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **macOS** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Linux** | ❌ | ✅ | ⚠️ | ❌ | ✅ | ✅ | ✅ | ❌ |
| **Web** | ❌ | ✅ | ⚠️ | ❌ | ✅ | ✅ | ✅ | ❌ |
| **XAML Reuse** | N/A | 70% | 0% | 50% | 90% | 0% | 0% | 60% |
| **C# Services Reuse** | N/A | 100% | 100% | 100% | 100% | 0% | 0% | 100% |
| **Binary Size** | 50MB | 30MB | 80MB | 80MB | 40MB | 200MB | 10MB | 30MB |
| **Memory Usage** | Low | Low | Medium | Medium | Low | High | Low | Low |
| **Migration Effort** | N/A | Medium | High | Medium-High | Low | Very High | Very High | Low-Medium |
| **Maturity** | High | High | High | Medium | High | Very High | Medium | Very High |

Legend: ✅ Full Support | ⚠️ Partial/Community | ❌ No Support

---

## Recommended Approach

### Primary Recommendation: Avalonia UI

**Rationale:**
1. **Cross-Platform** - Covers Windows, macOS, Linux with single codebase
2. **XAML Familiarity** - Existing team can leverage WinUI/WPF knowledge
3. **Code Sharing** - Can directly reference `MarketDataCollector.Contracts`
4. **Performance** - Native Skia rendering, low resource usage
5. **Community** - Active, growing ecosystem with commercial support
6. **Migration Path** - Incremental, page-by-page migration possible

### Alternative: Blazor Hybrid

Choose if:
- Future web dashboard is a priority
- Team has web development expertise
- Willing to invest in complete UI rewrite
- Want to consolidate web and desktop UIs

### Alternative: Uno Platform

Choose if:
- Minimizing migration effort is top priority
- Need maximum WinUI XAML compatibility
- WebAssembly deployment is desired
- Comfortable with commercial framework

---

## Migration Strategy for Avalonia

### Phase 1: Foundation (2-3 weeks)
1. Create new Avalonia project structure
2. Set up shared service layer (extract from UWP services)
3. Define common styles/themes
4. Implement navigation infrastructure
5. Port core models (or reference Contracts directly)

### Phase 2: Core Pages (4-6 weeks)
1. DashboardPage - real-time metrics
2. SymbolsPage - symbol management
3. BackfillPage - historical data
4. SettingsPage - configuration
5. DataQualityPage - monitoring

### Phase 3: Advanced Features (4-6 weeks)
1. Remaining 38 pages
2. Custom controls migration
3. Background task equivalent
4. Platform-specific notifications
5. Theme switching

### Phase 4: Polish (2-3 weeks)
1. Cross-platform testing
2. Performance optimization
3. Installer/packaging for each platform
4. Documentation update

**Total Estimate:** 12-18 weeks for full migration

---

## Appendix: Current Windows-Specific Features

Features requiring platform abstraction or alternative implementation:

| Feature | Current Implementation | Avalonia Equivalent |
|---------|----------------------|---------------------|
| Toast Notifications | `AppNotificationManager` | Desktop.Notifications NuGet |
| Theme Detection | Windows theme API | `Application.ActualThemeVariant` |
| Background Tasks | Windows task scheduler | `System.Timers` + persistence |
| System Tray | Windows system tray | Avalonia.Desktop tray icon |
| Keyboard Shortcuts | WinUI accelerators | Avalonia KeyBindings |
| File Dialogs | Windows dialogs | Avalonia.Dialogs |

---

## Conclusion

The current WinUI 3 application is well-architected but platform-limited. **Avalonia UI** offers the best path forward for cross-platform support while preserving the team's XAML/MVVM expertise and enabling full code sharing with the core project. The migration effort is moderate and can be done incrementally.

If a complete technology shift is acceptable and web convergence is desired, **Blazor Hybrid** provides a compelling alternative with the largest ecosystem of UI components.
