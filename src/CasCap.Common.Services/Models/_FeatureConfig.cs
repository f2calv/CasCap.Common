namespace CasCap.Common.Models;

/// <inheritdoc cref="IFeatureOptions{T}"/>
public record FeatureOptions<T> : IFeatureOptions<T>
    where T : Enum
{
    /// <inheritdoc/>
    [Required]
    public required T AppMode { get; init; }
}
