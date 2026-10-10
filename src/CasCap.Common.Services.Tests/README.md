# CasCap.Common.Services.Tests

## Purpose

Verifies feature selection, background-service lifecycle behavior, and assembly-backed application metadata.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) |
| [Microsoft.Testing.Extensions.CodeCoverage](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage) |
| [xunit.v3](https://www.nuget.org/packages/xunit.v3) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Services` | Library under test |

## Tests

| Test class | Methods | Test cases | Coverage |
| --- | ---: | ---: | --- |
| `ApplicationMetadataTests` | 1 | 1 | Application version, source revision, repository, branch, workflow, and run metadata extraction |
| `FeatureFlagBgServiceTests` | 6 | 6 | Finite sibling completion, first and later faults, all-child completion, cancellation, and disabled features |
| `FeatureFlagBgServiceHostTests` | 1 | 1 | `BackgroundServiceExceptionBehavior.StopHost` after a later child fault |
| **Total** | **8** | **8** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `BackgroundService` | Feature-flag background-service tests |
| `Metadata` | Application metadata tests |

### Skipped Tests

None.

## File Structure

```text
Tests/
└── Unit/
    ├── ApplicationMetadataTests.cs
    ├── FeatureFlagBgServiceHostTests.cs
    └── FeatureFlagBgServiceTests.cs
```
