namespace CasCap.Common.Models;

/// <summary>Configuration for <see cref="FeatureFlagBgService"/> containing the set of enabled feature names.</summary>
public class FeatureFlagConfig
{
    /// <summary>Case-insensitive set of enabled feature names.</summary>
    [Required, MinLength(1)]
    public HashSet<string> EnabledFeatures { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
