# CasCap.Common.Configuration.Sqlite

Source-only .NET 10 EF Core SQLite authority for complete typed runtime controls with immutable
revision audit and expected-revision publication. Reference the source project; it is not released.

## Usage

Register `AddSqliteRuntimeConfiguration(settings, connectionString, baseline)` before dependent
hosted services. Consume the standard `IOptionsMonitor<T>`; a valid initial durable publication
is required. Expired controls return to the captured startup baseline, not a preceding revision.
See the [consumer guide](https://github.com/f2calv/CasCap.Common/blob/main/docs/runtime-configuration.md)
for typed validation, polling and optional shared Redis cache-aside.

The uncached
[`IRuntimeConfigurationPublisher<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Runtime/Abstractions/IRuntimeConfigurationPublisher%7BT%7D.cs)
publishes complete snapshots; the concrete `SqliteRuntimeConfigurationStore<T>` also exposes
`ReadRevisionAsync` for immutable historical reads. Dedicated restore, as-of and paginated history
APIs are deferred.

## Configuration Examples

```csharp
using CasCap.Common.Configuration.Runtime.Models;
using CasCap.Common.Configuration.Sqlite.Extensions;

services.AddSqliteRuntimeConfiguration(
    new RuntimeConfigurationOptions
    {
        Scope = new RuntimeConfigurationScope("example-application", "example-environment"),
        ContractId = "controls",
        RefreshInterval = TimeSpan.FromSeconds(5),
    },
    "Data Source=configuration.db;Pooling=False;Default Timeout=5",
    new Controls());
```

`Controls` is the application's validated, non-secret DTO. Application, arbitrary environment and
contract identify independent heads in the same file. One registered scope/contract per host is
supported. File location and connection settings remain deployment configuration.

## Schema and Operations

Apply real EF migrations externally, before startup. The runtime never calls `EnsureCreated`,
`Migrate` or publishes an initial revision. Generate/review offline SQL and verify model drift:

```powershell
dotnet ef migrations script --project src\CasCap.Common.Configuration.Sqlite --configuration Release
dotnet ef migrations has-pending-model-changes --project src\CasCap.Common.Configuration.Sqlite --configuration Release
```

The offline design-time factory never opens a file. Apply reviewed SQL or run an explicitly
configured migration context against the operator-selected file in a serialized external step.
SQLite cannot generate EF idempotent scripts; select the appropriate migration range.

Owned tables are `cascap_config_revisions`, `cascap_config_heads` and
`cascap_config_migrations_history`; application tables are untouched. EF's SQLite migrator also
uses its framework-owned `__EFMigrationsLock` table. Do not remove that lock while a migration is
running. A separate application context must use its own migration history and serialized migrator.

SQLite permits one writer at a time. A non-deferred write transaction reads the expected head,
appends the journal and advances the head atomically. Stale revisions, primary-key publication
races and SQLite busy/locked errors (5/6, including extended variants) become sanitized
`DbUpdateConcurrencyException`; no automatic retry or phantom audit append occurs. Re-read the
authority before deciding whether to retry. Other storage/constraint errors remain failures.
The connection's timeout bounds SQLite's own lock wait; cancellation does not interrupt every
synchronous native SQLite operation.

Sharing a file shares permissions and contention, not independently enforceable SQL roles.
Use durable local storage and operator-managed backups/checkpoints. Redis does not replicate files,
provide SQLite high availability or authorize a writable WAL file on a multi-node network filesystem.
Use PostgreSQL when multiple highly available writers are needed.

## Validation

The permanent runtime acceptance suite uses real disposable files, real migrations, shared-file
preservation, first/existing-head races and direct/cache-aside HTTP hosts. The root
[Compose test runner](https://github.com/f2calv/CasCap.Common/blob/main/docker-compose.yml)
runs the same SQLite classes without PostgreSQL.
