# CasCap.Common - Copilot Instructions

## Repository Overview

This is a .NET class library repository containing 8 NuGet packages with helper functions, extensions, utilities, and abstract classes for .NET applications. The project targets multiple .NET versions (netstandard2.0, net8.0, net9.0, net10.0) and consists of approximately 218 C# files with ~6,200 lines of code.

### Key Libraries

- **CasCap.Common.Caching** - Distributed caching with cache-aside pattern, supports Memory/Disk local cache and Redis remote cache
- **CasCap.Common.Extensions** - Common extension methods and helper utilities
- **CasCap.Common.Logging** - Logging abstractions
- **CasCap.Common.Net** - HTTP client base utilities
- **CasCap.Common.Serialization.Json/MessagePack** - Serialization helpers
- **CasCap.Common.Testing** - Testing utilities
- **CasCap.Common.Services** - Service abstractions
- **CasCap.Common.Extensions.Diagnostics.HealthChecks** - Health check extensions

## Build and Test Instructions

### Prerequisites

- **.NET SDK**: Version 10.0.102 or later (supports building net8.0, net9.0, and net10.0 targets)
- **Docker**: Required for running tests (Redis dependency)
- **Redis**: Tests require Redis running on localhost:6379

### Build Process

**ALWAYS follow these steps in order:**

1. **Restore packages** (required before building):
   ```bash
   dotnet restore
   ```

2. **Build** (Debug or Release):
   ```bash
   dotnet build --no-restore                    # Debug (default)
   dotnet build -c Release --no-restore         # Release
   ```
   - Build takes ~15-20 seconds
   - Expect 9 nullability warnings (these are acceptable and not errors)
   - Projects target multiple frameworks: netstandard2.0, net8.0, net9.0, net10.0

3. **Start Redis** (required before running tests):
   ```bash
   docker run -d -p 6379:6379 --name cascap-redis redis
   ```
   - Redis is MANDATORY for running tests in CasCap.Common.Caching.Tests
   - Alternatively, use: `docker-compose up -d redis`

4. **Run tests**:
   ```bash
   dotnet test --no-build --maxcpucount:1
   ```
   - **CRITICAL**: ALWAYS use `--maxcpucount:1` to disable parallel test execution
   - Parallel execution causes failures due to InlineData and Redis ClearOnStartup property
   - Tests run across 3 target frameworks (net8.0, net9.0, net10.0) per test project
   - Caching tests take ~24s per framework (total ~72s)
   - Total test execution: ~90-120 seconds

5. **Clean build artifacts** (if needed):
   ```bash
   dotnet clean
   ```
   - Or use PowerShell script: `./clean.ps1` (removes all bin/obj folders)

### Package Creation

Only projects with `<IsPackable>true</IsPackable>` in their .csproj generate NuGet packages. Test projects are not packable.

## Project Structure

### Directory Layout

```
/
├── .github/
│   ├── workflows/
│   │   └── ci.yml              # Main CI/CD workflow
│   ├── dependabot.yml          # Dependency updates
│   └── copilot-instructions.md # This file
├── .devcontainer/              # Dev container configuration
├── src/
│   ├── CasCap.Common.Caching/                      # Caching library (packable)
│   ├── CasCap.Common.Caching.Tests/               # Caching tests
│   ├── CasCap.Common.Extensions/                   # Extensions library (packable)
│   ├── CasCap.Common.Extensions.Tests/            # Extensions tests
│   ├── CasCap.Common.Extensions.Diagnostics.HealthChecks/ # Health checks (packable)
│   ├── CasCap.Common.Logging/                      # Logging library (packable)
│   ├── CasCap.Common.Net/                          # HTTP utilities (packable)
│   ├── CasCap.Common.Net.Tests/                   # Net tests
│   ├── CasCap.Common.Serialization.Json/          # JSON serialization (packable)
│   ├── CasCap.Common.Serialization.MessagePack/   # MessagePack serialization (packable)
│   ├── CasCap.Common.Serialization.Tests/         # Serialization tests
│   ├── CasCap.Common.Services/                     # Service abstractions (packable)
│   └── CasCap.Common.Testing/                      # Testing utilities (packable)
├── CasCap.Common.slnx          # Solution file (XML format)
├── Directory.Build.props       # Shared MSBuild properties
├── Directory.Packages.props    # Central package version management
├── GitVersion.yml              # Version configuration
├── global.json                 # SDK version constraints
└── docker-compose.yml          # Redis and Redis UI services
```

### Key Configuration Files

- **Directory.Build.props** - Defines common project settings:
  - Language: C# 14.0
  - ImplicitUsings: enabled
  - Nullable reference types: enabled
  - IsPackable: false by default (must be explicitly set to true)
  - Suppressed warnings: IDE1006, IDE0079, IDE0042, CS0162, S125, NETSDK1233

- **Directory.Packages.props** - Central package management:
  - ManagePackageVersionsCentrally: true
  - All package versions defined here, referenced without version in .csproj files

- **.editorconfig** - Code style rules:
  - Indent: 4 spaces
  - Line endings: LF (Unix-style)
  - Nullable reference types enabled
  - Expression-bodied members preferred for accessors/properties
  - Pattern matching preferred

- **global.json** - SDK constraints:
  - allowPrerelease: false (only use stable SDKs)

## CI/CD Pipeline

### GitHub Actions Workflow (.github/workflows/ci.yml)

The CI workflow uses reusable workflows from `f2calv/gha-workflows` and runs on:
- Push to any branch (except preview/\*\*)
- Pull requests to main branch
- Manual workflow dispatch

**Workflow Jobs:**

1. **lint** - Linting checks via reusable workflow
2. **versioning** - GitVersion-based semantic versioning
3. **build** - Main build job:
   - Runs on: ubuntu-latest
   - Services: Redis container on port 6379
   - Uses: `f2calv/gha-dotnet-nuget@v2` action
   - Test args: `--maxcpucount:1` (CRITICAL for passing tests)
   - Builds all target frameworks
   - Runs all tests
   - Creates NuGet packages for packable projects
4. **release** - Creates GitHub release (main branch only)

### Pre-commit Hooks (.pre-commit-config.yaml)

Configured hooks (if using pre-commit):
- XML/YAML/JSON validation
- Large file check (max 50KB)
- EOF fixer
- Trailing whitespace removal
- Markdown linting (MD013, MD034 disabled)

## Code Conventions

### Naming and Style

- **Interfaces**: Must start with 'I' (PascalCase)
- **Types/Classes**: PascalCase
- **Methods/Properties**: PascalCase
- **No 'this.' prefix**: Qualification disabled for fields/properties/methods
- **Braces**: Required for all blocks, new line before open brace
- **Nullability**: Enabled - expect and handle CS8604, CS8765 warnings in some serialization code

### Testing Conventions

- **Framework**: xUnit
- **Test Projects**: Named with .Tests suffix (e.g., CasCap.Common.Caching.Tests)
- **Dependencies**: Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, coverlet
- **Coverage**: coverlet.collector and coverlet.msbuild for code coverage
- **Test naming**: No specific pattern enforced

## Common Issues and Workarounds

### 1. Test Failures Due to Parallel Execution

**Symptom**: Tests fail intermittently or consistently when running with default settings.

**Cause**: InlineData and Redis ClearOnStartup property conflict with parallel execution.

**Solution**: ALWAYS use `--maxcpucount:1` flag:
```bash
dotnet test --maxcpucount:1
```

### 2. Redis Connection Failures

**Symptom**: Caching tests fail with connection errors to localhost:6379.

**Cause**: Redis not running or not accessible.

**Solution**: Start Redis before running tests:
```bash
docker run -d -p 6379:6379 --name cascap-redis redis
```

To verify Redis is running:
```bash
docker ps | grep redis
```

### 3. Build Warnings (Expected)

**Expected Warnings** (DO NOT attempt to fix):
- CS8604 in StringToIntConverter.cs (3 occurrences) - Nullable reference argument
- CS8765 in JsonTests.cs and MessagePackTests.cs (6 occurrences) - Nullability mismatch

These warnings are acceptable and part of the codebase. Focus on errors, not these specific warnings.

### 4. Multi-Targeting Build Times

**Observation**: Builds take 15-20 seconds due to building 3-4 target frameworks per project.

**Expected**: This is normal. The project targets netstandard2.0, net8.0, net9.0, and net10.0.

### 5. Package Restore

**Issue**: Build fails with package not found errors.

**Solution**: Always run `dotnet restore` before building. Do not skip this step.

## Making Changes

### Adding New Code

1. Identify the correct project based on functionality
2. Follow existing code style (enforced by .editorconfig)
3. Add tests in corresponding .Tests project
4. Ensure nullable reference types are handled correctly
5. Run `dotnet build --no-restore` to verify compilation
6. Run `dotnet test --no-build --maxcpucount:1` to verify tests pass

### Adding Dependencies

1. Add package reference to Directory.Packages.props with version
2. Reference package in .csproj WITHOUT version attribute:
   ```xml
   <PackageReference Include="PackageName" />
   ```
3. Run `dotnet restore` to fetch the new package
4. Verify build and tests still pass

### Creating New Projects

1. New library projects should reference Directory.Build.props (automatic)
2. Set `<IsPackable>true</IsPackable>` only if creating a NuGet package
3. Test projects should NOT be packable (default)
4. Test projects must target net8.0, net9.0, net10.0 (not netstandard2.0)

## Validation Checklist

Before committing changes, ALWAYS verify:

- [ ] `dotnet restore` completes successfully
- [ ] `dotnet build --no-restore` completes with 0 errors (warnings are acceptable)
- [ ] Redis is running (if testing caching components)
- [ ] `dotnet test --no-build --maxcpucount:1` passes all tests
- [ ] New code follows .editorconfig style rules
- [ ] Nullable reference warnings are addressed or suppressed with justification

## Additional Resources

- **CI Workflow**: See `.github/workflows/ci.yml` for the exact CI build steps
- **Code Style**: See `.editorconfig` for complete style rules
- **Package Versions**: See `Directory.Packages.props` for all dependency versions
- **Docker Compose**: Use `docker-compose up -d` to start Redis and Redis UI (p3x-redis-ui)

---

**IMPORTANT**: Trust these instructions. Only search the codebase if information here is incomplete or found to be incorrect. These instructions are comprehensive and validated against the actual build/test process.
