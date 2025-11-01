namespace CasCap.Common.Models;

/// <inheritdoc cref="IFeatureOptions{T}"/>
public class FeatureOptions<T> : IFeatureOptions<T>
    where T : Enum
{
    [Required]
    public required T AppMode { get; init; }
}
