# CasCap.Common.Testing

xUnit v3 test infrastructure — logging redirection and conditional-skip attributes for CI environments.

## Installation

```bash
dotnet add package CasCap.Common.Testing
```

## Purpose

Provides utilities that route `ILogger` output to xUnit's `ITestOutputHelper` via a custom `ILoggerProvider`, plus `[Fact]`/`[Theory]` attributes that automatically skip tests when running under Azure DevOps or GitHub Actions.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Extensions

| Extension | Description |
| --- | --- |
| `ServiceCollectionExtensions.AddXUnitLogging()` | Registers the test logging provider (`TestLogProvider`) into the DI container |

### Logging

| Type | Description |
| --- | --- |
| `TestLogProvider` | `ILoggerProvider` that forwards log entries to xUnit's `ITestOutputHelper` |
| `TestLogger` | `ILogger` implementation backing `TestLogProvider` |

### xUnit Attributes

| Attribute | Description |
| --- | --- |
| `SkipIfCIBuildFactAttribute` | Skips the `[Fact]` when running on any CI server (Azure DevOps or GitHub Actions) |
| `SkipIfCIBuildTheoryAttribute` | Skips the `[Theory]` when running on any CI server |
| `SkipIfGithubActionsBuildFactAttribute` | Skips the `[Fact]` when running on GitHub Actions |
| `SkipIfGithubActionsBuildTheoryAttribute` | Skips the `[Theory]` when running on GitHub Actions |
| `SkipIfAzureDevOpsBuildFactAttribute` | Skips the `[Fact]` when running on Azure DevOps |
| `SkipIfAzureDevOpsBuildTheoryAttribute` | Skips the `[Theory]` when running on Azure DevOps |

## Dependencies

### NuGet Packages

| Package |
| --- |
| [xunit.v3.extensibility.core](https://www.nuget.org/packages/xunit.v3.extensibility.core) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
