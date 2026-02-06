# Market Data Collector - Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-02-06
**Status:** Development / Pilot Ready

This document provides the current focus areas and near-term goals. The full backlog is maintained in the issue tracker.

---

## Current Focus Areas

1. **Provider completeness**
   - Finish Polygon streaming WebSocket parsing and message routing
   - Document IBAPI build steps and validate behavior with IBAPI builds
   - Validate NYSE and StockSharp integrations with live credentials

2. **Stability & correctness**
   - Expand automated tests for backfill, storage, and pipeline behavior (currently 85 test files)
   - Harden configuration validation and error reporting
   - Add HTTP API endpoint integration tests

3. **Operational readiness**
   - Improve deployment guidance (Docker, native .NET, WPF desktop, UWP legacy)
   - Clarify monitoring and alerting workflows
   - Add OpenAPI/Swagger documentation for HTTP endpoints

---

## Near-Term Goals

- ~~Improve provider status reporting in `--detect-providers`~~ ✅ (Added NYSE Direct and Nasdaq Data Link)
- Expand backfill scheduling UX in the dashboard and desktop apps
- Document supported HTTP endpoints and response schemas
- Complete WPF desktop app feature parity with UWP

---

## Longer-Term Goals

- Add authentication/authorization to HTTP endpoints
- Add additional storage sinks (cloud/object storage)
- Extend export formats and pipeline transforms
- Add OpenAPI/Swagger specification for REST API

---

## Notes

- Use `docs/status/production-status.md` for implementation status details.
- Use `docs/status/CHANGELOG.md` for the current snapshot summary.
