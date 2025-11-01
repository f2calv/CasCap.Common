namespace CasCap.Common.Abstractions;

/// <summary>
/// This interface is used to highlight that a service is launched by setting a Feature Flag.
/// </summary>
/// <remarks>
/// If a service inherits from BackgroundService then it's a little harder to test so we create
/// simpler objects that implement the <see cref="IFeature{T}"/> which can be launched via a bitwise enumeration.
/// </remarks>
public interface IFeature<T> where T : Enum
{
    /// <summary>
    /// Enum used to identify the feature type of the implementation.
    /// </summary>
    T FeatureType { get; }

    /// <summary>
    /// Launches the service.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
