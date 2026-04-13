namespace CasCap.Common.Abstractions;

/// <summary>Exposes the OpenTelemetry metric name prefix used by event sink metrics services.</summary>
public interface IMetricsConfig
{
    /// <summary>
    /// Prefix applied to all OpenTelemetry metric names (e.g. <c>"haus"</c> produces <c>haus.knx.hvac.temp</c>).
    /// Also used as the OTel <see cref="System.Diagnostics.Metrics.Meter"/> name.
    /// </summary>
    string MetricNamePrefix { get; }
}
