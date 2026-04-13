namespace CasCap.Common.Abstractions;

/// <summary>
/// Exposes Azure Blob Storage connection properties so that feature-specific
/// configuration records can be consumed through a common abstraction.
/// </summary>
public interface IAzBlobStorageConfig
{
    /// <summary>
    /// Azure Blob Storage connection string or endpoint URI.
    /// </summary>
    /// <remarks>
    /// When a <c>TokenCredential</c> is supplied this value is treated as a plain endpoint URI
    /// (e.g. <c>https://account.blob.core.windows.net</c>).
    /// Otherwise it is used as a full connection string that already contains authentication details.
    /// </remarks>
    string AzureBlobStorageConnectionString { get; }

    /// <summary>Azure Blob Storage container name.</summary>
    string AzureBlobStorageContainerName { get; }

    /// <summary>Kubernetes health check probe type for the Azure Blob Storage dependency.</summary>
    KubernetesProbeTypes HealthCheckAzureBlobStorage { get; }
}
