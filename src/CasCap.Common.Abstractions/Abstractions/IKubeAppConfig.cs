using System.Net;

namespace CasCap.Common.Abstractions;

/// <summary>
/// Exposes Kubernetes-specific runtime properties of the running pod or node.
/// </summary>
/// <remarks>
/// Implement on your application configuration record (e.g. <c>AppConfig</c>) so that
/// services needing only Kubernetes identity information can depend on this lightweight
/// abstraction instead of the full configuration type.
/// </remarks>
public interface IKubeAppConfig
{
    /// <summary>Kubernetes node name the pod is scheduled on.</summary>
    string? NodeName { get; }

    /// <summary>Kubernetes pod name.</summary>
    string? PodName { get; }

    /// <summary>Kubernetes namespace the pod belongs to.</summary>
    string? Namespace { get; }

    /// <summary>Cluster-internal IP address of the pod.</summary>
    IPAddress? PodIp { get; }

    /// <summary>Kubernetes service account name assigned to the pod.</summary>
    string? ServiceAccountName { get; }
}
