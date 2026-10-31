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
| `ApplicationExitStateTests` | 2 | 2 | Successful default state, DI registration and idempotent failure marking |
| `ApplicationMetadataTests` | 1 | 1 | Application version, source revision, repository, branch, workflow, and run metadata extraction |
| `CoalescingScheduleRunnerTests` | 6 | 7 | UTC exclusive cursors, slow-run coalescing, cancellation, callback faults and awaited timeout cleanup |
| `FeatureFlagBgServiceTests` | 8 | 9 | Finite siblings, first/later faults, requested stop, cancellation, disabled features, and awaited cleanup on shutdown or sibling failure |
| `FeatureFlagBgServiceHostTests` | 2 | 2 | Fatal child failure records a nonzero result before `StopHost`; run-to-completion remains successful |
| `ProcessOutcomeTests` | 1 | 4 | Child-process daemon failure, requested shutdown and finite migration success/failure exit codes |
| `SinkServiceCollectionExtensionsTests` | 3 | 3 | Every enabled sink keeps writing, the `QuerySinkType` sink answers queries with a last-registered fallback, and an earlier fallback query sink is replaced |
| **Total** | **23** | **28** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `BackgroundService` | Feature-flag background-service tests |
| `Hosting` | Shared application process-outcome tests |
| `Metadata` | Application metadata tests |
| `Scheduling` | UTC cron runner tests |
| `Sinks` | Event-sink registration tests |

### Skipped Tests

No declared or conditional skips.

## File Structure

```text
Tests/
└── Unit/
    ├── ApplicationExitStateTests.cs
    ├── ApplicationMetadataTests.cs
    ├── CoalescingScheduleRunnerTests.cs
    ├── FeatureFlagBgServiceHostTests.cs
    ├── FeatureFlagBgServiceTests.cs
    ├── ProcessOutcomeTests.cs
    └── SinkServiceCollectionExtensionsTests.cs
```
