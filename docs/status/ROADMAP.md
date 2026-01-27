# Market Data Collector - Roadmap

**Version:** 1.0.0 (repository snapshot)
**Last Updated:** 2026-01-27
**Status:** Active development

This document provides the current focus areas and near-term goals. The full backlog is maintained in the issue tracker.

---

## Current Focus Areas

1. **Provider completeness**
   - Finish Polygon streaming WebSocket parsing and message routing
   - Document IBAPI build steps and validate behavior with IBAPI builds
   - Validate NYSE and StockSharp integrations with live credentials

2. **Stability & correctness**
   - Expand automated tests for backfill, storage, and pipeline behavior
   - Harden configuration validation and error reporting

3. **Operational readiness**
   - Improve deployment guidance (Docker, native .NET, Windows UWP)
   - Clarify monitoring and alerting workflows

---

## Near-Term Goals

- Improve provider status reporting in `--detect-providers`
- Expand backfill scheduling UX in the dashboard and UWP app
- Document supported HTTP endpoints and response schemas

---

## Longer-Term Goals

- Add authentication/authorization to HTTP endpoints
- Add additional storage sinks (cloud/object storage)
- Extend export formats and pipeline transforms

---

## Notes

- Use `docs/status/production-status.md` for implementation status details.
- Use `docs/status/CHANGELOG.md` for the current snapshot summary.
