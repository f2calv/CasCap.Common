namespace CasCap.Common.Abstractions;

/// <summary>
/// Implement the <see cref="IFeatureOptions{T}"/> interface in conjunction with <see cref="IFeature{T}"/>
/// to pass the enabled features to the <see cref="BackgroundService"/> launcher.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IFeatureOptions<T> where T : Enum
{
    /// <summary>
    /// The types of feature which are enabled.
    /// </summary>
    public T AppMode { get; init; }
}
