namespace CasCap.Common.Extensions;

/// <summary>Extension methods for <see cref="CasCap.Common.Abstractions.KubernetesProbeTypes" />.</summary>
public static class KubernetesExtensions
{
    /// <summary>
    /// Converts the flags set on a <see cref="CasCap.Common.Abstractions.KubernetesProbeTypes" /> value into an array of health-check tag strings.
    /// </summary>
    /// <param name="e"><inheritdoc cref="CasCap.Common.Abstractions.KubernetesProbeTypes" path="/summary"/></param>
    /// <returns>An array of tag strings corresponding to the set flags.</returns>
    public static string[] GetTags(this KubernetesProbeTypes e)
    {
        var output = new List<string>();
        if (e.HasFlag(KubernetesProbeTypes.Readiness))
            output.Add(KubernetesProbeTypes.Readiness.GetDescription());
        if (e.HasFlag(KubernetesProbeTypes.Liveness))
            output.Add(KubernetesProbeTypes.Liveness.GetDescription());
        if (e.HasFlag(KubernetesProbeTypes.Startup))
            output.Add(KubernetesProbeTypes.Startup.GetDescription());
        return output.ToArray();
    }
}
