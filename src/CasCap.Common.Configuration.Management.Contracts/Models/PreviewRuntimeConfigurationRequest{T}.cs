namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>A complete typed candidate to validate without publication.</summary>
public sealed record PreviewRuntimeConfigurationRequest<T> where T : class
{
    /// <summary>Gets the complete browser-safe candidate.</summary>
    /// <example>{}</example>
    public required T Value { get; init; }
}
