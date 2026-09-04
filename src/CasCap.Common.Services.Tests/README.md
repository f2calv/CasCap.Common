# CasCap.Common.Services.Tests

## Purpose

Verifies feature selection, finite-child handling, fault propagation, cancellation, and generic-host shutdown behavior for `FeatureFlagBgService`.

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
| `FeatureFlagBgServiceTests` | 6 | 6 | Finite sibling completion, first and later faults, all-child completion, cancellation, and disabled features |
| `FeatureFlagBgServiceHostTests` | 1 | 1 | `BackgroundServiceExceptionBehavior.StopHost` after a later child fault |
| **Total** | **7** | **7** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `BackgroundService` | All tests |

### Skipped Tests

None.

## File Structure

```text
Tests/
└── Unit/
    ├── FeatureFlagBgServiceHostTests.cs
    └── FeatureFlagBgServiceTests.cs
```
