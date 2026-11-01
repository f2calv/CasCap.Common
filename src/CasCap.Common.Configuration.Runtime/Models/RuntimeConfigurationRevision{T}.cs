namespace CasCap.Common.Configuration.Runtime.Models;

/// <summary>A complete typed publication and its immutable audit metadata.</summary>
public sealed record RuntimeConfigurationRevision<T> where T : class
{
    /// <summary>Gets the monotonically increasing scoped revision.</summary>
    public required long Revision { get; init; }

    /// <summary>Gets the complete validated value.</summary>
    public required T Value { get; init; }

    /// <summary>Gets the authoritative UTC publication time.</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>Gets the operator's non-secret audit reference.</summary>
    public required string Actor { get; init; }

    /// <summary>Gets the publication reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets the expiry after which the reader returns to its startup baseline.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <summary>Gets the historical source of a restore, when applicable.</summary>
    public long? RestoredFromRevision { get; init; }
}
