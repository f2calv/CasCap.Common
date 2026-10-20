# CasCap.Common.Abstractions

Shared contracts for .NET configuration, background features, caching, notifications, event sinks and telemetry.

## Installation

```bash
dotnet add package CasCap.Common.Abstractions
```

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

## Usage

Implement `IAppConfig` to give a configuration type a section name. On .NET 8+, the interface
requires that static property:

```csharp
using CasCap.Common.Abstractions;

public sealed record ExampleConfig : IAppConfig
{
    public static string ConfigurationSectionName => "Example";
    public bool Enabled { get; init; }
}
```

This package supplies contracts and shared models; it does not load application configuration or
start background features by itself. Use
[CasCap.Common.Configuration](https://github.com/f2calv/CasCap.Common/tree/main/src/CasCap.Common.Configuration)
for binding and
[CasCap.Common.Services](https://github.com/f2calv/CasCap.Common/tree/main/src/CasCap.Common.Services)
for feature execution.

## Public Contracts

| Interface | Description |
| --- | --- |
| `IAppConfig` | Application options contract with a section name on .NET 8+ |
| `IBgFeature` | Named background feature with `FeatureName` and `ExecuteAsync` |
| `IEventSink<T>` | Event write path, initialization and housekeeping for reference or value types |
| `ILocalCache` | Local cache get, set and deletion operations |
| `INotifier` | Notification send/receive operations |
| `INotificationMessage`, `INotificationAttachment`, `INotificationGroup`, `INotificationResponse`, `IReceivedNotification` | Notification payload and result contracts |
| `ICommsEventFormatter`, `ICommsGroupRouter` | Communication-event formatting and routing; .NET 8+ only |
| `IHttpAuditStore` | HTTP audit persistence; .NET 8+ only |
| `IMyBlob` | Blob data and metadata |
| `IAzBlobStorageConfig`, `IAzTableStorageConfig` | Azure storage configuration contracts |
| `IKubeAppConfig`, `IMetricsConfig` | Host identity and telemetry configuration |

## Shared Models

| Type | Description |
| --- | --- |
| `KubernetesProbeTypes` | Flags for readiness, liveness and startup probes |
| `CommsEvent` | Communication event with source, timestamp, message and optional JSON payload |
| `MediaReference` | Cached-media pointer; .NET 8+ only |
| `HttpAuditEntry` | HTTP request/response audit record; .NET 8+ only |
| `ApiAuthConfig` | Required basic-auth username/password and anonymous path prefixes; .NET 8+ only |
| `SinkConfig`, `SinkConfigParams`, `SinkSettingKeys` | Sink selection and settings; .NET 8+ only |

Probe tag conversion is supplied by
[CasCap.Common.Extensions.Diagnostics.HealthChecks](https://github.com/f2calv/CasCap.Common/tree/main/src/CasCap.Common.Extensions.Diagnostics.HealthChecks),
not this package.

## Event Sink Registration

On .NET 8+, decorate sink implementations with `[SinkType("Console")]`, enable the matching
`AvailableSinks` entry, and call `services.AddEventSinks<TEvent>(sinkConfig, assemblies)`.
Registration discovers implementations; it does not dispatch events. The application resolves the
registered `IEventSink<TEvent>` instances and invokes their write methods.

The write-path contract is:

```csharp
string SinkType { get; }
Task InitializeAsync(CancellationToken cancellationToken);
Task WriteEvent(T @event, CancellationToken cancellationToken = default);
Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default);
```

`SinkType` should match the attribute's name. On .NET 8+, initialization and housekeeping have
default no-op implementations. On .NET Standard 2.0, implement all interface methods explicitly.

An application can define additional query interfaces in a `CasCap` namespace. `AddEventSinks`
registers those interfaces against the enabled `QuerySinkType` when supported by that sink, otherwise
the last registered compatible sink. The `"Primary"` keyed alias identifies the selected query sink.
Every sink enabled in the registration remains available through the write-path enumeration;
query-capable fallback registrations from earlier calls may be replaced.

## Configuration Examples

The application chooses the section name and binds `SinkConfig`; registration does not bind it
automatically or supply concrete sink implementations.

### One Enabled Sink

```json
{
  "Sinks": {
    "AvailableSinks": {
      "Console": { "Enabled": true }
    }
  }
}
```

### Write to Multiple Sinks with a Preferred Query Sink

```json
{
  "Sinks": {
    "QuerySinkType": "Redis",
    "AvailableSinks": {
      "Console": { "Enabled": true },
      "Redis": { "Enabled": true }
    }
  }
}
```

`ApiAuthConfig` binds under `CasCap:ApiAuthConfig`; its `Username` and `Password` must be non-empty.
Supply them through a secret provider. `AnonymousPathPrefixes` defaults to `["/healthz"]`.
