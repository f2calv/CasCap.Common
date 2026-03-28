# CasCap.Common.Abstractions

Core interface definitions shared across the entire home automation solution. Every feature library and CasCap.App.Server depend on this project for their foundational contracts.

## Purpose

This library contains no concrete implementations — only interfaces and abstractions that define the contracts between the solution's components.

### Interfaces

| Interface | Description |
| --- | --- |
| `IAppConfig` | Marker interface implemented by all application configuration records to allow easy identification and generic constraint usage |
| `IHausEventSink<T>` | Generic event sink contract. Feature monitor services fan out domain events to every registered `IHausEventSink<T>` implementation in parallel |
| `IHausServerHub` | SignalR hub server contract. Defines the methods that hub clients can invoke (`SendFroniusEvent`, `SendKnxTelegram`, `SendDoorBirdEvent`, `SendBuderusEvent`, `SendMessage`, `Broadcast`) |
| `IMyBlob` | Azure Blob Storage abstraction for uploading byte arrays |

### `IHausEventSink<T>` Contract

```csharp
Task InitializeAsync(CancellationToken cancellationToken);
Task WriteEvent(T @event, CancellationToken cancellationToken = default);
IAsyncEnumerable<T> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default);
```

`InitializeAsync` and `HousekeepingAsync` have default no-op implementations so that sink authors only need to implement `WriteEvent` and `GetEvents`.

## Dependencies

This project has **no external NuGet dependencies** and no project references, making it a zero-dependency foundation layer.
