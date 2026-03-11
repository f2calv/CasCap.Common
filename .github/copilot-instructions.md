# CasCap.Common - Copilot Instructions

## Code Quality Conventions

### Style (enforced by `.editorconfig`)

- **Class-per-file**: Each class, record, struct, or enum should be in its own file. Nested private types used only by their enclosing class are exempt.
- **Indentation**: 4 spaces, LF line endings
- **Interfaces**: Must start with `I` (PascalCase)
- **Types/Methods/Properties**: PascalCase
- **No `this.` prefix**: Qualification disabled
- **Braces**: Allman style (`csharp_new_line_before_open_brace = all`)
- **Expression-bodied members**: Preferred for accessors, properties, indexers, lambdas; **not** for constructors, methods, operators, local functions
- **Pattern matching**: Preferred (`is`, `not`, switch expressions)
- **Primary constructors**: Preferred (`csharp_style_prefer_primary_constructors = true`)
- **`var`**: Preferred — use `var` unless the type is not obvious from the right-hand side
- **Records**: Prefer `record` types with `get; init;` properties over classes where object comparison semantics are useful

### XML Documentation

- Every public class, record, method, property, and enum member should have an XML comment.
- **Exception — test projects**: XML comments are required on classes, records, and properties but **not** on test methods.
- **Document fully on the interface** — use `/// <inheritdoc/>` on implementing classes to avoid duplication.
- When an enum is a public method parameter, use `<inheritdoc cref="EnumType" path="/summary"/>` in the `<param>` tag rather than repeating the enum's documentation.
- Separate each property declaration with a blank line (including in records and classes with only auto-properties).

### Disposable Resources

- `ServiceProvider` instances built in tests must be disposed via `using`/`await using`.
- Test helper classes should be `static` when they have no instance state.
- Avoid shared mutable static state in test fixtures — each test should be independently repeatable.

### Multi-Targeting

- Library code using APIs unavailable in netstandard2.0 must use `#if NET8_0_OR_GREATER` or similar guards.
- `HttpClientBase` is entirely guarded behind `#if NET8_0_OR_GREATER`.
- `CasCap.Common.Services` targets net8.0+ only (no netstandard2.0).


