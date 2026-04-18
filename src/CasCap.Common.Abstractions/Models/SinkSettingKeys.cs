namespace CasCap.Common.Abstractions;

/// <summary>
/// Well-known setting keys used in per-sink configuration dictionaries.
/// Use these constants instead of string literals to avoid magic strings.
/// </summary>
/// <remarks>
/// The property names defined here must be kept in sync with the corresponding keys
/// in all <c>appsettings*.json</c> files and any overriding environment variables.
/// Domain-specific keys live in their respective consumer projects.
/// </remarks>
public static class SinkSettingKeys
{
    /// <summary>Azure Tables line item table name.</summary>
    public const string LineItemTableName = nameof(LineItemTableName);

    /// <summary>Azure Tables snapshot table name.</summary>
    public const string SnapshotTableName = nameof(SnapshotTableName);

    /// <summary>Azure Tables snapshot entity partition key.</summary>
    public const string SnapshotPartitionKey = nameof(SnapshotPartitionKey);

    /// <summary>Azure Tables snapshot entity row key.</summary>
    public const string SnapshotRowKey = nameof(SnapshotRowKey);

    /// <summary>Redis hash key for decoded values.</summary>
    public const string SnapshotValues = nameof(SnapshotValues);

    /// <summary>Redis sorted set key prefix for time-series readings.</summary>
    public const string SeriesValues = nameof(SeriesValues);

    /// <summary>Azure Blob Storage container name.</summary>
    public const string ContainerName = nameof(ContainerName);

    /// <summary>
    /// Whether the sink should buffer writes and submit them in batches.
    /// Expected values are <c>"true"</c> or <c>"false"</c>. Defaults to <see langword="true"/> when absent.
    /// </summary>
    public const string BatchingEnabled = nameof(BatchingEnabled);
}
