# mdc-brainstorm — Changelog

## v1.2.0 (2026-03-16)

### Added
- **Summary table (Ideas at a Glance)** — every brainstorm output now opens with a triage table (Idea | Effort | Audience | Impact | Depends On) before the narrative ideas; S/M/L/XL effort keys; lets users triage in 30 seconds
- **Explicit mode detection** — Step 0 now requires a one-line mode declaration at the top of the response (`**Mode detected:** [Mode Name] — [reasoning]`); prevents silent mode mismatches; ambiguous requests state both modes
- **Skill Improvement mode** — added as an explicit mode in the mode table; triggers when the user asks how the skills themselves can be improved; applies the brainstorm process reflexively
- **Competitive signals in every synthesis** — synthesis section now always includes 2-3 sentences from `references/competitive-landscape.md` on how competitors handle the brainstorm space; was previously only active in Competitive mode
- **Idea continuity / session ledger** — documented `brainstorm-history.jsonl` convention at `.claude/skills/mdc-brainstorm/brainstorm-history.jsonl` (gitignored); opens each session with "Previous sessions covered: X. Unexplored areas: Y."
- **Codebase anchor table** in `references/idea-dimensions.md` — 35-entry table mapping concept names to file paths and class names; makes ideas immediately navigable; covers all major interfaces, sinks, validators, providers, and WPF classes
- **Shared project context** — SKILL.md now references `../_shared/project-context.md` for authoritative stats, ADR table, and file paths; updated project context section to match actual current state (779 files, 266 test files, 27 CI workflows, 5 streaming providers)

### Changed
- Updated project context section: 779 source files (was unstated), 266 test files, 13 main projects, 5 streaming providers (Alpaca, Polygon, IB, StockSharp, NYSE)
- `references/idea-dimensions.md`: added frontmatter pointer to `_shared/project-context.md`; prepended codebase anchor table before existing dimension categories
- Mode table: added explicit trigger phrases for each mode; reordered to match frequency of use; added "Skill Improvement" mode

---

## v1.1.0 (2026-02-28)

### Added
- Added competitive mode with `references/competitive-landscape.md`
- Added UX / Information Design mode
- Added Technical Debt / Code Quality mode

### Changed
- Expanded persona descriptions for Hobbyist, Academic, and Institutional audiences
- Added WPF-specific UX principles to "The User Experience Lens" section

---

## v1.0.0 (2026-02-01)

### Added
- Initial skill release with 9 brainstorm modes
- `references/idea-dimensions.md` with 10 seeded concept categories
- Synthesis section format with highest-leverage idea, platform bets, and sequencing
