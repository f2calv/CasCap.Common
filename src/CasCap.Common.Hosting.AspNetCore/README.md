# CasCap.Common.AspNetCore

ASP.NET Core helpers for the CasCap feature-flag system — feature-gated controller discovery.

## Installation

```bash
dotnet add package CasCap.Common.AspNetCore
```

## Purpose

In a modular monolith the whole controller surface compiles into one process, but each deployment (pod/host) typically enables only a subset of features (see `CasCap.Common.Services` / `FeatureFlagConfig.EnabledFeatures`) and registers only that subset's services. Without gating, every controllers-enabled host maps every controller and returns **HTTP 500** when a controller's feature-scoped dependency is not registered — and advertises endpoints in Swagger it cannot actually serve.

Mark a controller with `[FeatureController(...)]` and call `AddFeatureGatedControllers(enabledFeatures)` so the controller is only routed (and shown in OpenAPI/Swagger) on hosts where one of its features is enabled. **Unmarked** controllers are always available (their dependencies must be registered on every host that maps controllers).

> This is the single-assembly (type-level) equivalent of gating controllers by `ApplicationPart` at the assembly level. Prefer assembly-level gating (`AddApplicationPart` per enabled feature) when each feature's controllers live in their own assembly, since that also avoids loading off-feature assemblies.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Usage

```csharp
// Controller — only mapped where the PafGen feature is enabled:
[ApiController]
[FeatureController(FeatureNames.PafGen)]
[Route("api/v{version:apiVersion}/paf/srzone")]
public sealed class SrZoneController(IPafPlotCollectionSource source) : ControllerBase { /* ... */ }

// Composition root:
builder.Services.AddControllers()
    .AddFeatureGatedControllers(enabledFeatures); // same set passed to AddFeatureFlagService(...)
```

### Types

| Type | Description |
| --- | --- |
| `FeatureControllerAttribute` | Marks a controller with the feature name(s) that must be enabled for it to be discovered. Unmarked = always available |
| `FeatureGatedControllerFeatureProvider` | `IApplicationFeatureProvider<ControllerFeature>` that prunes marked controllers whose feature is not enabled on the host |

### Extensions

| Extension | Description |
| --- | --- |
| `MvcBuilderExtensions.AddFeatureGatedControllers()` | Adds the `FeatureGatedControllerFeatureProvider` to the MVC application-part manager for the supplied enabled-feature set |

## Dependencies

### Framework References

| Reference | Purpose |
| --- | --- |
| `Microsoft.AspNetCore.App` | ASP.NET Core MVC application-part / controller-feature types (`IApplicationFeatureProvider<ControllerFeature>`, `IMvcBuilder`) |
