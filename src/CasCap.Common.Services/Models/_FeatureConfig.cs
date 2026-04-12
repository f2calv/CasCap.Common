namespace CasCap.Common.Models;

/// <inheritdoc cref="IFeatureConfig{T}"/>
public record FeatureConfig<T> : IFeatureConfig<T>
    where T : Enum
{
    /// <inheritdoc/>
    [Required]
    public required T AppMode { get; init; }
}
