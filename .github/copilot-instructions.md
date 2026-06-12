# Copilot Instructions

<!-- ── Synced section ─────────────────────────────────────────────────────
     This file plus every file under `.github/instructions/` is kept
     identical across all f2calv .NET repositories. The repo-specific
     "Project-Specific Overrides" section below is excluded from sync.
     Edit once, sync everywhere.
     ──────────────────────────────────────────────────────────────────── -->

## Instruction Files

Detailed conventions live in scoped instruction files under `.github/instructions/`, auto-applied by file type:

| File | Applies to | Covers |
| --- | --- | --- |
| `csharp.instructions.md` | `**/*.cs` | C# / .NET style, XML docs, logging, performance, Web API |
| `csharp.testing.instructions.md` | `**/*Tests/**/*.cs` | xUnit test structure, naming, theories, assertions |
| `csharp.mcp.instructions.md` | `**/*.cs` | MCP server tool attributes, descriptions, naming |
| `csharp.azure.instructions.md` | `**/*.cs` | Azure Table Storage & Redis key naming |
| `dotnet.instructions.md` | `**/*.csproj`, `*.slnx`, `Directory.*.props` | Central build/package config, solution format, SDK pinning |
| `github-actions.instructions.md` | workflows / `action.yml` | GitHub Actions naming, YAML, security, GitVersion |
| `documentation.instructions.md` | `**/*.md` | README consistency & Mermaid diagrams |
| `configuration.instructions.md` | `**/appsettings*.json` | `IAppConfig` / appsettings sync |

The conventions below always apply, regardless of the file being edited.

## Copilot Workflow

- **Test execution**: Never run tests automatically — they may be integration tests requiring extra setup. Always prompt (ideally with a visual yes/no button) before running any tests.
- **Preserve git history during renames/moves**: When renaming or relocating files, first perform the rename/move (preferably via `git mv`), then make content edits to the file in its new location/name. This two-step approach preserves git history across the rename. Do not delete-and-recreate files when a rename or move is the intent.
- **Build after refactoring**: After any refactoring, build the **entire solution** (not just the affected project) to catch edge-case compilation errors in dependent projects. When multiple `.sln` / `.slnx` files exist, prefer the one with a `.Debug.slnx` suffix.

## Repository Structure

Every f2calv repository follows a consistent layout, regardless of language:

- **Root files**: `README.md`, `LICENSE`, `GitVersion.yml`, `.editorconfig`, `.gitattributes`, `.gitignore`, and `.pre-commit-config.yaml` live in the repository root.
- **Source code** lives under `src/`. *(Exception: GitHub Action repositories keep `action.yml` at the root per the GitHub Actions convention.)*
- **Tooling** lives in dot-prefixed folders — `.github/` (workflows, instructions), `.scripts/`, `.devcontainer/`, `.docker/`, `.config/`, `.vscode/`.
- **Additional documentation** beyond the root `README.md` lives as Markdown under `docs/`.
- **`.gitattributes`** standardises line endings across Windows/Linux. Use:

  ```gitattributes
  * text=auto eol=lf
  *.{cmd,[cC][mM][dD]} text eol=crlf
  *.{bat,[bB][aA][tT]} text eol=crlf
  ```

- **`.editorconfig`** is the single source of truth for indentation, line endings, and analyzer/formatting rules.
- **`GitVersion.yml`** in the root drives semantic-versioning rules.

## Misc

- When detecting new conventions or patterns in the codebase, add them to the appropriate `.github/instructions/*.instructions.md` file (or this file for cross-cutting workflow rules) and apply them retroactively where applicable.
- Keep this file and the `.github/instructions/` files in sync across repositories based on the common synced guidelines.

---

## Project-Specific Overrides

<!-- This section is excluded from cross-repository sync. Place any repo-specific rules below. -->
