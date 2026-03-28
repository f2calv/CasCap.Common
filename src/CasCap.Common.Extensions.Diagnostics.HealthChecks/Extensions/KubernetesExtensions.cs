namespace CasCap.Common.Extensions;

/// <summary>
/// TODO
/// </summary>
public static class KubernetesExtensions
{
    /// <summary>
    /// TODO
    /// </summary>
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
