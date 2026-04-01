# CasCap.Common.Caching.Tests

xUnit tests for `CasCap.Common.Caching`.

## Purpose

Verifies the multi-tier caching infrastructure — Memory, Disk, and Redis cache services, expiry synchronisation, and the `DistributedCacheService` orchestrator.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

> **Prerequisite:** A Redis instance must be running (see `docker-compose.yml` at the repository root).

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/microsoft.net.test.sdk) |
| [xunit](https://www.nuget.org/packages/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) |
| [coverlet.msbuild](https://www.nuget.org/packages/coverlet.msbuild) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Caching` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |
