namespace CasCap.Common.Abstractions;

/// <summary>Well-known background feature names shared across target frameworks.</summary>
public static class BgFeatureNames
{
    /// <summary>Indicates that a background feature runs in every feature combination.</summary>
    public const string AlwaysEnabled = "All";
}

/// <summary>Identifies a feature-gated background service launched at runtime by the feature host.</summary>
/// <remarks>
/// Implementations declare a <see cref="FeatureName"/> string that is matched against the set of
/// enabled feature names at startup. Use <see cref="BgFeatureNames.AlwaysEnabled"/> for
/// services that should run regardless of which features are active.
/// </remarks>
public interface IBgFeature
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Sentinel value for <see cref="FeatureName"/> indicating the service runs in every feature combination.
    /// </summary>
    public const string AlwaysEnabled = BgFeatureNames.AlwaysEnabled;
#endif

    /// <summary>The feature name that gates this service (matched case-insensitively against enabled features).</summary>
    public string FeatureName { get; }

    /// <summary>Launches the service.</summary>
    public Task ExecuteAsync(CancellationToken cancellationToken);
}
