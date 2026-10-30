# CasCap.Common.Configuration.Redis

Optional .NET 10 cache-aside acceleration for typed runtime configuration, backed by an independently
authoritative store. This is a source-only trial, not a released package or a durable Redis provider.

## Usage

First register a durable provider and the existing shared CasCap cache. Then opt in through
[`AddRuntimeConfigurationCache<T, TStore>`](Extensions/ServiceCollectionExtensions.cs):

```csharp
using CasCap.Common.Configuration.PostgreSql.Extensions;
using CasCap.Common.Configuration.PostgreSql.Services;
using CasCap.Common.Configuration.Redis.Extensions;
using CasCap.Common.Configuration.Redis.Models;
using CasCap.Common.Configuration.Runtime.Models;

builder.Services.AddPostgresRuntimeConfiguration(
    new RuntimeConfigurationOptions
    {
        Scope = new RuntimeConfigurationScope("example-application", builder.Environment.EnvironmentName),
        ContractId = "controls",
    }, databaseConnection, baseline);
builder.Services.AddCasCapCaching(remoteCacheConnectionString: redisConnection);
builder.Services.AddRuntimeConfigurationCache<Controls, PostgresRuntimeConfigurationStore<Controls>>(
    new RuntimeConfigurationCacheOptions { AuthorityId = "example-database-schema" });
```

`databaseConnection`, `redisConnection`, `baseline` and the `Controls` DTO are host-owned.
Reuse an already registered cache rather than opening another Redis connection. Connections belong
in the existing secret-store chain, never in the runtime DTO.

The [consumer guide](../../docs/runtime-configuration.md) supplies the direct-provider setup,
typed publisher, migration and subscriber lifecycle. This package changes only the reader:
publication still uses the concrete PostgreSQL store and commits to PostgreSQL first.

## Configuration Examples

[`RuntimeConfigurationCacheOptions`](Models/RuntimeConfigurationCacheOptions.cs) is immutable
deployment configuration, not a live control:

```csharp
new RuntimeConfigurationCacheOptions
{
    AuthorityId = "example-database-schema",
    MaximumAge = TimeSpan.FromSeconds(30),
    ReconciliationInterval = TimeSpan.FromMinutes(1),
}
```

- `AuthorityId` is required, non-secret and unique to the durable database/schema or file. Use the
  same value across its replicas, and different values for different authorities.
- `MaximumAge` defaults to 30 seconds; supported bounds are 100 milliseconds to five minutes.
- `ReconciliationInterval` defaults to one minute, must be at least `MaximumAge`, and cannot exceed
  ten minutes.
- Cached entries use absolute expiry with up to ten percent shorter jitter, never sliding renewal.
  Authority identity, application, environment, contract ID and contract version form the hashed
  lowercase `configuration:cache:v1:...` key. The envelope repeats that identity.

## Behavior and Limits

[`CachedRuntimeConfigurationStore<T>`](Services/CachedRuntimeConfigurationStore{T}.cs) performs an
uncached durable read at startup, even with a warm cache. Later polls use shared
[`IDistributedCache`](../CasCap.Common.Caching/Abstractions/IDistributedCache.cs) cache-aside loading
and revalidate snapshots. Each instance independently bypasses Redis at its reconciliation interval.
Cached values are copied before returning; callers must still treat options as immutable.

Redis is **evictable cache only**. Deletion or eviction causes refill; it never deletes SQL history.
Malformed, wrong-scope, expired or previously superseded cache entries trigger a sanitized warning
and a durable read. Redis connection failures also fall back to the authority. If that read fails,
normal runtime-reader retention, expiry and sanitized failure behavior apply; Redis is not an
availability substitute for required initial PostgreSQL loading.

The host still owns shared Redis connection registration and its timeout/abort-connect policy.
`AddCasCapCaching` connects during registration; a connection-registration failure occurs before
this decorator can fall back. Configure that existing client appropriately if the application
must start while Redis is unavailable. The trial does not open a parallel connection or override
shared cache settings.

Typed publication and direct operator SQL deliberately do not invalidate this trial's entries.
They become visible after cache age expiry or independent reconciliation, followed by the next
poll and source-read duration. Bounds assume healthy dependencies and do not promise a hard deadline.
A delayed stale refill can remain cached, but cannot move a reader below a revision it already
observed. Reconciliation bypasses even a repeatedly stale cache. No outbox, fenced shared pointer,
pub/sub delivery guarantee, distributed writer lock or immediate read-your-write promise is added.

Shared caching suppresses concurrent fills per key within a process. Cross-process duplicate fills
can still occur; do not enable distributed locking without evaluating its cost and endpoint safety.
Cache calls have the shared client's timeout semantics; cancellation stops waiting but an in-flight
shared fill can finish afterward. Cache serialization is an internal format, not a public API.
A fill whose absolute deadline expires before storage is rejected with a sanitized warning and
retried directly against the authority, rather than extending the stale entry's lifetime.

This trial has no measured production performance benefit yet and is not deployed by opting into
the source project. Keep the direct provider for low-volume or stricter-freshness workloads.
See the [ADR](../../docs/adr-0001-runtime-configuration.md) and
[acceptance tests](../CasCap.Common.Configuration.Runtime.Tests/README.md).
