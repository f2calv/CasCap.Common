namespace CasCap.Common.Models;

/// <inheritdoc cref="IFeatureConfig{T}"/>
[Obsolete("Use the non-generic IBgFeature interface with string-based FeatureName instead.")]
public record FeatureConfig<T> : IAppConfig, IFeatureConfig<T>
    where T : Enum
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(FeatureConfig<T>)}";

    /// <inheritdoc/>
    [Required]
    public required T EnabledFeatures { get; init; }
}
