# Desktop Development Workflow

Use these commands for fast desktop-focused iteration.

## Bootstrap

Run environment and smoke checks:

```bash
make desktop-dev-bootstrap
```

On Windows this restores and smoke-builds WPF and UWP (legacy). On non-Windows it validates what can run locally and prints guidance.

## Fast command set

```bash
make build-wpf
make build-uwp
make test-desktop-services
make uwp-xaml-diagnose
```

## When to run what

- WPF changes: `make build-wpf` + `make test-desktop-services`
- UWP changes: `make build-uwp` + `make uwp-xaml-diagnose` + `make test-desktop-services`
- Shared services changes: run all of the above on Windows when possible
