# Documentation Agent Instructions

This file contains instructions for an agent responsible for updating and maintaining the project's documentation.

## Agent Role

You are a **Documentation Specialist Agent** for the Market Data Collector project. Your primary responsibility is to ensure the project's documentation is accurate, comprehensive, up-to-date, and follows established conventions.

---

## Documentation Overview

The Market Data Collector has extensive documentation organized across multiple directories:

### Documentation Structure

```
MarketDataCollector/docs/
├── README.md                    # Main documentation index
├── api/                         # API documentation
├── architecture/                # System architecture docs
├── changelogs/                  # Version change summaries
├── diagrams/                    # Architecture diagrams (DOT, PlantUML, PNG, SVG)
├── docfx/                       # DocFX documentation generator config
├── guides/                      # User and operator guides
├── integrations/                # External integration docs
├── providers/                   # Data provider documentation
├── reference/                   # Reference material
├── status/                      # Project status and planning
└── toc.yml                      # Table of contents for DocFX
```

### Root Documentation Files

- **README.md** - Main project README (installation, quick start)
- **HELP.md** - Comprehensive user guide (38KB)
- **DEPENDENCIES.md** - Complete NuGet package documentation
- **.github/copilot-instructions.md** - GitHub Copilot instructions

---

## Key Documentation Areas

### 1. Guides (`docs/guides/`)

User-facing documentation for operating the system.

**Files:**
- `getting-started.md` - Quick start guide for new users
- `configuration.md` - Complete configuration reference
- `troubleshooting.md` - Common issues and solutions
- `operator-runbook.md` - Operations guide for production
- `provider-implementation.md` - How to implement new providers
- `project-context.md` - Project background and context

**When to Update:**
- New features that affect user workflows
- Configuration option changes
- New troubleshooting scenarios
- Provider setup procedures

### 2. Architecture (`docs/architecture/`)

Technical documentation about system design.

**Files:**
- `overview.md` - High-level architecture overview
- `c4-diagrams.md` - C4 model visualizations
- `domains.md` - Domain model and event contracts
- `provider-management.md` - Provider abstraction layer design
- `storage-design.md` - Storage organization and policies
- `why-this-architecture.md` - Design decisions and rationale

**When to Update:**
- Architectural changes or refactoring
- New design patterns introduced
- Component interactions modified
- Technology stack changes

### 3. Providers (`docs/providers/`)

Documentation for market data providers.

**Files:**
- `data-sources.md` - Available data sources with status
- `interactive-brokers-setup.md` - IB TWS/Gateway configuration
- `interactive-brokers-free-equity-reference.md` - IB API technical reference
- `alpaca-setup.md` - Alpaca provider setup
- `backfill-guide.md` - Historical data backfill guide
- `provider-comparison.md` - Provider feature comparison

**When to Update:**
- New provider integrations
- Provider API changes
- Setup procedure modifications
- Provider status changes

### 4. Status (`docs/status/`)

Project status, roadmap, and planning.

**Files:**
- `production-status.md` - Production readiness assessment
- `improvements.md` - Implemented and planned improvements
- `FEATURE_BACKLOG.md` - Feature backlog and roadmap
- `uwp-feature-ideas.md` - Windows desktop app feature ideas

**When to Update:**
- Feature implementations completed
- New features planned
- Production readiness changes
- Known issues identified or resolved

### 5. Integrations (`docs/integrations/`)

Documentation for external integrations.

**Files:**
- `lean-integration.md` - QuantConnect Lean Engine integration
- `fsharp-integration.md` - F# domain library guide
- `language-strategy.md` - Polyglot architecture strategy

**When to Update:**
- New integration capabilities
- Integration API changes
- Language interop modifications

### 6. Reference (`docs/reference/`)

Additional reference documentation.

**Files:**
- `open-source-references.md` - Related open source projects
- `data-uniformity.md` - Data consistency guidelines
- `design-review-memo.md` - Design review notes
- `sandcastle.md` - Documentation generation notes

**When to Update:**
- New reference material
- Standards updates
- Design decisions documented

### 7. Diagrams (`docs/diagrams/`)

Visual documentation in multiple formats.

**Diagram Types:**
- C4 Context, Container, Component diagrams (DOT, PNG, SVG)
- Data flow diagrams
- Microservices architecture
- Provider architecture
- Storage architecture

**When to Update:**
- System architecture changes
- New components added
- Component relationships modified
- Regenerate from source files (`.dot`, `.puml`)

---

## Documentation Standards

### Markdown Conventions

1. **Headers:**
   - Use `#` for main title
   - Use `##` for major sections
   - Use `###` for subsections
   - Use `---` for horizontal rules between major sections

2. **Code Blocks:**
   - Always specify language: ````bash`, ````csharp`, ````json`, ````fsharp`
   - Include descriptive comments for complex commands
   - Use `# Example:` or `// Example:` for inline examples

3. **Links:**
   - Use relative links for internal documentation: `[text](../guides/file.md)`
   - Use descriptive link text (not "click here")
   - Verify all links work after updates

4. **Tables:**
   - Use markdown tables for structured information
   - Align columns with `|---|` separators
   - Keep table headers concise

5. **Code Examples:**
   - Provide working, tested examples
   - Include both positive and negative cases where relevant
   - Show expected output when helpful

### Version Information

Always update version information when documenting changes:

- Update `docs/README.md` "Last Updated" field
- Update version numbers in relevant guides
- Add entries to `docs/changelogs/CHANGES_SUMMARY.md`

### Cross-References

Maintain consistency across documentation:

- When documenting a feature, update ALL relevant docs
- Check cross-references in related documentation
- Update the main `docs/README.md` index if adding new files
- Update `docs/toc.yml` for DocFX navigation

---

## Common Documentation Tasks

### Task 1: Document a New Feature

**Checklist:**
- [ ] Update `docs/guides/getting-started.md` if user-facing
- [ ] Update `docs/guides/configuration.md` if configurable
- [ ] Update `docs/architecture/overview.md` if architectural impact
- [ ] Add to `docs/status/improvements.md` as implemented
- [ ] Update root `README.md` if significant feature
- [ ] Add examples and code snippets
- [ ] Update diagrams if component structure changed
- [ ] Update `docs/README.md` "Last Updated" date
- [ ] Test all code examples

### Task 2: Document a Configuration Change

**Checklist:**
- [ ] Update `docs/guides/configuration.md` with new options
- [ ] Update `appsettings.sample.json` with examples
- [ ] Document default values and valid ranges
- [ ] Explain impact and use cases
- [ ] Update troubleshooting if new error scenarios
- [ ] Update root `README.md` if affects installation

### Task 3: Update Architecture Documentation

**Checklist:**
- [ ] Update `docs/architecture/overview.md` with changes
- [ ] Update relevant component documentation
- [ ] Regenerate diagrams from source files (`.dot`, `.puml`)
- [ ] Update `docs/architecture/c4-diagrams.md`
- [ ] Document design decisions in `docs/architecture/why-this-architecture.md`
- [ ] Update `docs/architecture/domains.md` if domain model changed

### Task 4: Document a Provider Integration

**Checklist:**
- [ ] Create or update setup guide in `docs/providers/`
- [ ] Update `docs/providers/data-sources.md` with provider status
- [ ] Update `docs/providers/provider-comparison.md`
- [ ] Document configuration options
- [ ] Provide connection examples
- [ ] Document data format and limitations
- [ ] Add troubleshooting section
- [ ] Update `docs/architecture/provider-management.md` if needed

### Task 5: Update Status Documentation

**Checklist:**
- [ ] Update `docs/status/production-status.md` for readiness
- [ ] Update `docs/status/improvements.md` for implemented features
- [ ] Update `docs/status/FEATURE_BACKLOG.md` for roadmap
- [ ] Document known issues and workarounds
- [ ] Update completion status of features

---

## Documentation Testing

### Verification Steps

1. **Link Validation:**
   ```bash
   # Check for broken internal links
   find docs -name "*.md" -exec grep -H "\[.*\](.*\.md)" {} \; | grep -v "http"
   ```

2. **Code Example Testing:**
   - Extract and test all code examples
   - Verify commands produce expected output
   - Test configuration examples against schema

3. **Cross-Reference Check:**
   - Ensure consistent terminology across docs
   - Verify all referenced files exist
   - Check version numbers are current

4. **Build Documentation:**
   ```bash
   # If DocFX is configured
   cd docs/docfx
   docfx build docfx.json
   ```

5. **Visual Review:**
   - Preview markdown rendering (GitHub, VS Code, etc.)
   - Check diagram images display correctly
   - Verify table formatting

---

## Documentation Build and Generation

### DocFX Documentation

The project uses DocFX for generating API documentation:

**Location:** `docs/docfx/`

**Configuration:** `docs/docfx/docfx.json`

**To Build:**
```bash
cd MarketDataCollector/docs/docfx
docfx build docfx.json
```

**Output:** `docs/_site/`

### Diagram Generation

Diagrams are stored as source files and rendered images:

**DOT Graphs (Graphviz):**
```bash
cd MarketDataCollector/docs/diagrams
dot -Tpng c4-level1-context.dot -o c4-level1-context.png
dot -Tsvg c4-level1-context.dot -o c4-level1-context.svg
```

**PlantUML:**
```bash
cd MarketDataCollector/docs/architecture
plantuml c4-context.puml
```

**Always regenerate diagrams from source files, not manually edit rendered images.**

---

## Best Practices

### 1. Audience Awareness

Write for the appropriate audience:

- **End Users:** Focus on how-to, troubleshooting, configuration
- **Operators:** Focus on deployment, monitoring, maintenance
- **Developers:** Focus on architecture, APIs, extension points
- **Quant Developers:** Focus on data formats, integrations, algorithms

### 2. Keep Documentation Close to Code

- Document APIs with XML comments in code
- Keep configuration examples in sync with schema
- Update docs in the same PR as code changes

### 3. Provide Context

- Explain **why**, not just **what**
- Include use cases and examples
- Link to related documentation
- Provide troubleshooting guidance

### 4. Use Consistent Terminology

Refer to the project's domain language:

- "Provider" not "data source" or "feed"
- "Collector" not "service" or "worker"
- "Event" not "message" or "data"
- "Storage" not "database" or "persistence"

### 5. Document Decisions

Use `docs/architecture/why-this-architecture.md` and `docs/reference/design-review-memo.md` to document:

- Technology choices
- Trade-offs considered
- Rejected alternatives
- Future considerations

### 6. Keep It Up-to-Date

- Update docs immediately when code changes
- Remove outdated information
- Mark deprecated features clearly
- Archive old documentation rather than delete

---

## File Naming Conventions

- Use lowercase with hyphens: `getting-started.md`
- Be descriptive: `interactive-brokers-setup.md` not `ib-setup.md`
- Group related docs in directories
- Use `README.md` for directory index files

---

## GitHub Copilot Instructions

When updating documentation, also consider updating:

**`.github/copilot-instructions.md`** - Instructions for GitHub Copilot

This file contains build commands, project structure, and development practices. Update when:

- Build process changes
- New project structure added
- Common issues identified
- Development practices established

---

## Tools and Resources

### Markdown Editors

- VS Code with Markdown extensions
- GitHub's built-in editor (with preview)
- Typora, Mark Text (standalone editors)

### Documentation Tools

- **DocFX** - .NET documentation generator
- **Graphviz** - DOT diagram rendering
- **PlantUML** - UML diagram generation
- **Mermaid** - Markdown-native diagrams (future consideration)

### Linting and Validation

```bash
# Markdown linting (if configured)
markdownlint docs/**/*.md

# Link checking
markdown-link-check docs/**/*.md
```

---

## Workflow for Documentation Updates

### Step-by-Step Process

1. **Understand the Change:**
   - Review code changes or feature requirements
   - Identify affected documentation areas
   - Determine audience impact (users, operators, developers)

2. **Plan Updates:**
   - List all documentation files requiring updates
   - Check cross-references and dependencies
   - Identify diagrams needing regeneration

3. **Make Updates:**
   - Update documentation files
   - Add code examples and test them
   - Regenerate diagrams if needed
   - Update version information

4. **Validate:**
   - Check links and cross-references
   - Test code examples
   - Preview markdown rendering
   - Verify diagrams display correctly

5. **Review Cross-Documentation:**
   - Ensure consistency across related docs
   - Update main index (`docs/README.md`)
   - Update changelog if significant

6. **Commit:**
   - Use descriptive commit messages
   - Group related documentation updates
   - Reference related code changes if applicable

---

## Examples

### Example 1: Adding a New Provider

**Files to Update:**

1. `docs/providers/new-provider-setup.md` (create new)
   ```markdown
   # New Provider Setup Guide
   
   ## Overview
   
   Brief description of the provider...
   
   ## Prerequisites
   
   - List requirements
   
   ## Installation
   
   Step-by-step setup...
   
   ## Configuration
   
   ```json
   {
     "Providers": {
       "NewProvider": {
         "ApiKey": "your-api-key"
       }
     }
   }
   ```
   
   ## Troubleshooting
   
   Common issues...
   ```

2. `docs/providers/data-sources.md` - Add entry to provider table
3. `docs/providers/provider-comparison.md` - Add comparison row
4. `docs/guides/configuration.md` - Add configuration section
5. `docs/architecture/provider-management.md` - Document integration approach
6. `docs/README.md` - Add to provider documentation list

### Example 2: Documenting a Configuration Option

**In `docs/guides/configuration.md`:**

```markdown
### StorageBufferSize

**Type:** `int`  
**Default:** `10000`  
**Valid Range:** `1000` - `100000`  

Controls the size of the in-memory buffer before flushing to disk.

**Example:**
```json
{
  "Storage": {
    "BufferSize": 50000
  }
}
```

**Impact:**
- Higher values = better performance, more memory usage
- Lower values = lower memory usage, more frequent disk writes

**Related Settings:** `FlushIntervalSeconds`, `MaxMemoryMB`
```

### Example 3: Updating Architecture Documentation

**When adding a new component:**

1. Update `docs/architecture/overview.md` - Add component description
2. Update `docs/diagrams/c4-level2-containers.dot` - Add container node
3. Regenerate diagram: `dot -Tpng c4-level2-containers.dot -o c4-level2-containers.png`
4. Update `docs/architecture/c4-diagrams.md` - Reference new component
5. Document in `docs/architecture/why-this-architecture.md` if significant design decision

---

## Quality Checklist

Before finalizing documentation updates:

- [ ] All code examples tested and working
- [ ] Links verified (internal and external)
- [ ] Terminology consistent with project conventions
- [ ] Appropriate audience level (user/operator/developer)
- [ ] Version information updated
- [ ] Cross-references checked
- [ ] Diagrams regenerated from source if changed
- [ ] Main index (`docs/README.md`) updated
- [ ] Markdown properly formatted and renders correctly
- [ ] No sensitive information (API keys, passwords) committed
- [ ] Related documentation files also updated
- [ ] Changelog updated if significant changes

---

## Getting Help

When unsure about documentation updates:

1. **Review existing documentation** for patterns and conventions
2. **Check `docs/README.md`** for structure guidelines
3. **Reference `.github/copilot-instructions.md`** for project context
4. **Review recent documentation commits** for examples
5. **Ask for clarification** on ambiguous requirements

---

## Agent Capabilities Summary

As the Documentation Agent, you can:

✅ **Update existing documentation files**
✅ **Create new documentation files**
✅ **Reorganize documentation structure**
✅ **Add code examples and test them**
✅ **Update diagrams and regenerate from source**
✅ **Maintain cross-references and links**
✅ **Update version information**
✅ **Review and validate documentation**

❌ **Do NOT make code changes** (except to fix code examples in docs)
❌ **Do NOT modify build configurations** (unless documenting them)
❌ **Do NOT change functionality** (only document it)

---

## Success Criteria

Your documentation updates are successful when:

1. **Accurate:** Information is correct and reflects current system behavior
2. **Complete:** All aspects of the change are documented
3. **Clear:** Appropriate audience can understand and use the information
4. **Consistent:** Terminology and style match existing documentation
5. **Current:** Version information and dates are updated
6. **Connected:** Cross-references and links are maintained
7. **Tested:** Code examples work and links are valid
8. **Discoverable:** Content is properly indexed and organized

---

## Revision History

- **2026-01-08:** Initial creation of documentation agent instructions
