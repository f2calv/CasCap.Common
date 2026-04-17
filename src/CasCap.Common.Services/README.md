# CasCap.Common.Services

Feature-flag background service launcher and configuration abstractions.

## Purpose

Contains `FeatureFlagBgService`, a `BackgroundService` that inspects the configured `FeatureFlagConfig.EnabledFeatures` set at startup and launches the matching `IBgFeature` implementations registered in the DI container. The `AddFeatureFlagService()` extension wires everything up.

The older generic `FeatureFlagBgService<T>` (bitwise enum-based) is retained but marked `[Obsolete]`.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Services

| Type | Description |
| --- | --- |
| `FeatureFlagBgService` | `BackgroundService` that resolves and executes `IBgFeature` implementations whose `FeatureName` is present in the configured `FeatureFlagConfig.EnabledFeatures` set (or `IBgFeature.AlwaysEnabled`) |
| `FeatureFlagBgService<T>` | **[Obsolete]** Generic predecessor that used a bitwise enum via `IFeature<T>.FeatureType` |
| `GitMetadataBgService` | Background service that periodically logs git build metadata (repository, tag, branch, commit) from environment variables to aid debugging |

### Extensions

| Extension | Description |
| --- | --- |
| `ServiceCollectionExtensions.AddFeatureFlagService()` | Registers `FeatureFlagBgService` and configures `FeatureFlagConfig` from a set of enabled feature name strings. Optionally registers `GitMetadataBgService` when `addGitMetadataService=true` |
| `ServiceCollectionExtensions.AddFeatureFlagService<T>()` | **[Obsolete]** Bridge overload that converts a flags enum to a `HashSet<string>` and delegates to the non-generic overload |

### Models

| Type | Description |
| --- | --- |
| `GitMetadata` | Build metadata record from the CI/CD pipeline — properties bind to environment variables injected by GitHub Actions and Helm deployments (repository, branch, commit, tag, workflow name, run ID, run number) |

### Configuration

| Type | Description |
| --- | --- |
| `FeatureFlagConfig` | Configuration class carrying the `EnabledFeatures` string set — configured via `IOptions<FeatureFlagConfig>` |
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
