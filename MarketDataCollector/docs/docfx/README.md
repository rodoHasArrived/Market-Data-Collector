# DocFX Setup

## Prerequisites
- .NET SDK installed
- DocFX installed (as a dotnet tool)

Example:
```bash
dotnet tool update -g docfx
```

## Build docs
From repository root:
```bash
docfx docs/docfx/docfx.json
```

Output will be in:
`docs/docfx/_site`

## Notes
- Mermaid rendering depends on the template. If Mermaid is not rendered, export diagrams to SVG/PNG and reference them from markdown.
