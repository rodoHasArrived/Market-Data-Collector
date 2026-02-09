# AI Assistant Prompts

This directory contains reusable prompt templates for AI assistants (Claude, Copilot, ChatGPT, etc.) working with the Market Data Collector codebase.

## Available Prompts

| Prompt | Description | Use When |
|--------|-------------|----------|
| [project-context.prompt.yml](project-context.prompt.yml) | Project overview and coding conventions | Starting any task, need project context |
| [code-review.prompt.yml](code-review.prompt.yml) | Comprehensive code review guidelines | Reviewing PRs or code changes |
| [add-data-provider.prompt.yml](add-data-provider.prompt.yml) | Guide for adding new data providers | Implementing new market data integrations |
| [provider-implementation-guide.prompt.yml](provider-implementation-guide.prompt.yml) | Detailed IMarketDataClient patterns | Implementing streaming providers |
| [write-unit-tests.prompt.yml](write-unit-tests.prompt.yml) | Unit test generation guidelines | Writing tests for components |
| [explain-architecture.prompt.yml](explain-architecture.prompt.yml) | Architecture explanation guide | Understanding system design |
| [troubleshoot-issue.prompt.yml](troubleshoot-issue.prompt.yml) | Issue diagnosis and resolution | Debugging problems |
| [optimize-performance.prompt.yml](optimize-performance.prompt.yml) | Performance optimization guidance | Improving hot paths |
| [configure-deployment.prompt.yml](configure-deployment.prompt.yml) | Deployment configuration help | Setting up environments |
| [add-export-format.prompt.yml](add-export-format.prompt.yml) | Export format implementation | Adding new export types |
| [wpf-debug-improve.prompt.yml](wpf-debug-improve.prompt.yml) | WPF debugging and improvement guide | Fixing or completing WPF UI work |

## How to Use

### With GitHub Copilot Chat

Reference a prompt in your chat:

```
@workspace /explain Use the explain-architecture prompt to explain the event pipeline
```

### With Claude Code

The prompts work as context for Claude Code sessions. Reference the project context:

```
Read .github/prompts/project-context.prompt.yml and use it as context for this task: [your task]
```

### Manual Use

Copy the system message content from any prompt file and use it as context for your AI assistant.

## Prompt Structure

Each prompt follows a standard structure:

```yaml
name: Prompt Name
description: What this prompt helps with
# Model-agnostic prompt - works with any capable LLM
messages:
  - role: system
    content: |
      Context and instructions for the AI...
  - role: user
    content: |
      Template with {{placeholders}} for user input...
```

## Quick Reference

### Development Tasks

- **New provider**: `add-data-provider.prompt.yml` + `provider-implementation-guide.prompt.yml`
- **New export format**: `add-export-format.prompt.yml`
- **Write tests**: `write-unit-tests.prompt.yml`
- **Code review**: `code-review.prompt.yml`

### Understanding & Troubleshooting

- **Architecture questions**: `explain-architecture.prompt.yml`
- **Debug issues**: `troubleshoot-issue.prompt.yml`
- **Performance problems**: `optimize-performance.prompt.yml`

### DevOps

- **Deployment setup**: `configure-deployment.prompt.yml`

## Adding New Prompts

1. Create a new `.prompt.yml` file in this directory
2. Follow the existing structure (name, description, messages)
3. Include relevant project context in the system message
4. Add placeholders (`{{variable}}`) for user-provided values
5. Update this README with the new prompt

## Related Resources

- [CLAUDE.md](../../CLAUDE.md) - Main project instructions for AI assistants
- [copilot-instructions.md](../../docs/ai/copilot/instructions.md) - GitHub Copilot configuration
- [agents/](../agents/) - AI agent configurations

---

**Last Updated**: 2026-01-31
