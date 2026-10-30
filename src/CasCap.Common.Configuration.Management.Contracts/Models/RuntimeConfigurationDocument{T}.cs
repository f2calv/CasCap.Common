namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>The authoritative durable document, not a reader activation acknowledgement or audit record.</summary>
/// <param name="Revision">The durable head revision. <example>1</example></param>
/// <param name="Value">The host-owned browser-safe typed value. <example>{}</example></param>
/// <param name="RecordedAtUtc">The publication time. <example>2026-01-01T00:00:00Z</example></param>
/// <param name="ExpiresAtUtc">The optional expiry. <example>2026-01-02T00:00:00Z</example></param>
public sealed record RuntimeConfigurationDocument<T>(
    long Revision,
    T Value,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset? ExpiresAtUtc
) where T : class;
