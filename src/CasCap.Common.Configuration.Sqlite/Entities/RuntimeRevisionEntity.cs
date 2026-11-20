using System.Diagnostics.CodeAnalysis;

namespace CasCap.Common.Configuration.Sqlite.Entities;

/// <summary>Immutable complete snapshot and simple audit record.</summary>
public sealed class RuntimeRevisionEntity
{
    /// <summary>Creates an EF entity populated by object initializers or materialization.</summary>
    public RuntimeRevisionEntity() { }

    /// <summary>Allows direct precompiled projection without EF 10's required-member unsafe-accessor bug.</summary>
    [SetsRequiredMembers]
    public RuntimeRevisionEntity(
        string application,
        string environment,
        string contractId,
        string payload,
        string actor,
        string reason
    )
    {
        Application = application;
        Environment = environment;
        ContractId = contractId;
        Payload = payload;
        Actor = actor;
        Reason = reason;
    }

    /// <summary>Gets or sets the application scope.</summary>
    public required string Application { get; set; }

    /// <summary>Gets or sets the environment scope.</summary>
    public required string Environment { get; set; }

    /// <summary>Gets or sets the contract identifier.</summary>
    public required string ContractId { get; set; }

    /// <summary>Gets or sets the scoped publication number.</summary>
    public long Revision { get; set; }

    /// <summary>Gets or sets the supported contract version.</summary>
    public int ContractVersion { get; set; }

    /// <summary>Gets or sets the complete JSON snapshot.</summary>
    public required string Payload { get; set; }

    /// <summary>Gets or sets the UTC publication time.</summary>
    public DateTimeOffset RecordedAtUtc { get; set; }

    /// <summary>Gets or sets the audit actor reference.</summary>
    public required string Actor { get; set; }

    /// <summary>Gets or sets the publication reason.</summary>
    public required string Reason { get; set; }

    /// <summary>Gets or sets optional baseline-reversion expiry.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>Gets or sets the historical source of a restoration.</summary>
    public long? RestoredFromRevision { get; set; }
}
