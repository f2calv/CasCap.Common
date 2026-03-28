# Copilot Instructions

<!-- ── Synced section ─────────────────────────────────────────────────────
     Everything above the "Project-Specific Overrides" heading is kept
     identical across all CasCap repositories. Edit once, sync everywhere.
     ──────────────────────────────────────────────────────────────────── -->

## C# / .NET

### Style (enforced by `.editorconfig`)

- **Class-per-file**: Each class, record, struct, or enum should be in its own file, and the filename must match the type name (e.g. `MyService.cs` for `class MyService`). Nested private types used only by their enclosing class are exempt. Enums are also exempt — prefer consolidating all enums within a project into a single `_Enums.cs` file for a quick overview of available enumerations. `IAppConfig` implementations (and their child/nested configuration classes) are also exempt — their filenames are conventionally prefixed with an underscore (e.g. `_AppConfig.cs` for `record AppConfig`). The underscore does **not** appear in the type name itself; it exists solely to group configuration files at the top of the file explorer via alphabetical ordering and to make them easy to identify at a glance.
- **Indentation**: 4 spaces, LF line endings, insert final newline
- **Interfaces**: Must start with `I` (PascalCase) and live in an Abstractions folder and an Abstractions namespace.
- **Types/Methods/Properties**: PascalCase
- **No `this.` prefix**: Qualification disabled
- **Implicit usings**: Enabled
- **Nullable reference types**: Enabled
- **C# Language Version**: 14.0
- **Braces**: Allman style (`csharp_new_line_before_open_brace = all`). For `if`, `else`, `foreach`, `for`, `while`, and `using` statements whose body is a single statement, omit the curly braces to reduce vertical verbosity.
- **Expression-bodied members**: Preferred for accessors, properties, indexers, lambdas; **not** for constructors, operators, or local functions. For methods, use an expression body (`=>`) when the method contains a single expression. If the combined method signature and expression would cause horizontal scrolling on smaller editor windows, place the `=>` and expression on the next line, indented.
- **Async pass-through**: When a method is a thin wrapper that only returns another async call (no `using`, `try`/`catch`, or additional `await`s), drop `async`/`await` and return the `Task`/`ValueTask` directly to avoid unnecessary state-machine overhead.
- **Async/Await**: Always await async method calls.
- **Pattern matching**: Preferred (`is`, `not`, switch expressions)
- **Primary constructors**: Preferred (`csharp_style_prefer_primary_constructors = true`)
- **`var`**: Preferred — use `var` unless the type is not obvious from the right-hand side
- **Records**: Prefer `record` types with `get; init;` properties over classes where object comparison semantics are useful
- **Constructors**: When injecting services use a 'Svc' suffix on the parameter name and its private field instead of 'Service' to make more concise.
- **DI parameter ordering**: In constructors that accept dependency-injected services, parameters should be ordered: `ILogger` first, then any `IOptions<T>` / `IOptionsMonitor<T>`, then custom/application services.
- **No magic strings**: Avoid using string literals as dictionary keys or lookup identifiers in multiple places. Instead, define a `const` field using `nameof()` so the key is a single point of change (e.g. `public const string SummaryValues = nameof(SummaryValues);`).
- **Namespaces**: The convention is folder-based namespacing. However, the `Services` folder is exempt — sub-folders under `Services` do **not** automatically get a sub-namespace. When creating a new sub-folder under `Services`, ask the user whether the sub-folder should introduce a sub-namespace (present a yes/no choice) before proceeding.
- **Namespace declarations**: File-scoped (not block-scoped). Using directives go above the namespace.
- **Using directive ordering**: Pure alphabetical — do **not** place `System.*` first (`dotnet_sort_system_directives_first = false`). No blank line separators between groups (`dotnet_separate_import_directive_groups = false`). This applies to both regular `using` directives and `global using` directives in `GlobalUsings.cs`.
- **Global usings file**: Every project must have a `GlobalUsings.cs` file located in the project root (not in a sub-folder). The file must always be named `GlobalUsings.cs`.
- **Standard overrides at bottom**: Standard C# overrides such as `ToString`, `GetHashCode`, and `Equals` should be placed at the bottom of the class/record body, just above any `#region` blocks for private/static helpers.
- **Property spacing**: Separate each public property declaration (`get`/`set`/`init`) with a blank line (including in records and classes with only auto-properties). Private backing fields, however, should appear on consecutive lines with **no** blank line between them.

### Suppressed Warnings

Configured in `Directory.Build.props`: `IDE1006`, `IDE0079`, `IDE0042`, `CS0162`, `S125`, `NETSDK1233`

### XML Documentation

- Every public or internal class, record, method, property, and enum member should have an XML comment.
- **Exception — test projects**: XML comments are required on classes, records, and properties but **not** on test methods.
- **Document fully on the interface** — use `/// <inheritdoc/>` on implementing classes to avoid duplication.
- When an enum is a public method parameter, use `<inheritdoc cref="EnumType" path="/summary"/>` in the `<param>` tag rather than repeating the enum's documentation.
- **Deep link referenced types**: When XML comments reference .NET classes, structs, interfaces, enums, or namespaces, use `<see cref="Fully.Qualified.TypeName" />` instead of plain text (e.g. `<see cref="Azure.Data.Tables.TableEntity" />`).
- **Timespan config properties**: Any configuration property on an `IAppConfig` implementation that represents a duration (conventionally suffixed with `Ms`) must include a `<see cref="…"/>` deep link to every service class that consumes it in its XML documentation. This ties the property to its consumers and makes it easy to navigate from configuration to consuming code (e.g. `/// Used by <see cref="CasCap.Services.MyMonitorBgService"/>.`).
- **Preserve hyperlinks**: Inline comment hyperlinks to external resources (e.g. blog posts, StackOverflow answers, GitHub issues) must never be deleted. When refactoring a comment into XML documentation, move the URL into a `<remarks>` block using `<see href="…" />` (e.g. `/// <remarks>See <see href="https://example.com" />.</remarks>`).

### Logging

- **`{ClassName}` first**: Every structured log message must include `{ClassName}` as the first template parameter, using `nameof(EnclosingClass)` as the argument (e.g. `_logger.LogInformation("{ClassName} something happened", nameof(MyService));`).
- **Template parameters**: Use PascalCase for all template parameters and never enclose them in quotes (e.g. `{DesiredValue}`, `{GroupAddress}`, `{ValueBefore}` not `'{DesiredValue}'`, `'{GroupAddress}'`, `'{ValueBefore}'`). The logger handles value formatting automatically.
- **No magic strings in log messages**: When a log message references an enum value, class name, or other identifiable symbol, pass it via `nameof()` as a template argument rather than embedding it as a literal string in the message template.

### Disposable Resources

- `ServiceProvider` instances built in tests must be disposed via `using`/`await using`.
- Test helper classes should be `static` when they have no instance state.
- Avoid shared mutable static state in test fixtures — each test should be independently repeatable.

### Multi-Targeting

- Library code using APIs unavailable in lower target frameworks must use `#if` preprocessor guards (e.g. `#if NET8_0_OR_GREATER`).

## MCP (Model Context Protocol)

Feature libraries with `*QueryService` types decorated with `[McpServerToolType]` follow these conventions. Individual methods exposed to the Agent are decorated with `[McpServerTool]` and every such method — including currently commented-out candidates — must also carry a `[Description]` attribute on the method and on each of its parameters. Return-type objects and their nested types must have `[Description]` on every public property.

### Pattern

```csharp
[McpServerTool]
[Description("...")]
public async Task<Foo> DoSomething(
    [Description("...")] string bar,
    CancellationToken cancellationToken = default)
```

> Note: `McpServerToolAttribute` (v1.1.0) has **no** `Description` property. The correct pattern is always two separate attributes: `[McpServerTool]` then `[Description(...)]`.

### MCP `[Description]` text vs XML doc comments

XML doc comments (`<summary>`, `<param>`, `<returns>`) are read by developers and tooling (IntelliSense, generated docs). They may contain `<see cref="..."/>` deep-links, `<remarks>` blocks, multi-sentence explanations, and coding-specific detail.

`[Description]` text on MCP tools is **not** UI copy and is **not** a contract with humans. It is:

- Contextual guidance for an LLM deciding **which tool to call** and **how to map arguments**
- Never shown to end users
- Not subject to grammar or localisation requirements

Therefore MCP descriptions should be:

- **Concise** — one sentence per method/parameter is usually enough
- **Semantically rich** — include the key noun, verb, and any units or constraints the LLM needs (e.g. `"0=fully open, 100=fully closed"`, `"range 14–25"`)
- **Disambiguation-first** — if two tools or parameters could be confused, the description must distinguish them
- **Enum-aware** — when a parameter is typed as `string` but represents an enum, list **all valid values with a brief label** in the description text (the LLM cannot infer them from the type):

```csharp
[Description("Floor filter. Values: Ground, Upper, Basement, Attic.")]
string? floor = null
```

- **No XML markup** — plain text only; `<see cref="..."/>` links are meaningless to an LLM
- **No localization** — English only; multiple languages add noise without benefit

### Checklist when adding or editing a `[McpServerTool]` method

1. Add `[McpServerTool]` then `[Description("...")]` on the method — one sentence naming what it does.
2. Add `[Description("...")]` on every non-`CancellationToken` parameter.
3. For `string` parameters representing an enum: list all enum member names with a brief description each.
4. For complex request-object parameters: summarise the key fields and their constraints in the description.
5. Ensure every public property on the return type (and any nested types) has `[Description("...")]` with a concise label and unit/range where applicable.
6. Keep XML `<summary>` comments intact — they serve a different audience and must not be replaced by or merged with `[Description]` text.

## Cloud (Azure)

- **Azure Table Storage column naming**: For high-volume line-item/reading entities where many thousands of rows are retrieved, use ultra-short column names (even single letters) to reduce payload size and improve retrieval speed. This optimization is not needed for low-volume snapshot/summary entities where readability is more important.

## GitHub Actions

- **Step naming — one-liners**: When a step's `run` block is a single command, use that command (or a slightly abbreviated form) as the step `name` rather than a descriptive prose label (e.g. `name: npm install --global json5`, not `name: setup json5`).

## Documentation

### README Consistency

- **Every project must have a `README.md`**: When adding a new `.csproj` project, create a `README.md` in the project directory as part of the same commit. Follow the existing pattern: Purpose → Services/Extensions → Configuration → Dependencies (NuGet packages table + Project references table).
- Every project's `README.md` must stay in sync with its implementation. During any refactoring — and **always** before creating a new PR — scan each affected project's `README.md` for inconsistencies: outdated service names, missing or removed configuration options, stale dependency tables, or inaccurate flow diagrams. Update the README as part of the same change, not as a follow-up.
- **Major refactorings** (renames, project moves, DI restructuring, model type splits): when a rename or restructure touches type names, configuration sections, or project references, update every `README.md` that mentions the old names **in the same commit**. Do not leave stale references for a follow-up.
- For large refactorings that touch multiple projects, review all impacted `README.md` files before opening the PR.
- **Mermaid diagrams**: Use Mermaid diagrams in `README.md` files to illustrate NuGet package dependency graphs, GitHub Actions workflow chains, and .NET service/class hierarchies. These diagrams make complex relationships immediately visible and must be kept in sync with the code they describe.

## Configuration

### Configuration Sync

- Configuration properties (e.g. polling delays, feature flags, thresholds) are defined with sensible defaults directly on the `IAppConfig` record/class. Having defaults in the record means the application works out-of-the-box, but every property can be overridden via `appsettings*.json` or directly with environment variables in Kubernetes deployments (using the standard `CasCap__SectionName__PropertyName` double-underscore convention).
- When adding, renaming, or removing a property on any class or record that implements `IAppConfig` — or on any child/nested type reachable from such a class — update **all** `appsettings*.json` files (`appsettings.json`, `appsettings.Development.json`, and any other environment-specific variants) in the same commit. This includes adding new keys with sensible defaults, renaming keys to match the new property name, and removing keys for deleted properties. If the new property's record default is already the desired value for all environments, the `appsettings*.json` files do not need a new entry — only add one when an environment-specific override is required.

## Copilot Workflow

- **Test execution after refactoring**: After completing a refactoring, always prompt the user with a yes/no choice before running any tests. Do not automatically run tests. When prompting, offer a clickable yes/no UI option if the environment supports it.
- **Preserve git history during renames/moves**: When renaming or relocating files, first perform the rename/move (preferably via `git mv`), then make content edits to the file in its new location/name. This two-step approach preserves git history across the rename. Do not delete-and-recreate files when a rename or move is the intent.

## Misc

- When detecting new conventions or patterns in the codebase, add them to this document and apply them retroactively where applicable.

---

## Project-Specific Overrides

<!-- This section is excluded from cross-repository sync. Place any repo-specific rules below. -->
