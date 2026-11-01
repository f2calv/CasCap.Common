namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>A complete optimistic publication request. Actor identity is never accepted from the browser.</summary>
public sealed record PublishRuntimeConfigurationRequest<T> where T : class
{
    /// <summary>Gets the complete browser-safe candidate.</summary>
    /// <example>{}</example>
    public required T Value { get; init; }

    /// <summary>Gets the last authoritative revision, or zero when no durable document exists.</summary>
    /// <example>1</example>
    public required long ExpectedRevision { get; init; }

    /// <summary>Gets a nonempty publication reason, at most 500 characters.</summary>
    /// <example>Adjust diagnostic verbosity</example>
    public required string Reason { get; init; }

    /// <summary>Gets an optional future expiry in ISO 8601 format.</summary>
    /// <example>2026-01-02T00:00:00Z</example>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
