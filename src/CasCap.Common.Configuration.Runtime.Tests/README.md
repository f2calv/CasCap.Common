# CasCap.Common.Configuration.Runtime.Tests

.NET 10/11 acceptance tests for typed runtime configuration, real EF Core PostgreSQL and SQLite adapters,
optional shared Redis cache-aside and an ASP.NET Core stub exercised through `WebApplicationFactory`.
The optional typed management API is exercised with real test-only cookie authentication.

## Running

From the repository root:

```powershell
docker compose --profile tests up -d --wait postgres-tests redis
dotnet test --project src\CasCap.Common.Configuration.Runtime.Tests\CasCap.Common.Configuration.Runtime.Tests.csproj --configuration Release
docker compose --profile tests stop postgres-tests
```

Use the repository's [Compose stack](../../docker-compose.yml). Tests create and drop databases;
use only disposable PostgreSQL and Redis test services, never application services.
Each fixture owns a GUID-named database. Cache tests delete only their own hashed keys; they never flush Redis.

SQLite tests have an independent file fixture with no PostgreSQL connection or fixture dependency.
Each owns a GUID-named directory under `.copilot-tracking/sqlite-tests` in its working directory
(override with `SQLITETEST_DIRECTORY`), applies real migrations and deletes only its owned files.
The same file initially contains an unrelated application table. To run just SQLite with Redis:

```powershell
dotnet test --project src\CasCap.Common.Configuration.Runtime.Tests\CasCap.Common.Configuration.Runtime.Tests.csproj --configuration Release --filter-class '*Sqlite*'
docker compose --profile sqlite-tests run --rm sqlite-tests
```

The Compose runner copies read-only source into its own writable build volume; embedded SQLite
uses a separate named volume, not a fictional database service. Test fixtures clean their files;
build/NuGet volumes persist for inspection and reuse. Default solution test discovery includes
these classes; no remote CI result is asserted.

Defaults come from [appsettings.Test.json](appsettings.Test.json), loaded optionally by the shared
configuration extension in the `Test` environment. The optional local override files load after it;
environment variables load last. Missing test settings are allowed when another provider supplies
the required connection string.

To select another **dedicated test server**, set
`RuntimeConfigurationTests__ConnectionStrings__Postgres`. The identity needs database creation
and deletion privileges. Set `RuntimeConfigurationTests__ConnectionStrings__Redis` to select the
corresponding dedicated Redis test service.

## Tests

### Published JIT and Native AOT

```powershell
docker compose --profile configuration-aot-tests run --rm configuration-aot-tests
```

The [separate console fixture](../CasCap.Common.Configuration.Aot.TestHost/README.md) is published
as real JIT and native executables. `PublishedConfigurationTests` is explicitly opted in by this
profile; ordinary test runs do not silently substitute managed execution for a missing native
artifact. Each case migrates with the JIT binary, exercises the selected binary, then verifies
history/state in a fresh process. Four cases cover PostgreSQL/SQLite times JIT/AOT, asserting
deployment mode, publication/history, writer races, validation, scope isolation and actual monitor
reload/expiry. No Redis service is needed. See the [AOT guide](../../docs/runtime-configuration.md#experimental-native-aot)
for warnings, platform and source-build distribution limits.

Four additional paired cases demonstrate runtime-built predicates and runtime-composed
join/group/aggregate queries. Each checks real JIT rows and then the exact EF `QueryNotPrecompiled`
native rejection against the same database; arbitrary exceptions do not count as success.

### Source-Declared Coverage

| Class | Methods | Expanded cases | Category | Coverage |
| --- | --- | --- | --- | --- |
| `RuntimeConfigurationCodecTests` | 10 | 13 | Serialization | Bounds, shape, undefined enum, nested/cross-property validation, cycles, round-trip, validators, payload limit, generated-metadata parity and skipped-validator rejection |
| `PublishedConfigurationTests` | 2 | 8 | Integration | Explicit published parity and paired dynamic-query native rejection with both real providers |
| `PostgresRuntimeConfigurationTests` | 9 | 11 | Integration | Migrations, history, first/existing-head races, 1/20 environment isolation, rejected writes, startup, reload, expiry |
| `RuntimeConfigurationResilienceTests` | 5 | 5 | Integration | Real database refusal/recovery, startup failure, replicas/replacement, durable regression, I/O-free monitor reads, cancellation/nonoverlap |
| `RuntimeConfigurationReloadTests` | 4 | 4 | Integration | Captured aggregate consistency, immutable bootstrap, listener/apply failures and server-enforced feature control |
| `RuntimeConfigurationCacheTests` | 8 | 11 | Integration | Shared Redis hits, source counts, SQL reconciliation, invalid/stale entries, expiry/eviction, startup authority, cached HTTP host, slow fill |
| `SqliteRuntimeConfigurationTests` | 12 | 18 | Integration | Dedicated/shared file migrations/no drift, unrelated table preservation, reopen/history, first/existing-head races, busy/locked conflicts, atomic rollback, 1/20 environment/application/contract isolation, invalid writes/reads, direct/cached HTTP startup/reload/expiry, warm-cache authority |
| `ManagementApiTests` | 14 | 45 | Integration | SQLite/Redis stateless hosts and replacement, typed preview/no write, audit identity, fixed-scope/operation policy matrix, named browser auth schemes, JSON CSRF, shape/enum/integral/metadata bounds, races, unavailable/no-retry, closed ApiExplorer metadata |
| `ManagementPostgresTests` | 2 | 2 | Integration | Real PostgreSQL HTTP writer race/audit and sanitized database outage while reader state/sentinel/feature remain accepted |
| `ManagementClientTests` | 2 | 3 | Integration | Actual typed browser client against cookies/antiforgery, preview/no write, publish, conflict and permission failures |
| Total | 68 | 120 | | Includes eight explicit published-process cases |

Earlier local Release baseline: 110 passed, 0 failed, 0 skipped, including all 47 management API cases
and three typed-client cases. The separate component/client suite passes all 24 cases.
The complete solution also builds with zero warnings and errors. Both provider model-drift
checks and offline migration SQL generation pass; PostgreSQL migrations/schema are unchanged.

The experimental AOT pilot's focused regression run passed 60 cases across .NET 10/11.
Its Linux amd64 published-process run passed all eight cases: four PostgreSQL/SQLite times JIT/AOT
parity cases and four paired dynamic-query rejection probes, with no failures or skips.
These are focused checks, not a rerun of the entire 120-case suite.
Native publication still emits upstream compatibility warnings.

The reload case asserts the standard
`OptionsMonitor<T>`, complete collection replacement, suppressed unchanged reloads and an unchanged
host singleton sentinel.

Database outage cases disable connections only to the fixture database and terminate only its
backends, then restore availability. Startup assertions require PostgreSQL's exact `55000` refusal:
the EF/provider 10 path wraps it in `InvalidOperationException`, while the 11 path exposes
`PostgresException` directly. Unrelated exceptions or a successful warm-cache startup fail.
Redis outage is a fault-injected connection exception, not
a real server interruption. Cache-age tests use a logical clock; the slow-fill test exercises a
real elapsed physical deadline. These are not Redis failover, large-fleet load or live rollout tests.
No automatic test claims successful remote CI execution.

### Skipped Tests

Eight published-process cases are explicit: ordinary runs do not execute them. The AOT Compose
profile opts them in and requires both real artifacts. No credential-dependent skip branches exist.

### Test Layout

```text
Tests/
|-- Unit/
|   `-- RuntimeConfigurationCodecTests.cs
`-- Integration/
    |-- TestBase.cs
    |-- ManualTimeProvider.cs
    |-- RuntimeConfigurationHostFactory.cs
    |-- RuntimeConfigurationCacheTests.cs
    |-- RuntimeConfigurationResilienceTests.cs
    |-- RuntimeConfigurationReloadTests.cs
    |-- RuntimeConfigurationFailureLog.cs
    |-- RuntimeTestConnectionStringsConfig.cs
    |-- SqliteTestBase.cs
    |-- SqliteRuntimeConfigurationTests.cs
    |-- ManagementTestSupport.cs
    |-- ManagementApiTests.cs
    |-- ManagementPostgresTests.cs
    |-- ManagementClientTests.cs
    |-- PublishedConfigurationTests.cs
    `-- PostgresRuntimeConfigurationTests.cs
```

The [stub host](../CasCap.Common.Configuration.TestHost/README.md) uses the actual provider and monitor.
Its observation endpoint is test-only, not a management API. The fixture explicitly opts into
[management controllers](../CasCap.Common.Configuration.Management/README.md) and installs cookie
sign-in middleware only from this test assembly. Replacement-host tests share fixture-owned Data
Protection keys, cleaned with the isolated SQLite files. Focused API execution uses
`--filter-class '*Management*'` with the same native .NET 11 MTP command.

The unavailable/missing-publication contract also uses an injected publisher to inspect sanitized
logs and single-attempt behavior; actual PostgreSQL outage coverage complements this fault injection.
The browser DTO is intentionally tiny and host-owned. The [RCL component suite](../CasCap.Common.Configuration.Blazor.Tests/README.md)
tests interactive typed editing and error recovery separately. Neither suite claims a real browser
asset download, production identity-provider integration, live rollout or remote CI execution.
