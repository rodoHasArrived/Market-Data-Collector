# ADR-011: Centralized Configuration and Credential Management

**Status:** Accepted
**Date:** 2026-02-02
**Deciders:** Core Team

## Context

The system runs across multiple host types (console, web, desktop) and integrates with numerous providers. Configuration and credentials are needed by:

- Provider clients (API keys, OAuth tokens, endpoints)
- Storage and archival pipelines (paths, compression settings)
- Monitoring and operational scheduling (thresholds, timeouts)
- UI and automation workflows (runtime overrides)

Historically, configuration was fetched from a mix of environment variables, appsettings files, and per-service options. Credentials were resolved via ad-hoc environment lookups or provider-specific helper classes. This caused:

- Inconsistent validation rules and missing defaults
- Difficult auditing of required settings
- Divergent host behavior when configuration paths changed
- Duplicate logic for environment overrides and credential caching

## Decision

Adopt a centralized configuration and credential management model:

1. **Unified configuration access** via `IConfigurationProvider` for all components.
2. **Single configuration store** (`ConfigStore`) for load/save of persisted settings.
3. **Central credential interface** (`ICredentialStore`) to handle provider secrets, caching, and refresh.
4. **Composition-root registration** so every host receives the same configuration/credential stack.

## Implementation Links

<!-- These links are verified by the build process -->

| Component | Location | Purpose |
|-----------|----------|---------|
| Configuration contract | `src/MarketDataCollector/Application/Config/IConfigurationProvider.cs:64` | Typed, validated configuration access |
| Configuration store | `src/MarketDataCollector/Application/Http/ConfigStore.cs:14` | Shared load/save for all hosts |
| Credential contract | `src/MarketDataCollector/Application/Credentials/ICredentialStore.cs:76` | Unified credential resolution |
| Composition registration | `src/MarketDataCollector/Application/Composition/ServiceCompositionRoot.cs:63` | Ensures consistent wiring |

## Rationale

Centralizing configuration and credential handling ensures:

- **Consistency**: All hosts resolve settings the same way.
- **Observability**: Metadata surfaces required settings and their sources.
- **Security**: Credential loading is constrained to a single entry point.
- **Testability**: Configuration can be injected, mocked, or validated in isolation.

## Alternatives Considered

### Alternative 1: Host-specific configuration services

Each host (console/web/desktop) owns its own configuration and credential logic.

**Pros:**
- Faster initial implementation
- Host-specific customization

**Cons:**
- Divergent behavior and duplicate logic
- Harder to validate or document configuration requirements

**Why rejected:** Inconsistent behavior across hosts is more costly than centralized setup.

### Alternative 2: Provider-specific credential helpers

Each provider implements its own credential resolution logic.

**Pros:**
- Tailored per provider
- Minimal shared abstractions

**Cons:**
- No caching strategy consistency
- Difficult to implement cross-cutting validation and reporting

**Why rejected:** Centralized credential management reduces duplication and supports unified validation.

## Consequences

### Positive

- Configuration and credentials are validated and discoverable.
- Hosts can safely override settings at runtime.
- Provider onboarding includes documented credential metadata.

### Negative

- Added abstraction layer to maintain.
- Composition root changes require coordination when extending configuration sources.

### Neutral

- Legacy configuration access may remain during gradual migration.

## Compliance

### Code Contracts

Components accessing configuration or credentials should use the centralized interfaces:

```csharp
public interface IConfigurationProvider
{
    T Get<T>(string section, string key, T defaultValue = default!);
    ConfigurationValidationResult Validate();
}

public interface ICredentialStore
{
    Task<CredentialResult> GetCredentialAsync(string provider, string key, CancellationToken ct = default);
}
```

### Runtime Verification

- Build-time verification via `make verify-adrs`

## References

- [Configuration Guide](../guides/configuration.md)
- [Project Context](../development/project-context.md)

---

*Last Updated: 2026-02-02*
