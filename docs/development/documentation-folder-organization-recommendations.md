# Documentation Folder Organization Recommendations

**Last Updated:** 2026-03-15  
**Audience:** Maintainers, Contributors

This document proposes practical ways to make the `docs/` folder easier to navigate, maintain, and scale.

---

## Current Pain Points

- The top-level `docs/` directory has many sibling folders, which can make discovery difficult for new contributors.
- Some content categories overlap (`status/`, `evaluations/`, `audits/`, and `archived/`) and can create uncertainty about where new material belongs.
- Multiple diagram locations (`diagrams/` and `uml/`) split related visual assets.
- Long-lived status files and one-off analysis reports are mixed in the same navigation flow.

---

## Recommended Improvements

### 1) Adopt a three-zone layout

Group folders into a simple mental model:

- `docs/product/` — user- and operator-facing guides
- `docs/engineering/` — architecture, development, ADRs, integrations
- `docs/governance/` — status, roadmap, audits, evaluations, security

This helps contributors decide location by audience and purpose first.

### 2) Consolidate duplicate or near-duplicate categories

- Merge `audits/` and `evaluations/` into `governance/reviews/` with subfolders:
  - `architecture/`
  - `providers/`
  - `quality/`
  - `performance/`
- Keep `status/` for active project tracking only (roadmap, changelog, feature inventory).
- Move outdated analyses from active areas to `archived/` quickly.

### 3) Unify visual assets

Choose one diagrams home:

- Preferred: `docs/diagrams/`
- Move `docs/uml/` into `docs/diagrams/uml/`

Also add a small naming convention:

- `{domain}-{view}-{tool}.{ext}`
- Examples:
  - `ingestion-sequence-plantuml.puml`
  - `ingestion-sequence-rendered.svg`

### 4) Add folder-level READMEs with ownership and scope

For every top-level docs subfolder, include:

- Purpose (what belongs here)
- What does **not** belong here
- Canonical examples
- Owner/team
- Review cadence

This reduces drift and lowers onboarding cost.

### 5) Introduce lifecycle tags in front matter

For markdown docs, standardize lightweight metadata using the **bold-field style** already
established in this repository (e.g., `**Status:**`, `**Version:**`, `**Last Updated:**`):

```markdown
**Status:** active | draft | deprecated | archived  
**Owner:** <team-or-alias>  
**Reviewed:** YYYY-MM-DD  
```

Add these fields to the bold metadata block at the top of each document, alongside existing
fields such as `**Version:**`, `**Last Updated:**`, and `**Audience:**`. Do **not** use
YAML front matter (the `---`-fenced format) unless a specific toolchain requires it, as it
is not the current convention in this repository.

Then build a simple script to flag stale docs (e.g., > 180 days since **Reviewed** date).

### 6) Separate generated from hand-authored navigation

Keep generated docs discoverable but isolated:

- Continue using `docs/generated/`
- Add a note in `docs/README.md` that generated pages should not be edited manually
- Add per-file banner in generated docs: `AUTO-GENERATED — DO NOT EDIT`

### 7) Clarify where planning artifacts live

Define one canonical place for ongoing planning:

- `docs/status/` for active plans and progress
- `docs/archived/` for completed planning snapshots

Avoid maintaining parallel “plan” documents in `development/` unless they are implementation guides.

### 8) Add docs linting and structure checks

Automate quality gates in CI:

- Broken relative links
- Missing front matter fields
- Missing folder README
- Files in forbidden locations (based on folder taxonomy)

This keeps organization intact over time.

---

## Suggested Migration Plan (Low Risk)

1. Define and publish folder taxonomy in `docs/README.md`.
2. Consolidate diagrams (`uml/` → `diagrams/uml/`): move all files, leave a stub `docs/uml/README.md` that links to the new location, and do a repo-wide search-and-replace to update every relative link that referenced the old `uml/` path.
3. Add README + ownership metadata to each top-level section.
4. Migrate audits/evaluations into a unified reviews section.
5. Add CI checks for links and metadata.
6. Archive stale or superseded planning/evaluation docs.

---

## Quick Wins (Do This First)

- Add folder-scope README files where missing.
- Add `**Status:**` / `**Owner:**` / `**Reviewed:**` bold-field metadata to the most-visited docs first.
- Merge or cross-link `audits/` and `evaluations/` landing pages.
- Add a “Where should this doc go?” decision tree in `docs/development/documentation-contribution-guide.md`.
