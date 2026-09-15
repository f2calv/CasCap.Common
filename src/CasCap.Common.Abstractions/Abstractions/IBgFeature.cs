namespace CasCap.Common.Abstractions;

/// <summary>Identifies a feature-gated background service launched at runtime by the feature host.</summary>
/// <remarks>
/// Implementations declare a <see cref="FeatureName"/> string that is matched against the set of
/// enabled feature names at startup. Use the well-known <c>AlwaysEnabled</c> constant for
/// services that should run regardless of which features are active.
/// </remarks>
public interface IBgFeature
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Sentinel value for <see cref="FeatureName"/> indicating the service runs in every feature combination.
    /// </summary>
    const string AlwaysEnabled = "All";
#endif

    /// <summary>The feature name that gates this service (matched case-insensitively against enabled features).</summary>
    string FeatureName { get; }

    /// <summary>Launches the service.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
