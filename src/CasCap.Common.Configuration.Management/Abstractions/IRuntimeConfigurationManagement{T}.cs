using System.Security.Claims;

namespace CasCap.Common.Configuration.Management.Abstractions;

/// <summary>The typed management boundary for an already authorized fixed destination.</summary>
public interface IRuntimeConfigurationManagement<T> where T : class
{
    /// <summary>Reads the authoritative durable document, without exposing audit metadata.</summary>
    public Task<RuntimeConfigurationManagementResult<RuntimeConfigurationDocument<T>>> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Validates and copies a candidate without mutating durable or runtime state.</summary>
    public RuntimeConfigurationManagementResult<PreviewRuntimeConfigurationResponse<T>> Preview(PreviewRuntimeConfigurationRequest<T> request);

    /// <summary>Publishes once with optimistic concurrency and an authenticated audit identity.</summary>
    public Task<RuntimeConfigurationManagementResult<PublishRuntimeConfigurationResponse<T>>> PublishAsync(
        PublishRuntimeConfigurationRequest<T> request, ClaimsPrincipal principal, CancellationToken cancellationToken);
}
