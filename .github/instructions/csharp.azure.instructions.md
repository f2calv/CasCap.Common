---
description: 'Azure Table Storage entity and Redis key-naming conventions.'
applyTo: '**/*.cs'
---

# Cloud (Azure)

## Azure Table Storage

- **Column naming**: For high-volume line-item/reading entities where many thousands of rows are retrieved, use ultra-short column names (even single letters) to reduce payload size and improve retrieval speed. This optimization is not needed for low-volume snapshot/summary entities where readability is more important.
- **Expanded accessor properties**: High-volume `ITableEntity` reading entities with ultra-short column names must also expose full-name expression-bodied read-only accessor properties decorated with `[IgnoreDataMember]` for developer ergonomics (e.g. `public string IpAddress => ip;`). This provides readable access without adding storage overhead.
- **ReadingEntity constructors**: Reading entity constructors must accept the domain event record directly (e.g. `SensorReadingEntity(SensorEvent evt)`) — never individual properties unpacked at the call site. The constructor is responsible for mapping event properties to ultra-short column fields.
- **SnapshotEntity constructors**: When a snapshot entity's `RowKey` is always derivable from a property on the event (e.g. `DeviceId`, `NodeName`), the constructor should accept `(string partitionKey, TEvent evt)` and derive `RowKey` internally. Only pass `RowKey` as a separate parameter when it is a constant not present on the event (e.g. `"latest"` for single-device tables).
- **Entity-scoped PartitionKey**: When a reading entity stores data from multiple devices or sensors, `PartitionKey` should be the device/entity identifier (`DeviceId`, `NodeName`, `CameraId`, `DatapointId`, etc.) rather than a date string. Date-based PK (`yyMMdd`) is acceptable only for single-device-per-table scenarios.
- **Table name versioning**: When changing the `PartitionKey` or `RowKey` structure of an Azure Table entity, increment the table name version suffix (e.g. `readingsv1` → `readingsv2`). Old data under the previous key structure is orphaned by design — treat structural changes as fresh-start migrations.

## Redis Key Naming

When a project uses a feature enum (e.g. `AppFeature`) to gate services, derive the domain segment of every Redis key from the **lowercase enum member name** so that key prefixes stay tightly coupled to the feature taxonomy (e.g. `AppFeature.Billing` → `billing:`, `AppFeature.Notifications` → `notifications:`).

- **All lowercase**, colon-separated segments: `{domain}:{type}:{detail}`.
- **Domain** — derived from the feature enum member name via `nameof(AppFeature.Member).ToLowerInvariant()`. Cross-feature keys use a functional domain (e.g. `comms`, `lock`, `stats`).
- **Standard type segments**:

| Segment | Redis Type | Purpose |
| --- | --- | --- |
| `snapshot` | Hash | Current state (field per entity) |
| `series` | Sorted Set | Time-series readings (score = UTC ticks) |
| `stream` | Stream | Event/message streams |
| `cache` | String | Temporary data with TTL |
| `lock` | String | Distributed locks (Redlock) |
| `stats` | Hash | Observability / call counters with TTL |

- **Detail** — further qualifiers such as entity IDs, date partitions (`{yyMMdd}`, `{yyyy-MM-dd}`), or sub-categories (e.g. `values`, `timestamps`).
- **Consumer groups** — use `{domain}:{role}` format (e.g. `billing:processors`, `comms:agents`).
- **Stats keys** — date-partitioned with a 7-day TTL: `stats:{domain}:{yyyy-MM-dd}`. Hash fields are the method or operation names.
- **Lock keys** — `lock:{domain}:{resource}` format string, configured via `RedisKeyFormat` in `appsettings.json`.
- **Config-driven keys**: Snapshot and series key names should be stored in `appsettings.json` per-sink settings (via a settings dictionary), not hardcoded in service code. This allows key migration by config change alone.
- **Key sync on rename**: When renaming Redis keys, update `appsettings*.json`, Lua scripts, and any C# code that constructs or references the old key name in the same commit. Existing Redis data under the old key will be orphaned — treat key renames as a fresh-start migration.
