# AI Assistant Documentation

This folder contains specialized guides for AI assistants working with the Market Data Collector codebase.

## Claude Guides

Located in `claude/`:

- **[CLAUDE.providers.md](claude/CLAUDE.providers.md)** - Provider implementation guide
- **[CLAUDE.storage.md](claude/CLAUDE.storage.md)** - Storage system guide
- **[CLAUDE.fsharp.md](claude/CLAUDE.fsharp.md)** - F# domain library guide
- **[CLAUDE.testing.md](claude/CLAUDE.testing.md)** - Testing guide
- **[CLAUDE.actions.md](claude/CLAUDE.actions.md)** - GitHub Actions/CI guide

## Copilot Guides

Located in `copilot/`:

- **[instructions.md](copilot/instructions.md)** - GitHub Copilot instructions

## Root Guide

The main `CLAUDE.md` file at the repository root provides the primary AI assistant context.

## AI Error Memory Workflow

- Use `docs/ai-known-errors.md` as the canonical registry of repeated AI mistakes.
- Label GitHub issues with `ai-known-error` to trigger the `AI Known Errors Intake` job in `.github/workflows/documentation.yml`, which opens a PR that records the issue in the registry.
- Include headings in issue bodies for best automation quality: `Area`, `Symptoms`, `Root cause`, `Prevention checklist`, and `Verification commands`.

