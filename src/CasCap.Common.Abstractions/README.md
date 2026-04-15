# CasCap.Common.Abstractions

Core interface definitions shared across the CasCap ecosystem. This project provides the foundational contracts that other CasCap libraries and applications may depend upon.

## Purpose

This library contains no concrete implementations — only interfaces and abstractions that define the contracts between components.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Interfaces

| Interface | Description |
| --- | --- |
| `IAppConfig` | Marker interface implemented by all application configuration records to allow easy identification and generic constraint usage |
| `IFeature<T>` | Identifies a service launched via a bitwise feature-flag enum — exposes the `FeatureType` property and an `ExecuteAsync` entry point |
| `IEventSink<T>` | Generic event sink contract. Domain events are fanned out to every registered `IEventSink<T>` implementation in parallel |
| `ILocalCache` | Abstraction for an in-process cache provider supporting `Get`, `Set`, `Delete`, and `DeleteAll` |
| `IMyBlob` | Represents a blob with associated metadata (`bytes`, `DateCreatedUtc`, `BlobName`, `SizeInBytes`, `HasImage`) |
| `INotifier` | Abstracts a notification service capable of sending and receiving messages with optional attachment support |
| `INotificationMessage` | Represents an outgoing notification message (text, sender, recipients, attachments) |
| `INotificationAttachment` | Metadata for an attachment received as part of a notification (`Id`, `ContentType`) |
| `INotificationGroup` | Represents a named group in a notification service (`Id`, `Name`, members) |
| `INotificationResponse` | Response returned after sending a notification (`Timestamp`) |
| `IReceivedNotification` | Represents a notification received from an external messaging service (`Sender`, `GroupId`, `Message`, attachments) |
| `IAzBlobStorageConfig` | Exposes Azure Blob Storage connection properties (endpoint/connection string, container name, health check probe type) for feature-specific configuration records |
| `IAzTableStorageConfig` | Exposes Azure Table Storage connection properties (endpoint/connection string, health check probe type) for feature-specific configuration records |
| `IFeatureConfig<T>` | Pairs with `IFeature<T>` to carry the enabled `EnabledFeatures` flags into the `BackgroundService` launcher |
| `IKubeAppConfig` | Exposes Kubernetes-specific runtime properties (node name, pod name, namespace, pod IP, service account name) |
| `IMetricsConfig` | Exposes OpenTelemetry metric configuration (metric name prefix, OTel service name) |

### Enums

| Type | Description |
| --- | --- |
| `KubernetesProbeTypes` | `[Flags]` enum for Kubernetes container health probe types: `None`, `Readiness`, `Liveness`, `Startup` |

### `IEventSink<T>` Contract

```csharp
Task InitializeAsync(CancellationToken cancellationToken);
Task WriteEvent(T @event, CancellationToken cancellationToken = default);
IAsyncEnumerable<T> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default);
```

`InitializeAsync` and `HousekeepingAsync` have default no-op implementations so that sink authors only need to implement `WriteEvent` and `GetEvents`.

## Dependencies

The only valid dependencies for this library are other Abstractions-style libraries.

### NuGet Packages

| Package |
| --- |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/microsoft.extensions.hosting.abstractions) |

This project has no project references.
