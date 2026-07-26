---
description: 'xUnit test project structure, naming, theories and assertion conventions.'
applyTo: '**/*Tests/**/*.cs'
---

# Testing

## Folder Structure

Every `*.Tests` project organises tests into subfolders by type:

```text
Tests/
├── Unit/           # Self-contained unit tests (no DI, no external services)
├── Integration/    # Tests requiring DI, configuration, or external services
│   └── TestBase.cs # Shared base class for integration tests
└── Gfx/            # Graphics/rendering tests (optional, project-specific)
```

## Integration Tests

- Must carry `[Trait("Category", "Integration")]`.
- Must live in the `Tests/Integration/` subfolder.
- Must inherit from `TestBase(output)` — this wires up `ILoggerFactory` (Serilog → xUnit output), `IConfiguration` (appsettings loading), and optionally DI services.
- `TestBase` lives in `Tests/Integration/TestBase.cs`. It exposes `protected` fields for commonly-used services resolved from the DI container. When a new service is needed by multiple integration test classes, add a `protected` field to `TestBase` resolved from the service provider — do not duplicate service resolution in each test class.
- `TestBase` exposes a `protected ITestOutputHelper _output` field. Subclasses should use `_output` directly rather than capturing the constructor parameter separately — capturing it in a primary constructor while also passing it to the base is compile error **CS9107**, so a `TestBase` that fails to expose the field pushes every subclass onto a worse alternative.
- **Secrets are loaded from Azure Key Vault.** `TestBase` builds configuration in two passes: the first reads the `AppConfig` section to discover the Key Vault URI and token credential, the second layers Key Vault over the `appsettings*.json` chain. Integration tests therefore run against real configuration with **no manual local wiring**, but they require an authenticated Azure context (`az login` locally, a managed identity or federated credential in CI) and will fail during construction without one.
- Never commit a secret to an `appsettings*.json` file to make an integration test pass, and never write a resolved secret to test output.
- Exception: lightweight integration tests that only need `HttpClient` (no DI container) may take `ITestOutputHelper` directly without `TestBase`.

## Unit Tests

- Live in the `Tests/Unit/` subfolder.
- Do **not** inherit from `TestBase` — they are self-contained with no DI container.
- May take `ITestOutputHelper` directly for diagnostic output.
- Use domain-specific trait categories (e.g. `"Parsing"`, `"String Manipulation"`) — not `"Unit"`.

## Diagnostic Output

- Write diagnostic output through `ITestOutputHelper` — `_output.WriteLine(...)` in classes deriving from `TestBase`, or a directly-injected `ITestOutputHelper` in unit tests. Logging through an `ILogger` resolved from the DI container is equally acceptable, since `TestBase` routes Serilog to xUnit output.
- **Never use `Debug.WriteLine` or `Console.WriteLine` for test diagnostics.** The xUnit runner captures neither, so the output is invisible in CI, in `dotnet test`, and in the VS Code Test Explorer — precisely the moments it is needed. `Debug.WriteLine` is additionally compiled out of Release builds, so it is silently absent from the configuration most pipelines run.
- Prefer string interpolation (`$"..."`) over concatenation or composite format strings.
- Do not dump large payloads unconditionally. Emit the few values that explain a failure, and put the decisive ones in the assertion message so they survive into the failure report rather than only appearing on a passing run.

## Theory Parameterisation

- When multiple `[Fact]` tests differ only by input values, consolidate into a single `[Theory]` with `[InlineData]`. This reduces code duplication while expanding coverage.
- Use `[Theory]` when testing the same logic across different parameter combinations (e.g. input sizes, threshold values, format types).
- Keep `[Fact]` for tests with complex setup/assertion logic that doesn't parameterise cleanly.

## Test Method Naming

- Name test methods after the method or feature being tested (e.g. `GetColumnCells`, `CreateOrUpdateItem`, `DetectsThresholdBreach`).
- Do **not** use verbose BDD-style sentence names (e.g. avoid `Should_Return_Column_When_Given_Valid_Input`).
- For lifecycle tests, use `_` separated phases (e.g. `ItemLifecycle_CreateGetUpdateDelete`).

## Assertions

- Every test must have meaningful assertions. Never use `Assert.True(true)` or other placeholder assertions.
- Prefer specific assertions (`Assert.Equal`, `Assert.Contains`, `Assert.Null`) over generic `Assert.True(condition)`.
- Tests that only measure performance (timing loops, `Stopwatch`, `Debug.WriteLine` of elapsed time) without asserting correctness are **not valid tests** — delete them and migrate the benchmark to a BenchmarkDotNet project if the measurement is still needed.

## Dead Code in Test Projects

- Commented-out test methods, unreachable code behind `return;`, and `[Skip]`-annotated tests with no plan to re-enable should be deleted rather than left to rot.
- When removing a test method that was the last consumer of a helper/field, remove the helper/field in the same commit.
- `using` directives that become unused after test removal must be cleaned up in the same commit.

## Shared Test Data

- Shared test data generators and fixtures live in dedicated `*TestData.cs` files at the `Tests/` root.
- Hardcoded reference data for regression tests lives in `*Patterns.cs` files.
- Static helper methods for building test objects should be in `static` helper classes.

## Test Project README

Every `*.Tests` project README must include:

- A tests table with **method count** and **test case count** (Theories expand to multiple cases via `[InlineData]`).
- All trait categories used in the project.
- A skipped tests section listing each skip reason and count.
- A file structure diagram showing the `Tests/` layout.
