#if NET8_0_OR_GREATER
using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Abstractions;

/// <summary>Configuration for an individual event sink.</summary>
public record SinkConfigParams
{
    /// <summary>Whether this sink is enabled.</summary>
    [Required]
    public required bool Enabled { get; init; }

    /// <summary>
    /// Sink-specific settings. Each sink reads the keys it needs from this dictionary.
    /// For example an Azure Tables sink might use "LineItemTableName" and "SnapshotTableName",
    /// while a Redis sink might use "SnapshotValues" and "SnapshotStrings".
    /// </summary>
    public Dictionary<string, string> Settings { get; init; } = [];

    /// <summary>
    /// Gets a setting value by key, or <see langword="null"/> if the key is not present.
    /// </summary>
    /// <param name="key">The setting key to look up.</param>
    /// <returns>The setting value, or <see langword="null"/>.</returns>
    public string? GetSetting(string key) =>
        Settings.TryGetValue(key, out var value) ? value : null;
}
#endif
