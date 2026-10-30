# Typed Runtime Configuration

Use the source-only .NET 10 runtime/PostgreSQL libraries for narrow, non-secret operational
controls that must change without restarting a host. Immutable deployment configuration remains
the default. Connections, authentication, DI topology and database/schema selection are not live controls.

The [ADR](adr-0001-runtime-configuration.md) separates the implemented slice from the broader
proposal. SQLite, management APIs/UI, typed restore commands, Redis outbox projection and Azure
authorities are not implemented. The optional Redis adapter is a bounded cache-aside trial only.

## Choose a Read Mode

| Mode | Dependencies | Freshness |
| --- | --- | --- |
| Direct PostgreSQL | Runtime and PostgreSQL source projects, PostgreSQL | Next successful poll, plus source/apply duration |
| Optional cache-aside | Direct provider plus Redis project and shared CasCap caching | Cache age or independent durable reconciliation, then polling/source duration |

Reference the source projects from the host while these packages are unpublished. The Redis
dependency stays out of the direct-provider projects. Ordinary `IOptionsMonitor<T>.CurrentValue`
reads use accepted in-process state, not the database or Redis.

Two replicas with the default five-second interval perform about **0.4 aggregate durable reads per
second** in direct mode, not a read every 0.4 seconds. Polls currently fetch one full bounded snapshot.
Redis can reduce repeated reads but adds dependencies and permitted staleness; it is not required
for this small workload, and the trial does not claim measured production speedup.

## Define a Typed Contract

Use a small DTO with explicit validation. For example:

```csharp
using System.ComponentModel.DataAnnotations;

public sealed record Controls
{
    [Range(0, 5)]
    public int Verbosity { get; init; } = 2;

    public bool FeatureEnabled { get; init; }
}
```

The codec rejects unknown JSON members, invalid member types, undefined enum values, cycles and
oversized/deep graphs. It runs recursive DataAnnotations, `IValidatableObject` and registered
`IValidateOptions<T>` validators before typed persistence and activation.
The DTO JSON limit is 65,536 UTF-8 bytes; maximum JSON depth is 32. Cross-property validation belongs
in the DTO or a registered validator, not in an arbitrary string setter.

Use `[JsonConverter]` on enum members/types when the wire format requires enum names.
Treat returned options as immutable, including nested collections.

## Register the Host

In the existing `Program.cs` or `AppHost` composition root:

```csharp
using CasCap.Common.Configuration.PostgreSql.Extensions;
using CasCap.Common.Configuration.Runtime.Models;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("RuntimeConfiguration")
    ?? throw new InvalidOperationException("Runtime configuration connection is required.");

builder.Services.AddPostgresRuntimeConfiguration(
    new RuntimeConfigurationOptions
    {
        Scope = new RuntimeConfigurationScope("example-application", builder.Environment.EnvironmentName),
        ContractId = "controls",
        ContractVersion = 1,
        RefreshInterval = TimeSpan.FromSeconds(5),
        StartupTimeout = TimeSpan.FromSeconds(15),
    },
    connection,
    new Controls { Verbosity = 2 });
```

Register before dependent hosted services. One contract per host is supported; duplicate registration
fails. Application and environment are required, case-sensitive strings of at most 100 characters,
not fixed environment enums. Use the host's actual environment consistently when publishing.
Contract ID is at most 100 characters and version is positive. Polling supports 100 milliseconds to
one hour; initial-load timeout is positive and at most five minutes.

The runtime root is isolated from deployment configuration. It does not merge live values into the
host's whole `IConfiguration`, replace Key Vault, or update unrelated options. A valid initial durable
revision is required before enabled startup; an unavailable authority or invalid initial value fails
startup rather than using a success-shaped default.

## Provision PostgreSQL Externally

The independently owned EF context uses `cascap_config` in either a standalone database or the
existing application database. Its objects are `revisions`, `heads` and `__ef_migrations_history`.
No runtime migration or automatic initial publication occurs.

From the repository root, an operator first generates and reviews offline SQL:

```powershell
dotnet ef migrations script --project src\CasCap.Common.Configuration.PostgreSql --configuration Release --context RuntimeConfigurationDbContext
dotnet ef migrations has-pending-model-changes --project src\CasCap.Common.Configuration.PostgreSql --configuration Release --context RuntimeConfigurationDbContext
```

The design-time factory uses a synthetic offline connection and does not start the application.
Keep `dotnet-ef` aligned with the resolved EF runtime version. Apply the reviewed SQL or migration
through an externally serialized, separately authorized operation against the exact target.
Never apply the test fixture to an application database.

Keep migration, publisher and reader identities separate:

- Reader: schema USAGE and SELECT on `heads` and `revisions`; no DDL or journal mutation rights.
- Publisher: read scopes/history, INSERT revisions and INSERT/UPDATE heads in one transaction.
  Do not grant UPDATE/DELETE on historical revisions.
- Migrator: owns required DDL and migration history. Do not run every replica with this identity.

Schema separation is not permission enforcement. Reusing an application identity can be an
explicitly accepted trial tradeoff, but it does not remove that account's existing privileges.
All records are non-secret; never use configuration history as a credential store.

## Publish and Read History

Use the concrete typed writer, with an audit actor derived from an authorized operator/principal:

```csharp
using CasCap.Common.Configuration.PostgreSql.Services;

var writer = services.GetRequiredService<PostgresRuntimeConfigurationStore<Controls>>();
var initial = await writer.PublishAsync(
    new Controls { Verbosity = 2 },
    expectedRevision: 0,
    actor: "example-operator",
    reason: "Initial baseline",
    cancellationToken: cancellationToken);

var temporary = await writer.PublishAsync(
    new Controls { Verbosity = 4 },
    expectedRevision: initial.Revision,
    actor: "example-operator",
    reason: "Temporary diagnostic control",
    expiresAtUtc: timeProvider.GetUtcNow().AddMinutes(2),
    cancellationToken: cancellationToken);

var earlier = await writer.ReadRevisionAsync(initial.Revision, cancellationToken);
```

Here `services` is the initialized host/operator service provider and `timeProvider` is its clock.
Provision the initial record before starting an enabled reader host; a dedicated operator process
can resolve the writer without starting reader hosted services.

Expected revision 0 means no head exists. A mismatch or competing writer throws
`DbUpdateConcurrencyException`; do not automatically retry with a newly read revision or blindly
retry an uncertain commit. Actor/reason must be nonempty and fit 200/500 characters; expiry must be
in the future. A rejected typed publication must not append a journal row.

Publication atomically appends the journal and advances its scoped head. Success means committed,
not yet applied by every host. Historical reads never move the active head.
To restore earlier content now, validate and publish that content as a **new** revision; a typed
restore/provenance API is deferred. Never move a head backward or edit old payloads.

Direct SQL is an operator-only escape hatch, not a management API. It must preserve the same
transaction/scope/expected-head checks, and it bypasses typed writer validation. Readers still
validate candidates. The cache trial does not require writers to invalidate entries; that increases
permitted delay as documented in its [README](../src/CasCap.Common.Configuration.Redis/README.md).

## Consume and Apply Safely

Inject standard `IOptionsMonitor<Controls>`. Capture one `CurrentValue` aggregate per operation
where consistency requires it; do not repeatedly read different revisions during one operation.
Subscriptions are disposable. A host-owned consumer should subscribe before reading the initial
value, serialize its initial/change application when necessary, and dispose the subscription at stop.

Only reload-safe operations belong in callbacks, such as updating the logging pipeline's existing
level switch. Standard options notifications are not a transaction: one failing listener does not
roll back other listeners or undo a consumer's side effects. Keep callback errors distinct from
invalid configuration and storage failures; application-specific apply/status behavior stays in
the consumer. Disabling a feature in the browser is not authorization for its server endpoint.

Complete dictionary replacement removes deleted collection keys. An unchanged serialized DTO emits
no reload notification even if its durable revision/expiry metadata changed.

## Failure and Expiry Contract

| Situation | Reader behavior |
| --- | --- |
| Missing, unavailable or invalid initial publication | Enabled startup fails |
| Invalid/unsupported refresh, durable revision regression or database failure | Logs sanitized failure type and retains accepted controls |
| Accepted temporary override reaches expiry | Returns to immutable startup baseline before a poll or after a failed refresh |
| Dependency recovers with a newer valid revision | Normal reconciliation applies it without replacing the process |
| Redis unavailable, invalid entry or stale/regressed cache | Trial consults the durable authority; if that fails, normal reader retention applies |
| Host shutdown during a store read | Propagates cancellation; refreshes do not overlap |

Expiry returns to the **supplied startup baseline**, not necessarily revision 1. It does not append a
revert revision or change the durable head. An already accepted expiry still applies after a newer
invalid head. Poll scheduling and in-flight I/O can delay it: there is no hard real-time deadline.
Database restore/generation replacement under running readers is unsupported.

For evidence, use a temporary level distinct from startup. Observe actual consumer behavior before,
during and after expiry on the same host identities. Same-value expiry and unchanged notification
counts cannot prove that reconciliation occurred. The short live logging check proved effective
switch/filter transitions, not exported-event delivery, all future failures or a long-duration soak.

## Validate and Troubleshoot

The [acceptance suite](../src/CasCap.Common.Configuration.Runtime.Tests/README.md) runs against
disposable PostgreSQL/Redis and the permanent `WebApplicationFactory` stub. Tests exercise validation,
concurrency, migration shape, isolation, real database connection refusal/recovery, replicas,
replacement, local expiry, cancellation and the optional cache trial.

Check bootstrap application/environment/contract/version, applied migration and scoped head before
enabling a host. On refresh failures, inspect the sanitized failure type and use an authorized
operator connection to inspect records; do not export payloads, connections or provider exception
details into logs. If a valid publication appears delayed, check polling, cache maximum age and
independent reconciliation settings. Never flush a shared Redis database to diagnose one key.

Feature controls still require application-owned authorization, health and audit policies. A
management API and reusable Blazor UI are future work, not endpoints exposed by these libraries.
