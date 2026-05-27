# CasCap.Common.Abstractions

Core interface definitions shared across the CasCap ecosystem. This project provides the foundational contracts that other CasCap libraries and applications may depend upon.

## Installation

```bash
dotnet add package CasCap.Common.Abstractions
```

## Purpose

This library contains no concrete implementations — only interfaces and abstractions that define the contracts between components.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Interfaces

| Interface | Description |
| --- | --- |
| [`IAppConfig`](Abstractions/_IAppConfig.cs) | Marker interface implemented by all application configuration records to allow easy identification and generic constraint usage |
| [`IBgFeature`](Abstractions/IBgFeature.cs) | Identifies a feature-gated background service — exposes a string `FeatureName` and `ExecuteAsync` entry point. Matched case-insensitively against the enabled features set at startup |
| [`IFeature<T>`](Abstractions/IFeature%7BT%7D.cs) | **[Obsolete]** Generic predecessor of `IBgFeature` that used a bitwise feature-flag enum via `FeatureType`. Retained for backward compatibility |
| [`IEventSink<T>`](Abstractions/IEventSink%7BT%7D.cs) | Generic write-path event sink contract (unconstrained — accepts both reference and value types). Exposes a `SinkType` property for targeted dispatch filtering. Domain events are fanned out to every registered `IEventSink<T>` implementation in parallel. Read-path queries are defined by domain-specific interfaces (e.g. `IFroniusQuery`, `IKnxQuery`) in the consuming projects |
| [`ILocalCache`](Abstractions/ILocalCache.cs) | Abstraction for an in-process cache provider supporting `Get`, `Set`, `Delete`, and `DeleteAll` |
| [`IMyBlob`](Abstractions/IMyBlob.cs) | Represents a blob with associated metadata (`bytes`, `DateCreatedUtc`, `BlobName`, `SizeInBytes`, `HasImage`) |
| [`INotifier`](Abstractions/INotifier.cs) | Abstracts a notification service capable of sending and receiving messages with optional attachment support |
| [`INotificationMessage`](Abstractions/INotificationMessage.cs) | Represents an outgoing notification message (text, sender, recipients, attachments) |
| [`INotificationAttachment`](Abstractions/INotificationAttachment.cs) | Metadata for an attachment received as part of a notification (`Id`, `ContentType`) |
| [`INotificationGroup`](Abstractions/INotificationGroup.cs) | Represents a named group in a notification service (`Id`, `Name`, members) |
| [`INotificationResponse`](Abstractions/INotificationResponse.cs) | Response returned after sending a notification (`Timestamp`) |
| [`IReceivedNotification`](Abstractions/IReceivedNotification.cs) | Represents a notification received from an external messaging service (`Sender`, `GroupId`, `Message`, attachments) |
| [`IHttpAuditStore`](Abstractions/IHttpAuditStore.cs) | Abstraction for persisting HTTP audit entries (net8.0+ only) |
| [`IAzBlobStorageConfig`](Abstractions/_IAzBlobStorageConfig.cs) | Exposes Azure Blob Storage connection properties (endpoint/connection string, container name, health check probe type) for feature-specific configuration records |
| [`IAzTableStorageConfig`](Abstractions/_IAzTableStorageConfig.cs) | Exposes Azure Table Storage connection properties (endpoint/connection string, health check probe type) for feature-specific configuration records |
| [`IFeatureConfig<T>`](Abstractions/_IFeatureConfig%7BT%7D.cs) | **[Obsolete]** Pairs with `IFeature<T>` to carry the enabled `EnabledFeatures` flags into the `BackgroundService` launcher |
| [`IKubeAppConfig`](Abstractions/_IKubeAppConfig.cs) | Exposes Kubernetes-specific runtime properties (node name, pod name, namespace, pod IP, service account name) |
| [`IMetricsConfig`](Abstractions/_IMetricsConfig.cs) | Exposes OpenTelemetry metric configuration (metric name prefix, OTel service name) |

### Enums

| Type | Description |
| --- | --- |
| [`KubernetesProbeTypes`](_Enums.cs) | `[Flags]` enum for Kubernetes container health probe types: `None`, `Readiness`, `Liveness`, `Startup` |

### Configuration Types

| Type | Description |
| --- | --- |
| [`ApiAuthConfig`](Models/_ApiAuthConfig.cs) | Basic authentication settings for a REST API (`Username`, `Password`, `AnonymousPathPrefixes`) |
| [`SinkConfig`](Models/_SinkConfig.cs) | Dictionary of [`SinkTypeAttribute`](Attributes/SinkTypeAttribute.cs) name → [`SinkConfigParams`](Models/_SinkConfigParams.cs) |
| [`SinkConfigParams`](Models/_SinkConfigParams.cs) | Per-sink settings: `Enabled`, and a `Settings` dictionary for sink-specific key/value settings |
| [`SinkSettingKeys`](Models/SinkSettingKeys.cs) | Compile-time constants for common sink setting keys |

### Models

| Type | Description |
| --- | --- |
| [`HttpAuditEntry`](Abstractions/HttpAuditEntry.cs) | Represents a single HTTP request/response audit record — `Source`, `HttpMethod`, `RequestUri`, `StatusCode`, `ElapsedMs`, `RequestBody`, `ResponseBody` (net8.0+ only) |

### Event Models

| Type | Description |
| --- | --- |
| [`CommsEvent`](Models/CommsEvent.cs) | A comms stream entry with `Source`, `Message`, `TimestampUtc`, and optional `JsonPayload` for AI agent context |

### Attributes

| Attribute | Description |
| --- | --- |
| [`SinkTypeAttribute`](Attributes/SinkTypeAttribute.cs) | Decorates [`IEventSink<T>`](Abstractions/IEventSink%7BT%7D.cs) implementations with a string type name (e.g. `"Redis"`, `"AzureTables"`, `"Console"`) used by [`AddEventSinks()`](Extensions/SinkServiceCollectionExtensions.cs) for discovery |

### Extension Methods

| Method | Description |
| --- | --- |
| [`IServiceCollection.AddEventSinks<T>(SinkConfig, Assembly)`](Extensions/SinkServiceCollectionExtensions.cs) | Discovers and registers all [`IEventSink<T>`](Abstractions/IEventSink%7BT%7D.cs) implementations in the supplied assembly whose [`SinkTypeAttribute`](Attributes/SinkTypeAttribute.cs) name is enabled in [`SinkConfig`](Models/_SinkConfig.cs) |
| `KubernetesProbeTypes.GetTags()` | Returns the string health-check tag array for a probe type (`"ready"`, `"live"`, `"startup"`) |

### [`IEventSink<T>`](Abstractions/IEventSink%7BT%7D.cs) Contract

```csharp
string SinkType { get; }
Task InitializeAsync(CancellationToken cancellationToken);
Task WriteEvent(T @event, CancellationToken cancellationToken = default);
Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default);
```

`SinkType` is a self-describing property that each implementation sets to its own sink identifier (e.g. `"Redis"`, `"AzureTables"`, `"Console"`). This enables targeted dispatch filtering at runtime without reflection. The value should match the string passed to [`SinkTypeAttribute`](Attributes/SinkTypeAttribute.cs) on the same class.

`InitializeAsync` and `HousekeepingAsync` have default no-op implementations so that sink authors only need to implement `SinkType` and `WriteEvent`.

Read-path queries (retrieving stored events, snapshots) are intentionally **not** part of this interface. Each domain defines its own query interface (e.g. `IFroniusQuery`, `IKnxQuery`) with methods tailored to the domain's access patterns. This follows the Interface Segregation Principle — write-only sinks (e.g. Console, SignalR) are not forced to implement read methods they cannot support.

## Dependencies

The only valid dependencies for this library are other Abstractions-style libraries.

### NuGet Packages

| Package |
| --- |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/microsoft.extensions.hosting.abstractions) |

This project has no project references.
