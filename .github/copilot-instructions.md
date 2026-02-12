# CasCap.Common - Copilot Instructions

## Repository Overview

A .NET class library repository containing 8 NuGet packages with helper functions, extensions, utilities, and abstract classes for .NET applications.

### Libraries

| Library | Description | Targets | Packable |
|---------|-------------|---------|----------|
| **CasCap.Common.Caching** | Distributed caching (cache-aside pattern) with Memory/Disk local cache and Redis remote cache | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Extensions** | Common extension methods and helper utilities | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Logging** | Static logging abstraction via `ApplicationLogging` | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Net** | `HttpClientBase` abstract class for HTTP client wrappers (net8.0+ only via `#if`) | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Serialization.Json** | System.Text.Json serialization helpers | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Serialization.MessagePack** | MessagePack serialization helpers | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Services** | `FeatureFlagBgService<T>` and `IFeature<T>` abstractions | net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Testing** | xUnit test logging utilities | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |
| **CasCap.Common.Extensions.Diagnostics.HealthChecks** | Custom health check extensions | netstandard2.0; net8.0; net9.0; net10.0 | ✅ |

### Test Projects

| Project | Targets |
|---------|---------|
| CasCap.Common.Caching.Tests | net8.0; net9.0; net10.0 |
| CasCap.Common.Extensions.Tests | net8.0; net9.0; net10.0 |
| CasCap.Common.Net.Tests | net8.0; net9.0; net10.0 |
| CasCap.Common.Serialization.Tests | net8.0; net9.0; net10.0 |

## Build and Test

### Prerequisites

- **.NET SDK**: 10.0.x stable (see `global.json` — `allowPrerelease: false`)
- **Docker**: Required for Redis in caching tests

### Commands

```bash
# 1. Restore (required before build)
dotnet restore

# 2. Build
dotnet build --no-restore

# 3. Start Redis (required before caching tests)
docker run -d -p 6379:6379 --name cascap-redis redis

# 4. Run tests — ALWAYS use --maxcpucount:1
dotnet test --no-build --maxcpucount:1
```

> **CRITICAL**: Always use `--maxcpucount:1` — parallel execution causes failures due to `InlineData` and Redis `ClearOnStartup` property conflicts.

### Expected Build Behaviour

- Build produces nullability warnings (CS8604, CS8765) — these are **accepted and must not be "fixed"**.
- Multi-target builds take ~15–20 seconds.

## Project Configuration

### Key Files

| File | Purpose |
|------|---------|
| `Directory.Build.props` | C# 14.0, `ImplicitUsings`, `Nullable: enable`, `IsPackable: false` by default |
| `Directory.Packages.props` | Central package version management (`ManagePackageVersionsCentrally: true`) |
| `.editorconfig` | Code style rules (4-space indent, LF line endings, full formatting rules) |
| `global.json` | SDK constraint — stable releases only |
| `docker-compose.yml` | Redis and Redis UI (p3x-redis-ui) services |
| `GitVersion.yml` | Semantic versioning configuration |

### Suppressed Warnings

Configured in `Directory.Build.props`: `IDE1006`, `IDE0079`, `IDE0042`, `CS0162`, `S125`, `NETSDK1233`

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

## CI/CD Pipeline

### GitHub Actions (`.github/workflows/ci.yml`)

**Triggers**: Push (except `preview/**`), PRs to `main`, manual dispatch.

**Jobs**:

1. **lint** — Reusable workflow from `f2calv/gha-workflows`
2. **versioning** — GitVersion-based semantic versioning
3. **build** — Ubuntu-latest with Redis service container; uses `f2calv/gha-dotnet-nuget@v2`; test args include `--maxcpucount:1`
4. **release** — GitHub release (main branch only, when tag doesn't already exist)

## Making Changes

### Adding Code

1. Place code in the correct project by functionality
2. Follow `.editorconfig` style rules
3. Add XML documentation to all public API surface
4. Add tests in the corresponding `.Tests` project
5. Validate: `dotnet build --no-restore` → 0 errors
6. Validate: `dotnet test --no-build --maxcpucount:1` → all pass

### Adding Dependencies

1. Add version to `Directory.Packages.props`
2. Reference in `.csproj` **without** a version attribute:

   ```xml
   <PackageReference Include="PackageName" />
   ```

3. Run `dotnet restore`

### Creating New Projects

- Library projects inherit `Directory.Build.props` automatically
- Set `<IsPackable>true</IsPackable>` explicitly only for NuGet packages
- Test projects must **not** be packable (default) and must target `net8.0;net9.0;net10.0`

## Validation Checklist

- [ ] `dotnet restore` succeeds
- [ ] `dotnet build --no-restore` completes with 0 errors
- [ ] Redis is running (if testing caching)
- [ ] `dotnet test --no-build --maxcpucount:1` passes all tests
- [ ] Public API has XML documentation
- [ ] Properties separated by blank lines
- [ ] `ServiceProvider` instances are disposed in tests
- [ ] No shared mutable static state in test helpers

## Contributing

1. Fork the repository and create a feature branch
2. Follow all conventions documented above
3. Run the full validation checklist before submitting a PR
4. PRs target the `main` branch and require CI to pass
5. Versioning is automated via GitVersion — do not manually edit version numbers
6. When using Copilot to implement code quality or legibility improvements, update this file to capture any new conventions so they are applied consistently in future sessions
