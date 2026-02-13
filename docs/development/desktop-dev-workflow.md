# Desktop Development Workflow

> **Note:** This content has been consolidated into the [Desktop Testing Guide](desktop-testing-guide.md).
> This file is kept as a redirect for existing links.

**Last Updated:** 2026-02-12

Use these commands for fast desktop-focused iteration.

## Bootstrap

Run environment and smoke checks:

```bash
make desktop-dev-bootstrap
```

On Windows this restores and smoke-builds WPF and UWP (legacy). On non-Windows it validates what can run locally and prints guidance.

## Fast Command Set

```bash
make build-wpf
make build-uwp
make test-desktop-services
make uwp-xaml-diagnose
```

## When to Run What

| Change Type | Commands |
|-------------|----------|
| WPF changes | `make build-wpf` + `make test-desktop-services` |
| UWP changes | `make build-uwp` + `make uwp-xaml-diagnose` + `make test-desktop-services` |
| Shared services | Run all of the above on Windows when possible |

See the [Desktop Testing Guide](desktop-testing-guide.md) for full details and the
[Desktop Support Policy](policies/desktop-support-policy.md) for required validation by change type.
