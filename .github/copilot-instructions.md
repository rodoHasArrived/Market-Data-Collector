# Copilot Repository Instructions

Use these instructions for all tasks in this repository to improve quality, reliability, and review speed.

## 1) Prefer well-scoped tasks

When working from an issue or prompt, treat it as a strict implementation contract:

- Restate the exact problem being solved.
- Confirm acceptance criteria before coding.
- Limit changes to the smallest file set that satisfies the task.
- Call out assumptions when requirements are ambiguous.

If requirements are unclear, propose concrete acceptance criteria in the PR body.

## 2) Choose tasks appropriate for an agent

Good fits:

- Bug fixes with reproducible symptoms.
- Targeted UI adjustments.
- Test coverage improvements.
- Documentation updates.
- Refactors with clear boundaries.

Escalate or avoid autonomous changes for:

- Security-sensitive or auth-critical logic.
- Broad architectural redesigns.
- High-risk production incident work.
- Ambiguous tasks without verifiable outcomes.

## 3) Quality bar for every change

Always do the following before opening a PR:

1. Read `docs/ai/ai-known-errors.md` and apply relevant prevention checks.
2. Restore/build with Windows targeting enabled on non-Windows systems.
3. Run tests relevant to touched code.
4. Update docs when behavior, interfaces, or workflows change.
5. Keep PR title/body in sync with final implemented behavior.

## 4) Build and test commands

Use the fastest command set that validates your change:

```bash
dotnet restore MarketDataCollector.sln /p:EnableWindowsTargeting=true
dotnet build MarketDataCollector.sln -c Release --no-restore /p:EnableWindowsTargeting=true
```

Common targeted test commands:

```bash
dotnet test tests/MarketDataCollector.Tests/MarketDataCollector.Tests.csproj -c Release /p:EnableWindowsTargeting=true
dotnet test tests/MarketDataCollector.FSharp.Tests/MarketDataCollector.FSharp.Tests.fsproj -c Release /p:EnableWindowsTargeting=true
```

## 5) Response quality expectations

- Explain *what* changed and *why*.
- Mention risks, tradeoffs, and follow-up items.
- Include exact validation commands and outcomes.
- Keep edits consistent with existing architecture and naming.

## 6) Path-specific instructions

Also follow any matching files under `.github/instructions/**/*.instructions.md` for language-, path-, and test-specific guidance.
