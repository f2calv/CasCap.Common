namespace CasCap.Common.Abstractions;

/// <summary>
/// Exposes Azure Table Storage connection properties so that feature-specific
/// configuration records can be consumed through a common abstraction.
/// </summary>
public interface IAzTableStorageConfig
{
    /// <summary>
    /// Azure Table Storage connection string or endpoint URI.
    /// </summary>
    /// <remarks>
    /// When a <c>TokenCredential</c> is supplied this value is treated as a plain endpoint URI
    /// (e.g. <c>https://account.table.core.windows.net</c>).
    /// Otherwise it is used as a full connection string that already contains authentication details.
    /// </remarks>
    string AzureTableStorageConnectionString { get; }

    /// <summary>Kubernetes health check probe type for the Azure Table Storage dependency.</summary>
    KubernetesProbeTypes HealthCheckAzureTableStorage { get; }
}
