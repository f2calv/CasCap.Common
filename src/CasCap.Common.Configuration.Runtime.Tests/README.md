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

The test service uses loopback port `15443` and disposable in-memory container storage. Each test
creates, migrates and drops its own uniquely named database; no development database is reset.
A missing test service fails setup rather than silently skipping required coverage.

To select another **dedicated test server**, set
`RuntimeConfigurationTests__ConnectionStrings__Postgres`. The identity needs database creation
and deletion privileges. Do not point this at a shared application database.

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
