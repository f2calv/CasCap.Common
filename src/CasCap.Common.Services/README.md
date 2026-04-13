# CasCap.Common.Services

Feature-flag background service launcher and configuration abstractions.

## Purpose

Contains `FeatureFlagBgService<T>`, a generic `BackgroundService` that inspects bitwise `EnabledFeatures` flags at startup and launches the matching `IFeature<T>` implementations registered in the DI container. The `AddFeatureFlagService()` extension wires everything up.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Services

| Type | Description |
| --- | --- |
| `FeatureFlagBgService<T>` | Generic `BackgroundService` that resolves and executes `IFeature<T>` implementations whose `FeatureType` matches the configured `EnabledFeatures` flags |
| `GitMetadataBgService` | Background service that periodically logs git build metadata (repository, tag, branch, commit) from environment variables to aid debugging |

### Extensions

| Extension | Description |
| --- | --- |
| `ServiceCollectionExtensions.AddFeatureFlagService<T>()` | Registers `FeatureFlagBgService<T>` and binds `FeatureConfig<T>` from configuration. Optionally registers `GitMetadataBgService` when `addGitMetadataService=true` |

### Models

| Type | Description |
| --- | --- |
| `GitMetadata` | Build metadata record from the CI/CD pipeline — properties bind to environment variables injected by GitHub Actions and Helm deployments (repository, branch, commit, tag, workflow name, run ID, run number) |

### Configuration

| Type | Description |
| --- | --- |
| `FeatureConfig<T>` | Record carrying the `EnabledFeatures` flags - bound from configuration via `IOptions<FeatureConfig<T>>` |

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/microsoft.extensions.hosting.abstractions) |
| [Microsoft.Extensions.Options.DataAnnotations](https://www.nuget.org/packages/microsoft.extensions.options.dataannotations) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | `IFeature<T>`, `IFeatureConfig<T>`, `IAppConfig` contracts |
| `CasCap.Common.Extensions` | General-purpose helper utilities |
