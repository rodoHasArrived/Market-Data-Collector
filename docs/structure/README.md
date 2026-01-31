# Repository Structure Documentation

This folder contains documentation related to the repository's organizational structure.

## Contents

| Document | Purpose |
|----------|---------|
| [REPOSITORY_REORGANIZATION_PLAN.md](REPOSITORY_REORGANIZATION_PLAN.md) | Comprehensive plan for reorganizing the repository structure to improve discoverability and maintainability |

## Overview

The repository structure documentation serves as a guide for:

1. **Understanding** the current organizational decisions
2. **Planning** structural improvements
3. **Executing** reorganization safely
4. **Maintaining** structural integrity over time

## Key Principles

The repository structure follows these guiding principles:

- **Provider Type Parallelism** - Streaming and Historical providers are parallel concerns
- **Layer Integrity** - Clear separation between Application, Domain, and Infrastructure
- **Test Structure Mirroring** - Test folders mirror source folders
- **Documentation by Audience** - Docs organized by who reads them
- **Build Tooling Isolation** - Build infrastructure separate from application source

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Why This Architecture](../architecture/why-this-architecture.md)
- [Architecture Decision Records](../adr/)
