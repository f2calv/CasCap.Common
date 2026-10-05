# CasCap.Common.Hosting.AspNetCore

ASP.NET Core hosting defaults for feature-gated controllers, Serilog logging, and OpenTelemetry metrics, traces, and logs.

## Installation

```bash
dotnet add package CasCap.Common.Hosting.AspNetCore
```

## Feature-Gated Controllers

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

## Serilog

`GetBootstrapLogger` provides early startup logging, while `InitializeSerilog` configures Serilog as the host logging pipeline. Serilog owns the pipeline through `UseSerilog(..., writeToProviders: true)` so one `Serilog` configuration section controls the console and forwards events to Microsoft.Extensions.Logging providers, including OpenTelemetry.

| Extension | Description |
| --- | --- |
| `SerilogExtensions.GetBootstrapLogger()` | Creates a bootstrap console logger and wires `ApplicationLogging.LoggerFactory` |
| `SerilogWebApplicationBuilderExtensions.InitializeSerilog(builder, categoryName)` | Configures Serilog as the host pipeline and forwards events to registered logging providers |
| `LoggerConfiguration.AddCasCapDefaults(IConfiguration)` | Applies standard enrichers, console output, health-check filtering, and configuration binding |

`SerilogExtensions.ConsoleLevelSwitch` controls console verbosity independently from other sinks. It defaults to `Verbose`; interactive applications can temporarily raise it to `Warning` to prevent rendered output from interleaving with logs.

## OpenTelemetry

`InitializeOpenTelemetry` registers metrics, traces, and logs with OTLP gRPC export. It uses `IMetricsConfig` for the service name, metric prefix, and exporter endpoint.

| Extension | Description |
| --- | --- |
| `OpenTelemetryExtensions.InitializeOpenTelemetry(...)` | Registers OpenTelemetry metrics, traces, and logs with OTLP gRPC export |
| `MeterProviderBuilderExtensions.AddHistogramView(...)` | Registers explicit histogram bucket boundaries for a prefixed instrument |
| `MetricBoundaries.Symmetric(...)` | Mirrors positive boundaries into a signed set around zero |

Registration is skipped when `IMetricsConfig.OtlpExporterEndpoint` is `null`. Development hosts expose the Prometheus exporter; non-development hosts add ASP.NET Core, runtime, process, HTTP client, and optional Redis instrumentation.

### Configuration Examples

Minimal configuration disables telemetry:

```json
{
    "AppConfig": {
        "MetricNamePrefix": "app",
        "OtelServiceName": "Example.App"
    }
}
```

Configured OTLP export:

```json
{
    "AppConfig": {
        "MetricNamePrefix": "app",
        "OtelServiceName": "Example.App",
        "OtlpExporterEndpoint": "http://otel-collector.example.com:4317"
    }
}
```

## Dependencies

### Framework References

| Reference | Purpose |
| --- | --- |
| `Microsoft.AspNetCore.App` | ASP.NET Core MVC application-part / controller-feature types (`IApplicationFeatureProvider<ControllerFeature>`, `IMvcBuilder`) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | Host and metrics configuration contracts |
| `CasCap.Common.Logging` | Shared `ApplicationLogging` logger factory |
| `CasCap.Common.Net` | Basic-auth header creation for authenticated OTLP exporters |
| `CasCap.Common.Services` | Git build metadata for telemetry resource attributes |

### NuGet Packages

The package includes the Serilog ASP.NET Core pipeline, standard Serilog enrichers and console sink, OpenTelemetry hosting/export packages, ASP.NET Core, HTTP, process, runtime, and Redis instrumentation, and `StackExchange.Redis` integration.
