namespace CasCap.Common.Abstractions;

/// <summary>
/// Implement the <see cref="IFeatureConfig{T}"/> interface in conjunction with <see cref="IFeature{T}"/>
/// to pass the enabled features to the <see cref="BackgroundService"/> launcher.
/// </summary>
/// <typeparam name="T"></typeparam>
[Obsolete("Use the non-generic IBgFeature interface with string-based FeatureName instead.")]
public interface IFeatureConfig<T> where T : Enum
{
    /// <summary>The bitwise combination of features that are enabled.</summary>
    public T EnabledFeatures { get; init; }
}
