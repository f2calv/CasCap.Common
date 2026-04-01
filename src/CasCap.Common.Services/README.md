# CasCap.Common.Services

Feature-flag background service launcher and configuration abstractions.

## Purpose

Contains `FeatureFlagBgService<T>`, a generic `BackgroundService` that inspects bitwise `AppMode` flags at startup and launches the matching `IFeature<T>` implementations registered in the DI container. The `AddFeatureFlagService()` extension wires everything up.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Services

| Type | Description |
| --- | --- |
| `FeatureFlagBgService<T>` | Generic `BackgroundService` that resolves and executes `IFeature<T>` implementations whose `FeatureType` matches the configured `AppMode` flags |

### Extensions

| Extension | Description |
| --- | --- |
| `ServiceCollectionExtensions.AddFeatureFlagService()` | Registers `FeatureFlagBgService<T>` and its options into the DI container |

### Configuration

| Type | Description |
| --- | --- |
| `FeatureOptions<T>` | Record carrying the enabled `AppMode` flags — bound from configuration via `IOptions<FeatureOptions<T>>` |

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/microsoft.extensions.hosting.abstractions) |
| [Microsoft.Extensions.Options.DataAnnotations](https://www.nuget.org/packages/microsoft.extensions.options.dataannotations) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | `IFeature<T>`, `IFeatureOptions<T>`, `IAppConfig` contracts |
| `CasCap.Common.Extensions` | General-purpose helper utilities |
