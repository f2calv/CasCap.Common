namespace CasCap.Common.Abstractions;

/// <summary>
/// Exposes Azure Blob Storage connection properties so that feature-specific
/// configuration records can be consumed through a common abstraction.
/// </summary>
public interface IAzBlobStorage
{
    /// <summary>Azure Blob Storage endpoint URI.</summary>
    Uri AzureBlobStorageUri { get; }

    /// <summary>Azure Blob Storage container name.</summary>
    string AzureBlobStorageContainerName { get; }

    /// <summary>Authentication method used to connect to Azure Blob Storage.</summary>
    AzureAuthType AzureBlobStorageAuthType { get; }

    /// <summary>Kubernetes health check probe type for the Azure Blob Storage dependency.</summary>
    KubernetesProbeTypes HealthCheckAzureBlobStorage { get; }
}
