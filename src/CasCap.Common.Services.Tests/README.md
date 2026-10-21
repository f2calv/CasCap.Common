# CasCap.Common.Services.Tests

xUnit v3 tests for feature selection, background-service lifecycle, assembly-backed application metadata and event-sink registration.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Running

From the repository root with the .NET 10 SDK:

```bash
dotnet test --project src/CasCap.Common.Services.Tests/CasCap.Common.Services.Tests.csproj --framework net10.0
```

Tests create local hosts and in-memory sink implementations. No credentials or external service
are required. Host tests deliberately exercise child failures and shutdown paths.

## Tests

Counts are declared methods and cases per target framework, not execution results.

| Test class | Methods | Test cases | Coverage |
| --- | ---: | ---: | --- |
| `ApplicationMetadataTests` | 1 | 1 | Application version, source revision, repository, branch, workflow, and run metadata extraction |
| `FeatureFlagBgServiceTests` | 7 | 7 | Finite sibling completion, first and later faults, all-child completion with and without a requested stop, cancellation, and disabled features |
| `FeatureFlagBgServiceHostTests` | 2 | 2 | `BackgroundServiceExceptionBehavior.StopHost` after a later child fault, and a clean stop by a run-to-completion child |
| `SinkServiceCollectionExtensionsTests` | 3 | 3 | Every enabled sink keeps writing, the `QuerySinkType` sink answers queries with a last-registered fallback, and an earlier fallback query sink is replaced |
| **Total** | **13** | **13** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `BackgroundService` | Feature-flag background-service tests |
| `Metadata` | Application metadata tests |
| `Sinks` | Event-sink registration tests |

### Skipped Tests

No declared or conditional skips.

## File Structure

```text
Tests/
└── Unit/
    ├── ApplicationMetadataTests.cs
    ├── FeatureFlagBgServiceHostTests.cs
    ├── FeatureFlagBgServiceTests.cs
    └── SinkServiceCollectionExtensionsTests.cs
```
