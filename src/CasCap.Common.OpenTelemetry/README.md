# CasCap.Common.OpenTelemetry

Reusable OpenTelemetry configuration with standard metrics, traces, and log exporters via OTLP gRPC, built on top of `CasCap.Common.Abstractions` and `CasCap.Common.Logging.Serilog`.

## Purpose

Provides a single `InitializeOpenTelemetry` extension method on `WebApplicationBuilder` that registers the full OpenTelemetry pipeline (metrics, traces, and logs) with standard ASP.NET Core instrumentations. Configuration is driven by `IMetricsConfig` — the OTLP endpoint, service name, and metric prefix are all sourced from the application's configuration record.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Extensions

| Extension | Description |
| --- | --- |
| `OpenTelemetryExtensions.InitializeOpenTelemetry(builder, metricsConfig, connectionMultiplexer, gitMetadata, configureMetrics?, configureTracing?)` | Registers OpenTelemetry metrics, traces, and logs with OTLP gRPC export |

## Behaviour

- **Skips registration** when `IMetricsConfig.OtlpExporterEndpoint` is `null` — safe for development without an OTEL collector.
- **Development mode**: Registers a Prometheus exporter for local `/metrics` scraping.
- **Production mode**: Adds ASP.NET Core, runtime, and process instrumentations for metrics; ASP.NET Core, HTTP client, and Redis instrumentations for traces.
- **App-specific hooks**: Optional `Action<MeterProviderBuilder>` and `Action<TracerProviderBuilder>` callbacks for custom histogram views, additional trace sources, etc.

## Configuration

The extension reads configuration from any `IMetricsConfig` implementation:

| Property | Type | Description |
| --- | --- | --- |
| `MetricNamePrefix` | `string` | Meter name and metric prefix (e.g. `"haus"`, `"cas"`) |
| `OtelServiceName` | `string` | OpenTelemetry service name resource attribute |
| `OtlpExporterEndpoint` | `Uri?` | OTLP gRPC endpoint (e.g. `http://localhost:4317/`). `null` disables telemetry. |

### Configuration Examples

Minimal (disables telemetry):

```json
{
  "AppConfig": {
    "MetricNamePrefix": "haus",
    "OtelServiceName": "CasCap.App"
  }
}
```

Fully configured:

```json
{
  "AppConfig": {
    "MetricNamePrefix": "haus",
    "OtelServiceName": "CasCap.App",
    "OtlpExporterEndpoint": "http://opentelemetry-collector.monitoring.svc:4317"
  }
}
```

## Dependencies

### NuGet Packages

| Package | Purpose |
| --- | --- |
| [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol) | OTLP gRPC exporter for metrics, traces, and logs |
| [OpenTelemetry.Exporter.Prometheus.AspNetCore](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.AspNetCore) | Prometheus `/metrics` endpoint (development) |
| [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting) | `AddOpenTelemetry()` host integration |
| [OpenTelemetry.Instrumentation.AspNetCore](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore) | HTTP request metrics and traces |
| [OpenTelemetry.Instrumentation.Http](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http) | Outbound HTTP client traces |
| [OpenTelemetry.Instrumentation.Process](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process) | CPU and memory utilization metrics |
| [OpenTelemetry.Instrumentation.Runtime](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime) | GC, thread pool, and assembly metrics |
| [OpenTelemetry.Instrumentation.StackExchangeRedis](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis) | Redis command traces |
| [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis) | Redis connection multiplexer type |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | `IMetricsConfig` interface |
| `CasCap.Common.Logging.Serilog` | Serilog fallback warning when endpoint is null |
| `CasCap.Common.Services` | `GitMetadata` record for resource attributes |
