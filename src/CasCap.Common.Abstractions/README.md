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
| `IFeatureOptions<T>` | Pairs with `IFeature<T>` to carry the enabled `AppMode` flags into the `BackgroundService` launcher |
| `IHausEventSink<T>` | Generic event sink contract. Domain events are fanned out to every registered `IHausEventSink<T>` implementation in parallel |
| `ILocalCache` | Abstraction for an in-process cache provider supporting `Get`, `Set`, `Delete`, and `DeleteAll` |
| `IMyBlob` | Represents a blob with associated metadata (`bytes`, `DateCreatedUtc`, `BlobName`, `SizeInBytes`, `HasImage`) |

### `IHausEventSink<T>` Contract

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

| Package | Version |
| --- | --- |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.5 |

This project has no project references.
