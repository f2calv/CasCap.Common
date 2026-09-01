# CasCap.Common.Caching.Tests

xUnit tests for `CasCap.Common.Caching`.

## Purpose

Verifies the multi-tier caching infrastructure — Memory, Disk, and Redis cache services, expiry synchronisation, and the `DistributedCacheService` orchestrator.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

> **Prerequisite:** Redis-backed tests require a Redis instance (see `docker-compose.yml` at the repository root). The local-only lifecycle test has no Redis dependency.

## Dependencies

### NuGet Packages

| Package |
| --- |
| [xunit.v3](https://www.nuget.org/packages/xunit.v3) |
| [Microsoft.Testing.Extensions.CodeCoverage](https://www.nuget.org/packages/microsoft.testing.extensions.codecoverage) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Caching` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |

## Tests

All tests live in `CacheTests.cs`. Redis-backed tests require a running Redis instance; the local-only lifecycle test is self-contained.

| Test method | Cases | Coverage |
| --- | --- | --- |
| `ServiceCollectionSetupTests` | 8 | DI registration across all `AddCasCapCaching` overloads (default, `IConfiguration`, `CachingConfig`, `Action<>`) for Memory & Disk local caches |
| `RemoteCacheSvc_Sync` | 8 | Synchronous `IRemoteCache` set/get/delete with JSON & MessagePack |
| `RemoteCacheSvc_Async` | 4 | Asynchronous `IRemoteCache` round-trips |
| `RemoteCacheSvc_LuaTest` | 2 | Built-in Lua scripts for expiry retrieval & deletion |
| `DistCacheSvc_LocalOnlyLifecycle` | 2 | Local-only `IDistributedCache` lifecycle and `NullRemoteCache` resolution for null and whitespace remote connections |
| `DistCacheSvc_RemoteEnabledWithoutRegistration` | 1 | Clear `NullRemoteCache` activation failure when remote caching is enabled without a Redis connection |
| `DistCacheSvc_Test` | 4 | `IDistributedCache` local+remote orchestration, factory-delegate setter |
| `DistCacheSvc_DistributedLocking_Test` | 4 | Redlock-guarded cache-miss path, double-check after local eviction |
| `TestBgServices_Async` | 1 | `CacheExpiryBgService` start/stop lifecycle |
| `SlidingExpirationTest_Async` | 1 | Sliding expiration eviction timing |
| `AbsoluteExpirationTest_Async` | 1 | Absolute expiration eviction timing |
| **Total** | **36** | 11 methods |

### Trait Categories

| Category | Used by |
| --- | --- |
| `ServiceCollection` | `ServiceCollectionSetupTests` |
| `IRemoteCache` | `RemoteCacheSvc_Sync`, `RemoteCacheSvc_Async`, `RemoteCacheSvc_LuaTest` |
| `IDistributedCache` | `DistCacheSvc_LocalOnlyLifecycle`, `DistCacheSvc_RemoteEnabledWithoutRegistration`, `DistCacheSvc_Test`, `DistCacheSvc_DistributedLocking_Test` |
| `BackgroundService` | `TestBgServices_Async` |

`SlidingExpirationTest_Async` and `AbsoluteExpirationTest_Async` carry no trait category.

### Skipped Tests

None.

## File Structure

```text
Tests/
├── CacheTests.cs
├── MockApiService.cs
├── MockDto.cs
└── TestBase.cs
```
