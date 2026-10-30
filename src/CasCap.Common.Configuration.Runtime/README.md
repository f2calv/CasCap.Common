# CasCap.Common.Configuration.Runtime

Provisional .NET 10 typed runtime configuration support. It reuses the prototype's standard
configuration-provider and complete dictionary-replacement approach, with validation before reload,
an isolated root and the standard `IOptionsMonitor<T>`. It is not a custom monitor implementation.

## Contract

Register one DTO per host through `AddRuntimeConfiguration<T>` and supply an
`IRuntimeConfigurationStore<T>` implementation. A complete initial durable revision is required:
missing data or unavailable storage fails startup. Expired revisions return to the supplied startup
baseline. Invalid refreshes log a sanitized error and retain the previous accepted snapshot.
Consumers must not mutate returned options; capture one aggregate per operation.

Durable adapters also expose [`IRuntimeConfigurationPublisher<T>`](Abstractions/IRuntimeConfigurationPublisher{T}.cs)
for typed expected-revision publication and authoritative reads. Optional caching decorates only
the reader contract, never this publisher.

Expiry of an already accepted override is checked locally before polling and after failed refreshes;
a newer invalid durable revision cannot extend that override indefinitely. Scheduling and in-flight
store operations can delay reconciliation, so expiry is not a hard real-time safety deadline.

The initial slice supports one contract, complete snapshots, direct polling and .NET 10 only.
It does not merge arbitrary deployment overrides, implement cross-contract atomicity or promise
rollback of failing application callbacks. Deployment settings and credentials stay immutable.
Register runtime configuration before consumers' hosted services.

No network access occurs during ordinary options reads. Polling currently reads one complete
bounded snapshot per interval; revision-only optimization is deferred until measured load warrants it.
See the [consumer and operations guide](../../docs/runtime-configuration.md),
[PostgreSQL adapter](../CasCap.Common.Configuration.PostgreSql/README.md),
[optional cache-aside trial](../CasCap.Common.Configuration.Redis/README.md) and
[design](../../docs/adr-0001-runtime-configuration.md).
