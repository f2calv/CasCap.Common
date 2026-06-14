namespace CasCap.Common.Extensions;

/// <summary>OpenTelemetry registration extensions for ASP.NET Core applications.</summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Registers OpenTelemetry metrics, traces, and logs with standard instrumentations
    /// and OTLP gRPC export.
    /// </summary>
    /// <param name="builder">Web application builder.</param>
    /// <param name="metricsConfig">Metrics configuration providing service name, metric prefix, and OTLP endpoint.</param>
    /// <param name="gitMetadata">Git metadata for resource attributes.</param>
    /// <param name="connectionMultiplexer">Optional Redis connection multiplexer for trace instrumentation.</param>
    /// <param name="apiAuthConfig">Optional basic-auth credentials applied as an <c>Authorization</c> header on every OTLP export — required only when the collector endpoint sits behind an authenticating ingress.</param>
    /// <param name="configureMetrics">Optional callback to add app-specific metrics configuration.</param>
    /// <param name="configureTracing">Optional callback to add app-specific tracing configuration.</param>
    public static void InitializeOpenTelemetry(
        this WebApplicationBuilder builder,
        IMetricsConfig metricsConfig,
        GitMetadata gitMetadata,
        IConnectionMultiplexer? connectionMultiplexer = null,
        ApiAuthConfig? apiAuthConfig = null,
        Action<MeterProviderBuilder>? configureMetrics = null,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        if (metricsConfig.OtlpExporterEndpoint is null || metricsConfig.OtlpExporterEndpoint == default)
        {
            Serilog.Log.Warning("OtlpExporterEndpoint is null/empty so skipping OpenTelemetry registration");
            return;
        }

        var otlpEndpoint = metricsConfig.OtlpExporterEndpoint;

        // Build an OTLP Authorization header (gRPC metadata / HTTP header) from the shared basic-auth
        // credentials. Only needed when exporting through an authenticating ingress; in-cluster exports
        // straight to the collector service ignore it harmlessly.
        string? otlpHeaders = null;
        if (apiAuthConfig is not null
            && !string.IsNullOrWhiteSpace(apiAuthConfig.Username)
            && !string.IsNullOrWhiteSpace(apiAuthConfig.Password))
        {
            otlpHeaders = $"Authorization={NetExtensions.GetBasicAuthHeaderValue(apiAuthConfig.Username, apiAuthConfig.Password)}";
        }

        void ConfigureOtlpExporter(OtlpExporterOptions opt)
        {
            opt.Protocol = OtlpExportProtocol.Grpc;
            opt.Endpoint = otlpEndpoint;
            if (otlpHeaders is not null)
                opt.Headers = otlpHeaders;
        }

        var attributes = new Dictionary<string, object>
        {
            { "service.version", gitMetadata.GIT_TAG },
            { "deployment.environment", builder.Environment.GetAcronym() }
        };
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(
            serviceName: metricsConfig.OtelServiceName,
            serviceInstanceId: Environment.MachineName
            ).AddAttributes(attributes);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder.SetResourceBuilder(resourceBuilder).AddMeter(metricsConfig.MetricNamePrefix);
                if (builder.Environment.IsDevelopment())
                    metricsBuilder.AddPrometheusExporter();
                else
                {
                    metricsBuilder.AddAspNetCoreInstrumentation();
                    metricsBuilder.AddRuntimeInstrumentation();
                    metricsBuilder.AddProcessInstrumentation();
                }
                configureMetrics?.Invoke(metricsBuilder);
                metricsBuilder.AddOtlpExporter(ConfigureOtlpExporter);
            })
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder.SetResourceBuilder(resourceBuilder);
                if (!builder.Environment.IsDevelopment())
                    tracingBuilder.AddAspNetCoreInstrumentation(o =>
                    {
                        o.Filter = context =>
                        {
                            if (context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
                                return false;
                            if (context.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
                                return false;
                            return true;
                        };
                    });
                tracingBuilder.AddHttpClientInstrumentation();
                if (!builder.Environment.IsDevelopment() && connectionMultiplexer is not null)
                    tracingBuilder.AddRedisInstrumentation(connectionMultiplexer, configure => { });
                configureTracing?.Invoke(tracingBuilder);
                tracingBuilder.AddOtlpExporter(ConfigureOtlpExporter);
            })
            .WithLogging(loggingBuilder =>
            {
                loggingBuilder.SetResourceBuilder(resourceBuilder);
                loggingBuilder.AddOtlpExporter(ConfigureOtlpExporter);
            });
    }
}
