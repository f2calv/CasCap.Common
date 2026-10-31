# CasCap.Common.Services.TestHost

Test-only executable used by
[ProcessOutcomeTests](../CasCap.Common.Services.Tests/Tests/Unit/ProcessOutcomeTests.cs) to verify
application exit codes in a separate process. It is not a production application or NuGet package.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Modes

The executable accepts exactly one mode argument. Tests use only local hosts and synthetic
failures; no credentials or external services are required.

| Mode | Expected exit code | Behavior |
| --- | ---: | --- |
| `daemon-failure` | 1 | A background feature fails after host startup |
| `graceful-shutdown` | 0 | A background feature requests host shutdown |
| `migration-success` | 0 | A synthetic finite operation succeeds; no database is accessed |
| `migration-failure` | 1 | A synthetic finite operation throws; no database is accessed |

Missing, unknown or multiple arguments return exit code 1.

## Running the Tests

From the repository root with the .NET 10 SDK:

```bash
dotnet test --project src/CasCap.Common.Services.Tests/CasCap.Common.Services.Tests.csproj --filter-class CasCap.Common.Services.Tests.ProcessOutcomeTests
```

The test project builds this host for each target framework and locates its corresponding assembly.
