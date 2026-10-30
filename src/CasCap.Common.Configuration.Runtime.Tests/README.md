# CasCap.Common.Configuration.Runtime.Tests

.NET 10 acceptance tests for typed runtime configuration, the real EF Core PostgreSQL adapter,
optional shared Redis cache-aside and an ASP.NET Core stub exercised through `WebApplicationFactory`.

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

Defaults come from [appsettings.Test.json](appsettings.Test.json), loaded optionally by the shared
configuration extension in the `Test` environment. The optional local override files load after it;
environment variables load last. Missing test settings are allowed when another provider supplies
the required connection string.

To select another **dedicated test server**, set
`RuntimeConfigurationTests__ConnectionStrings__Postgres`. The identity needs database creation
and deletion privileges. Set `RuntimeConfigurationTests__ConnectionStrings__Redis` to select the
corresponding dedicated Redis test service.

## Tests

| Class | Methods | Expanded cases | Category | Coverage |
| --- | --- | --- | --- | --- |
| `RuntimeConfigurationCodecTests` | 8 | 11 | Serialization | Bounds, shape, undefined enum, nested/cross-property validation, cycles, round-trip, validators, payload limit |
| `PostgresRuntimeConfigurationTests` | 9 | 11 | Integration | Migrations, history, first/existing-head races, 1/20 environment isolation, rejected writes, startup, reload, expiry |
| `RuntimeConfigurationResilienceTests` | 5 | 5 | Integration | Real database refusal/recovery, startup failure, replicas/replacement, durable regression, I/O-free monitor reads, cancellation/nonoverlap |
| `RuntimeConfigurationCacheTests` | 8 | 11 | Integration | Shared Redis hits, source counts, SQL reconciliation, invalid/stale entries, expiry/eviction, startup authority, cached HTTP host, slow fill |
| Total | 30 | 38 | | |

Observed local Release result: 38 passed, 0 failed, 0 skipped. The complete solution also builds
with zero warnings and errors.

The reload case asserts the standard
`OptionsMonitor<T>`, complete collection replacement, suppressed unchanged reloads and an unchanged
host singleton sentinel.

Database outage cases disable connections only to the fixture database and terminate only its
backends, then restore availability. Redis outage is a fault-injected connection exception, not
a real server interruption. Cache-age tests use a logical clock; the slow-fill test exercises a
real elapsed physical deadline. These are not Redis failover, large-fleet load or live rollout tests.
No automatic test claims successful remote CI execution.

### Skipped Tests

None: there are no skip branches or credential-dependent exclusions.

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
    `-- PostgresRuntimeConfigurationTests.cs
```

The [stub host](../CasCap.Common.Configuration.TestHost/README.md) uses the actual provider and monitor,
not a fake store. Its observation endpoint is test-only, not a management API.
