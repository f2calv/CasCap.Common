namespace CasCap.Common.Extensions;

/// <summary>Factory helpers for explicit histogram bucket boundary sets.</summary>
public static class MetricBoundaries
{
    /// <summary>Mirrors a positive-only boundary set into a signed set straddling zero.</summary>
    /// <remarks>
    /// The OpenTelemetry default histogram boundaries are non-negative, so an instrument that records signed
    /// measurements collapses every negative observation into the single <c>le="0"</c> bucket — the negative half of the
    /// distribution becomes a flat block and any quantile or distribution query over it is meaningless. Supplying the
    /// positive half once and mirroring it keeps the two halves symmetric by construction and avoids hand-maintaining a
    /// duplicated literal array. Pass the result to
    /// <see cref="MeterProviderBuilderExtensions.AddHistogramView(OpenTelemetry.Metrics.MeterProviderBuilder, string, string, double[])"/>.
    /// <para>
    /// For example <c>Symmetric(1, 5, 10)</c> returns <c>[-10, -5, -1, 0, 1, 5, 10]</c>.
    /// </para>
    /// </remarks>
    /// <param name="positiveBoundaries">The positive half of the boundary set, strictly positive and in ascending order.</param>
    /// <returns>A new array of the form <c>[-max … -min, 0, min … max]</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="positiveBoundaries"/> is empty or not strictly ascending.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="positiveBoundaries"/> contains a value that is not strictly positive.</exception>
    public static double[] Symmetric(params double[] positiveBoundaries)
    {
        ArgumentNullException.ThrowIfNull(positiveBoundaries);
        if (positiveBoundaries.Length == 0)
            throw new ArgumentException("At least one boundary is required.", nameof(positiveBoundaries));

        for (var i = 0; i < positiveBoundaries.Length; i++)
        {
            if (positiveBoundaries[i] <= 0)
                throw new ArgumentOutOfRangeException(nameof(positiveBoundaries), positiveBoundaries[i],
                    "Boundaries must be strictly positive; the mirrored half and zero are supplied automatically.");
            if (i > 0 && positiveBoundaries[i] <= positiveBoundaries[i - 1])
                throw new ArgumentException("Boundaries must be in strictly ascending order.", nameof(positiveBoundaries));
        }

        var count = positiveBoundaries.Length;
        var boundaries = new double[(count * 2) + 1];
        for (var i = 0; i < count; i++)
        {
            boundaries[i] = -positiveBoundaries[count - 1 - i];
            boundaries[count + 1 + i] = positiveBoundaries[i];
        }
        boundaries[count] = 0;
        return boundaries;
    }
}
