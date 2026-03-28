# CasCap.Common.Caching.Tests

xUnit tests for `CasCap.Common.Caching`.

## Purpose

Verifies the multi-tier caching infrastructure — Memory, Disk, and Redis cache services, expiry synchronisation, and the `DistributedCacheService` orchestrator.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

> **Prerequisite:** A Redis instance must be running (see `docker-compose.yml` at the repository root).

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Microsoft.NET.Test.Sdk` | 18.3.0 |
| `xunit` | 2.9.3 |
| `xunit.runner.visualstudio` | 3.1.5 |
| `coverlet.collector` | 8.0.1 |
| `coverlet.msbuild` | 8.0.1 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Caching` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |
