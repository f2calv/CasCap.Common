# CasCap.Common

[cascap.common.caching-badge]: https://img.shields.io/nuget/v/CasCap.Common.Caching?color=blue
[cascap.common.caching-url]: https://nuget.org/packages/CasCap.Common.Caching
[cascap.common.extensions-badge]: https://img.shields.io/nuget/v/CasCap.Common.Extensions?color=blue
[cascap.common.extensions-url]: https://nuget.org/packages/CasCap.Common.Extensions
[cascap.common.extensions.diagnostics.healthchecks-badge]: https://img.shields.io/nuget/v/CasCap.Common.Extensions.Diagnostics.HealthChecks?color=blue
[cascap.common.extensions.diagnostics.healthchecks-url]: https://nuget.org/packages/CasCap.Common.Extensions.Diagnostics.HealthChecks
[cascap.common.logging-badge]: https://img.shields.io/nuget/v/CasCap.Common.Logging?color=blue
[cascap.common.logging-url]: https://nuget.org/packages/CasCap.Common.Logging
[cascap.common.net-badge]: https://img.shields.io/nuget/v/CasCap.Common.Net?color=blue
[cascap.common.net-url]: https://nuget.org/packages/CasCap.Common.Net
[cascap.common.Serialization.json-badge]: https://img.shields.io/nuget/v/CasCap.Common.Serialization.Json?color=blue
[cascap.common.Serialization.json-url]: https://nuget.org/packages/CasCap.Common.Serialization.Json
[cascap.common.Serialization.messagepack-badge]: https://img.shields.io/nuget/v/CasCap.Common.Serialization.MessagePack?color=blue
[cascap.common.Serialization.messagepack-url]: https://nuget.org/packages/CasCap.Common.Serialization.MessagePack
[cascap.common.testing-badge]: https://img.shields.io/nuget/v/CasCap.Common.Testing?color=blue
[cascap.common.testing-url]: https://nuget.org/packages/CasCap.Common.Testing

![CI](https://github.com/f2calv/CasCap.Common/actions/workflows/ci.yml/badge.svg) [![Coverage Status](https://coveralls.io/repos/github/f2calv/CasCap.Common/badge.svg?branch=main)](https://coveralls.io/github/f2calv/CasCap.Common?branch=main) [![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=f2calv_CasCap.Common&metric=code_smells)](https://sonarcloud.io/component_measures/metric/code_smells/list?id=f2calv_CasCap.Common)

A .NET class library repository containing 8 NuGet packages with helper functions, extensions, utilities, and abstract classes for .NET applications.

| Library                                           | Package                                                                                                |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| CasCap.Common.Caching                             | [![Nuget][cascap.common.caching-badge]][cascap.common.caching-url]                                     |
| CasCap.Common.Extensions                          | [![Nuget][cascap.common.extensions-badge]][cascap.common.extensions-url]                               |
| CasCap.Common.Extensions.Diagnostics.HealthChecks | [![Nuget][cascap.common.extensions.diagnostics.healthchecks-badge]][cascap.common.extensions.diagnostics.healthchecks-url] |
| CasCap.Common.Logging                             | [![Nuget][cascap.common.logging-badge]][cascap.common.logging-url]                                     |
| CasCap.Common.Net                                 | [![Nuget][cascap.common.net-badge]][cascap.common.net-url]                                             |
| CasCap.Common.Serialization.Json                  | [![Nuget][cascap.common.Serialization.json-badge]][cascap.common.Serialization.json-url]               |
| CasCap.Common.Serialization.MessagePack           | [![Nuget][cascap.common.Serialization.messagepack-badge]][cascap.common.Serialization.messagepack-url] |
| CasCap.Common.Testing                             | [![Nuget][cascap.common.testing-badge]][cascap.common.testing-url]                                     |

## Libraries

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
|---------|----------|
| CasCap.Common.Caching.Tests | net8.0; net9.0; net10.0 |
| CasCap.Common.Extensions.Tests | net8.0; net9.0; net10.0 |
| CasCap.Common.Net.Tests | net8.0; net9.0; net10.0 |
| CasCap.Common.Serialization.Tests | net8.0; net9.0; net10.0 |

## Prerequisites

- **.NET SDK**: 10.0.x stable (see `global.json` — `allowPrerelease: false`)
- **Docker**: Required for Redis in caching tests

## Build and Test

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
|------|----------|
| `Directory.Build.props` | C# 14.0, `ImplicitUsings`, `Nullable: enable`, `IsPackable: false` by default |
| `Directory.Packages.props` | Central package version management (`ManagePackageVersionsCentrally: true`) |
| `.editorconfig` | Code style rules (4-space indent, LF line endings, full formatting rules) |
| `global.json` | SDK constraint — stable releases only |
| `docker-compose.yml` | Redis and Redis UI (p3x-redis-ui) services |
| `GitVersion.yml` | Semantic versioning configuration |

### Suppressed Warnings

Configured in `Directory.Build.props`: `IDE1006`, `IDE0079`, `IDE0042`, `CS0162`, `S125`, `NETSDK1233`

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
6. When using Copilot to implement code quality or legibility improvements, update the [copilot-instructions.md](.github/copilot-instructions.md) to capture any new conventions so they are applied consistently in future sessions
