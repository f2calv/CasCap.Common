using System.ComponentModel;

namespace CasCap.Common.Abstractions;

/// <summary>Authentication method used to connect to Azure Blob Storage.</summary>
public enum AzureAuthType
{
    /// <summary>Authenticate using a connection string.</summary>
    ConnectionString,

    /// <summary>Authenticate using an Azure <c>TokenCredential</c>.</summary>
    TokenCredential,
}

/// <summary>
/// Different types of Kubernetes container health probes.
/// </summary>
[Flags]
public enum KubernetesProbeTypes
{
    /// <summary>
    /// Disabled
    /// </summary>
    None = 0,
    /// <summary>
    /// Readiness indicates if the app is running normally but isn't ready to receive requests.
    /// The readiness check filters health checks to the health check with the ready tag.
    /// i.e. /healthz/ready
    /// </summary>
    [Description("ready")]
    Readiness = 1,
    /// <summary>
    /// Liveness indicates if an app has crashed and must be restarted.
    /// The liveness check filters out the StartupHostedServiceHealthCheck by returning false.
    /// i.e. /healthz/live
    /// </summary>
    [Description("live")]
    Liveness = 2,
    /// <summary>
    /// A Kubernetes startup probe is an optional configuration that blocks other probes
    /// (like readiness and liveness) from running until the application has successfully initialized,
    /// preventing premature restarts or traffic routing while the app starts up.
    /// i.e. /healthz/startup
    /// </summary>
    [Description("startup")]
    Startup = 4,
}
