# CasCap.Common.Configuration.PostgreSql

Provisional .NET 10 EF Core PostgreSQL authority for typed runtime configuration. Use the source
project while validating; this package has not yet been released.

## Usage

Register `AddPostgresRuntimeConfiguration(settings, connectionString, baselineDto)` before dependent
hosted services. `RuntimeConfigurationOptions` fixes the application, arbitrary environment name,
stable DTO contract identifier and version for the host lifetime. Inject the standard
`IOptionsMonitor<T>` to observe validated reloads. The initial scope must already have a revision.
Only non-secret operational controls belong here.

The context owns the fixed `runtime_configuration` schema in either a dedicated or existing database.
It never migrates at startup; apply reviewed EF migrations externally. Reader-only deployments
need SELECT on this schema. Keep migration permissions separate from operator publication rights.

`PostgresRuntimeConfigurationStore<T>.PublishAsync` appends a complete validated snapshot and
advances its head in one transaction, using the expected current revision. `ReadRevisionAsync`
provides historical typed reads. Direct operator SQL must use the same atomic journal/head shape;
invalid payloads are rejected by readers, not activated. Database privileges must prevent UPDATE
or DELETE of journal rows; EF mappings alone do not enforce append-only permissions.

## Initial Slice Limits

One typed contract per host, full snapshots, bounded payloads and direct polling are supported.
Schema customization, idempotent command retries, paginated/as-of history, typed restore commands,
store-generation recovery, outbox/Redis projection and SQLite remain follow-up ADR phases.
Do not use this provisional slice for safety-critical controls or restore a database under running
readers. First-publication and existing-scope races produce explicit concurrency failures.

See [the ADR](../../docs/adr-0001-runtime-configuration.md) for the full intended scope.
