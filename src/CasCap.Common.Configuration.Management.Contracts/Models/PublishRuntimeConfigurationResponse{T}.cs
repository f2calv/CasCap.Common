namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>Confirms durable commit only; activation by monitors and replicas remains pending.</summary>
/// <param name="Document">The committed document. <example>{}</example></param>
public sealed record PublishRuntimeConfigurationResponse<T>(RuntimeConfigurationDocument<T> Document) where T : class
{
    /// <summary>Gets whether the authority committed the publication.</summary>
    /// <example>true</example>
    public bool Committed => true;

    /// <summary>Gets whether runtime activation remains unacknowledged.</summary>
    /// <example>true</example>
    public bool ActivationPending => true;
}
