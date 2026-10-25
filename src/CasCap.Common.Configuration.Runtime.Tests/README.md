# CasCap.Common.Configuration.Runtime.Tests

.NET 10 acceptance tests for typed runtime configuration, the real EF Core PostgreSQL adapter
and an ASP.NET Core stub exercised through `WebApplicationFactory`.

## Running

From the repository root:

```powershell
docker compose --profile tests up -d --wait postgres-tests
dotnet test --project src\CasCap.Common.Configuration.Runtime.Tests\CasCap.Common.Configuration.Runtime.Tests.csproj --configuration Release
docker compose --profile tests stop postgres-tests
```

Use the repository's [Compose stack](../../docker-compose.yml). Tests create and drop databases;
use only a disposable test server, never a shared application server.

Defaults come from [appsettings.Test.json](appsettings.Test.json), loaded optionally by the shared
configuration extension in the `Test` environment. The optional local override files load after it;
environment variables load last. Missing test settings are allowed when another provider supplies
the required connection string.

To select another **dedicated test server**, set
`RuntimeConfigurationTests__ConnectionStrings__Postgres`. The identity needs database creation
and deletion privileges.

## Tests

| Class | Methods | Expanded cases | Category | Coverage |
| --- | --- | --- | --- | --- |
| `RuntimeConfigurationCodecTests` | 5 | 6 | Serialization | Bounds, unknown members, round-trip, validators, payload limit |
| `PostgresRuntimeConfigurationTests` | 8 | 8 | Integration | Migrations, history, conflicts, concurrent publication, environment isolation, startup, reload, invalid retention, expiry |
| Total | 13 | 14 | | |

Observed Release result: 14 passed, 0 failed, 0 skipped. The reload case asserts the standard
`OptionsMonitor<T>`, complete collection replacement, suppressed unchanged reloads and an unchanged
host singleton sentinel.

### Skipped Tests

None: there are no skip branches or credential-dependent exclusions.

### Test Layout

```text
Tests/
|-- Unit/
|   `-- RuntimeConfigurationCodecTests.cs
`-- Integration/
    |-- TestBase.cs
    |-- RuntimeConfigurationHostFactory.cs
    `-- PostgresRuntimeConfigurationTests.cs
```

The [stub host](../CasCap.Common.Configuration.TestHost/README.md) uses the actual provider and monitor,
not a fake store. Its observation endpoint is test-only, not a management API.
