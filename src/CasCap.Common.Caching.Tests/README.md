# CasCap.Common.Caching.Tests

xUnit v3 tests for memory, disk and Redis caching, registration, distributed locking and expiry behavior in `CasCap.Common.Caching`.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Running

From the repository root, with the .NET 10 SDK and Docker Compose:

```bash
docker compose up -d --wait redis
dotnet test --project src/CasCap.Common.Caching.Tests/CasCap.Common.Caching.Tests.csproj --framework net10.0
docker compose down
```

Use only a disposable Redis instance at `localhost:6379`: tests can clear databases and need
administrative commands. The test fixture selects databases 8, 9 and 10 for the corresponding target
frameworks. When running the whole solution, use `--max-parallel-test-modules 1` as described in the
[repository quick start](../../README.md#development).

`RegistrationTests` and the local-only lifecycle checks do not require Redis. To run only the
registration class:

```bash
dotnet test --project src/CasCap.Common.Caching.Tests/CasCap.Common.Caching.Tests.csproj --framework net10.0 --filter-class CasCap.Common.Caching.Tests.RegistrationTests
```

`LeadershipRunnerTests` and `LeadershipRegistrationTests` also require no Redis or credentials.
Run both with `--filter-class CasCap.Common.Caching.Tests.Leadership*`. Lease/factory fakes and
cleanup gates exercise cancellation, contention, release ordering, reacquisition and failure
propagation; these are not live renewal, quorum or network-partition tests.

## Tests

Counts are declared methods and expanded data rows per target framework, not a record of a test run.

| Test class | Methods | Test cases | Scope |
| --- | ---: | ---: | --- |
| `CacheTests` | 11 | 36 | Memory/disk registration, local-only lifecycle, missing remote registration, Redis JSON/MessagePack round-trips, Lua, distributed locking and expiry |
| `RegistrationTests` | 2 | 2 | All configuration properties are copied; repeated registration is idempotent |
| `LeadershipRunnerTests` | 10 | 12 | Ownership, contention, acquisition cancellation, loss/shutdown cleanup, fresh runs and worker/acquisition/observation/release fault propagation |
| `LeadershipRegistrationTests` | 4 | 14 | Shared options, exact cache/election timings and fallback, nested validation, first-registration ownership and missing factory |
| **Total** | **27** | **64** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `ServiceCollection` | `ServiceCollectionSetupTests` |
| `IRemoteCache` | `RemoteCacheSvc_Sync`, `RemoteCacheSvc_Async`, `RemoteCacheSvc_LuaTest` |
| `IDistributedCache` | `DistCacheSvc_LocalOnlyLifecycle`, `DistCacheSvc_RemoteEnabledWithoutRegistration`, `DistCacheSvc_Test`, `DistCacheSvc_DistributedLocking_Test` |
| `BackgroundService` | `TestBgServices_Async` |
| `Registration` | `RegistrationTests` |
| `Leadership` | Shared leadership lifecycle and configuration |
| `Integration` | Credential-free DI/configuration checks in `LeadershipRegistrationTests` |

`SlidingExpirationTest_Async` and `AbsoluteExpirationTest_Async` carry no trait category.

### Skipped Tests

No declared or conditional skips. Redis-backed tests fail rather than skip if the service is absent.

## File Structure

```text
Tests/
├── CacheTests.cs
├── MockApiService.cs
├── MockDto.cs
├── RegistrationTests.cs
├── TestBase.cs
├── Integration/
│   └── LeadershipRegistrationTests.cs
└── Unit/
    └── LeadershipRunnerTests.cs
```
