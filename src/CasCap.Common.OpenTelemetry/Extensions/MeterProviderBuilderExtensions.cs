namespace CasCap.Common.Extensions;

/// <summary>Metric view registration extensions for <see cref="OpenTelemetry.Metrics.MeterProviderBuilder"/>.</summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>Registers an explicit-bucket view for a single prefixed histogram instrument.</summary>
    /// <remarks>
    /// Composes the fully qualified instrument name as <c>{metricNamePrefix}.{instrumentName}</c> — pass the same
    /// prefix (<see cref="CasCap.Common.Abstractions.IMetricsConfig.MetricNamePrefix"/>) and instrument-name constant
    /// used when the <see cref="System.Diagnostics.Metrics.Histogram{T}"/> was created, so a rename cannot leave a view
    /// silently unmatched (which would restore the default boundaries without any error). Only the bucket series are
    /// affected; <c>_sum</c> and <c>_count</c> are unchanged.
    /// </remarks>
    /// <param name="builder">The meter provider builder to configure.</param>
    /// <param name="metricNamePrefix">Metric name prefix shared by every instrument in the application.</param>
    /// <param name="instrumentName">Instrument name, excluding the prefix.</param>
    /// <param name="boundaries">Explicit bucket boundaries, in strictly ascending order.</param>
    /// <returns>The same <see cref="OpenTelemetry.Metrics.MeterProviderBuilder"/>, for chaining.</returns>
    public static MeterProviderBuilder AddHistogramView(
        this MeterProviderBuilder builder,
        string metricNamePrefix,
        string instrumentName,
        params double[] boundaries)
        => builder.AddView($"{metricNamePrefix}.{instrumentName}",
            new ExplicitBucketHistogramConfiguration { Boundaries = boundaries });
}
