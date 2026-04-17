namespace CasCap.Common.Abstractions;

/// <summary>
/// Decorates an enum field with an OpenTelemetry metric name suffix and UCUM unit for gauge registration.
/// The final metric name is formed by prepending the configured meter prefix.
/// </summary>
/// <remarks>
/// Presence of this attribute on an enum field signals that the field should be tracked
/// as an observable gauge. Fields without the attribute are silently ignored by metrics sinks.
/// </remarks>
/// <param name="name">The metric name suffix (without the top-level prefix).</param>
/// <param name="unit">The UCUM unit string (e.g. <c>"Cel"</c>, <c>"W"</c>, <c>"%"</c>, <c>"1"</c>).</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class MetricAttribute(string name, string unit) : Attribute
{
    /// <summary>The metric name suffix (without the top-level prefix).</summary>
    public string Name { get; } = name;

    /// <summary>The UCUM unit string.</summary>
    public string Unit { get; } = unit;

    /// <summary>
    /// When <see langword="true"/> the gauge value is derived from a <see cref="bool"/>
    /// value (<see langword="true"/> → <c>1.0</c>, <see langword="false"/> → <c>0.0</c>).
    /// </summary>
    public bool IsBoolean { get; init; }

    /// <summary>
    /// Human-readable description passed to the <see cref="System.Diagnostics.Metrics.Meter"/>
    /// gauge registration, displayed in metric explorers such as Grafana.
    /// </summary>
    public string? Description { get; init; }
}
