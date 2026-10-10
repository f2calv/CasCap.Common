# CasCap.Common.Services

Feature-flag background service launcher and configuration abstractions.

## Installation

```bash
dotnet add package CasCap.Common.Services
```

## Purpose

Contains `FeatureFlagBgService`, a `BackgroundService` that inspects the configured `FeatureFlagConfig.EnabledFeatures` set at startup and launches the matching `IBgFeature` implementations registered in the DI container. The `AddFeatureFlagService()` extension wires everything up.

The launcher observes every enabled child for the full host lifetime. A child may complete successfully while other children continue running. Child faults propagate to the host, and completion of every enabled child before host cancellation is treated as an unexpected lifecycle failure. Host cancellation completes the launcher cleanly.

The older generic `FeatureFlagBgService<T>` (bitwise enum-based) is retained but marked `[Obsolete]`.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Services

| Type | Description |
| --- | --- |
| `FeatureFlagBgService` | `BackgroundService` that resolves, executes, and continuously observes enabled `IBgFeature` implementations until host cancellation |
| `FeatureFlagBgService<T>` | **[Obsolete]** Generic predecessor that used a bitwise enum via `IFeature<T>.FeatureType` |
| `ApplicationMetadataBgService` | Logs assembly-backed application metadata at startup and periodically to aid debugging |

### Extensions

| Extension | Description |
| --- | --- |
| `ServiceCollectionExtensions.AddFeatureFlagService()` | Registers `FeatureFlagBgService` and configures `FeatureFlagConfig` from a set of enabled feature name strings. Optionally registers `ApplicationMetadataBgService` when `addApplicationMetadataService=true` |
| `ServiceCollectionExtensions.AddFeatureFlagService<T>()` | **[Obsolete]** Bridge overload that converts a flags enum to a `HashSet<string>` and delegates to the non-generic overload |

### Models

| Type | Description |
| --- | --- |
| `ApplicationMetadata` | Immutable application version, source revision and build context read from generated assembly metadata |

### Configuration

| Type | Description |
| --- | --- |
| `FeatureFlagConfig` | Configuration class carrying the `EnabledFeatures` string set — configured via `IOptions<FeatureFlagConfig>` |
| `ApplicationMetadataConfig` | Periodic metadata logging options; `LogInterval` defaults to one minute and must be positive |
| `FeatureConfig<T>` | **[Obsolete]** Record carrying the `EnabledFeatures` flags — bound from configuration via `IOptions<FeatureConfig<T>>` |

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/microsoft.extensions.hosting.abstractions) |
| [Microsoft.Extensions.Options.DataAnnotations](https://www.nuget.org/packages/microsoft.extensions.options.dataannotations) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | `IBgFeature`, `IAppConfig` contracts |
| `CasCap.Common.Extensions` | General-purpose helper utilities |
