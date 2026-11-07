# CasCap.Common.Hosting.AspNetCore.Tests

xUnit v3 tests for the request summary, health-check filter and log privacy defaults in `CasCap.Common.Hosting.AspNetCore`.

**Target framework:** `net11.0`

## Running

From the repository root with the .NET 11 SDK:

```bash
dotnet test --project src/CasCap.Common.Hosting.AspNetCore.Tests/CasCap.Common.Hosting.AspNetCore.Tests.csproj
```

Each request logging test starts an in-process `TestServer` host with the real Serilog pipeline, an
in-memory sink and a forwarded Microsoft logging provider. No credentials, network or external
service are required. Those tests replace the static Serilog logger, so they stay in one test class
and run sequentially. The health-check filter tests build a standalone logger and leave the static
logger untouched.

## Tests

Counts are declared methods and expanded data rows, not execution results.

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `HealthCheckFilterTests` | 2 | 7 | `AddCasCapDefaults` drops framework health-check events below `Warning`, keeps failures and application events that mention health checks; `IsHealthCheckNoise` ignores events without `SourceContext` |
| `RequestLoggingTests` | 4 | 10 | `UseCasCapRequestLogging` levels for success, failure, unmatched, health and static-file requests; route templates instead of identifier-bearing paths and query strings; slow and streaming requests; one exception event; application and forwarded events without `RequestPath` |
| **Total** | **6** | **17** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `HealthCheckFilter` | `HealthCheckFilterTests` |
| `RequestLogging` | `RequestLoggingTests` |

### Skipped Tests

No declared or conditional skips.

## File Structure

```text
Tests/
├── CapturingLoggerProvider.cs
├── CapturingSink.cs
├── Integration/
│   └── RequestLoggingTests.cs
└── Unit/
    └── HealthCheckFilterTests.cs
```
