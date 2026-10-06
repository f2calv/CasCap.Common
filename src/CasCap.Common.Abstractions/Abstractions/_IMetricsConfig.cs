namespace CasCap.Common.Abstractions;

/// <summary>Exposes the OpenTelemetry metric name prefix used by event sink metrics services.</summary>
public interface IMetricsConfig
{
    /// <summary>
    /// Prefix applied to all OpenTelemetry metric names (e.g. <c>"myapp"</c> produces <c>myapp.sensor.temperature</c>).
    /// Also used as the OTel <see cref="System.Diagnostics.Metrics.Meter"/> name.
    /// </summary>
    public string MetricNamePrefix { get; }

    /// <summary>OpenTelemetry service name reported to the OTEL collector.</summary>
    public string OtelServiceName { get; }

#if NET8_0_OR_GREATER
    /// <summary>OTLP gRPC exporter endpoint URI (e.g. <c>http://otelcol:4317</c>).</summary>
    /// <remarks>When <see langword="null"/> or default, OpenTelemetry registration is skipped.</remarks>
    public Uri? OtlpExporterEndpoint => null;
#endif
}
