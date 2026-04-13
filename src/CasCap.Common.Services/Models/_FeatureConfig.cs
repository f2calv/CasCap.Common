namespace CasCap.Common.Models;

/// <inheritdoc cref="IFeatureConfig{T}"/>
public record FeatureConfig<T> : IAppConfig, IFeatureConfig<T>
    where T : Enum
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(FeatureConfig<T>)}";

    /// <inheritdoc/>
    [Required]
    public required T EnabledFeatures { get; init; }
}
