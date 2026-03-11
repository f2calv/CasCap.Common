# Copilot Instructions

## Code Quality Conventions

### Style (enforced by `.editorconfig`)

- **Class-per-file**: Each class, record, struct, or enum should be in its own file, and the filename must match the type name (e.g. `MyService.cs` for `class MyService`). Nested private types used only by their enclosing class are exempt. Enums are also exempt — prefer consolidating all enums within a project into a single `_Enums.cs` file for a quick overview of available enumerations.
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
- **Pattern matching**: Preferred (`is`, `not`, switch expressions)
- **Primary constructors**: Preferred (`csharp_style_prefer_primary_constructors = true`)
- **`var`**: Preferred — use `var` unless the type is not obvious from the right-hand side
- **Records**: Prefer `record` types with `get; init;` properties over classes where object comparison semantics are useful
- **Constructors**: When injecting services use a 'Svc' suffix on the parameter name and its private field instead of 'Service' to make more concise.
- **DI parameter ordering**: In constructors that accept dependency-injected services, parameters should be ordered: `ILogger` first, then any `IOptions<T>` / `IOptionsMonitor<T>`, then custom/application services.
- **No magic strings**: Avoid using string literals as dictionary keys or lookup identifiers in multiple places. Instead, define a `const` field using `nameof()` so the key is a single point of change (e.g. `public const string SummaryValues = nameof(SummaryValues);`).
- **Namespaces**: The convention is folder-based namespacing. However, the `Services` folder is exempt — sub-folders under `Services` do **not** automatically get a sub-namespace. When creating a new sub-folder under `Services`, ask the user whether the sub-folder should introduce a sub-namespace (present a yes/no choice) before proceeding.
- **Namespace declarations**: File-scoped (not block-scoped). Using directives go above the namespace.
- **Standard overrides at bottom**: Standard C# overrides such as `ToString`, `GetHashCode`, and `Equals` should be placed at the bottom of the class/record body, just above any `#region` blocks for private/static helpers.
- **Property spacing**: Separate each public property declaration (`get`/`set`/`init`) with a blank line (including in records and classes with only auto-properties). Private backing fields, however, should appear on consecutive lines with **no** blank line between them.

### Suppressed Warnings

Configured in `Directory.Build.props`: `IDE1006`, `IDE0079`, `IDE0042`, `CS0162`, `S125`, `NETSDK1233`

### XML Documentation

- Every public class, record, method, property, and enum member should have an XML comment.
- **Exception — test projects**: XML comments are required on classes, records, and properties but **not** on test methods.
- **Document fully on the interface** — use `/// <inheritdoc/>` on implementing classes to avoid duplication.
- When an enum is a public method parameter, use `<inheritdoc cref="EnumType" path="/summary"/>` in the `<param>` tag rather than repeating the enum's documentation.
- **Deep link referenced types**: When XML comments reference .NET classes, structs, interfaces, enums, or namespaces, use `<see cref="Fully.Qualified.TypeName" />` instead of plain text (e.g. `<see cref="Azure.Data.Tables.TableEntity" />`).
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
