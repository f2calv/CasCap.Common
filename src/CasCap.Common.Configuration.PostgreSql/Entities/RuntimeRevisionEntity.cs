namespace CasCap.Common.Configuration.PostgreSql.Entities;

/// <summary>Immutable snapshot-carrying publication event; never contains provider credentials.</summary>
internal sealed class RuntimeRevisionEntity
{
    /// <summary>Gets or sets the scoped application identifier.</summary>
    public required string Application { get; set; }
    /// <summary>Gets or sets the deployment-defined environment identifier.</summary>
    public required string Environment { get; set; }
    /// <summary>Gets or sets the registered contract identifier.</summary>
    public required string ContractId { get; set; }
    /// <summary>Gets or sets the monotonic revision.</summary>
    public long Revision { get; set; }
    /// <summary>Gets or sets the supported payload contract version.</summary>
    public int ContractVersion { get; set; }
    /// <summary>Gets or sets the complete serialized typed value.</summary>
    public required string Payload { get; set; }
    /// <summary>Gets or sets UTC publication time.</summary>
    public DateTimeOffset RecordedAtUtc { get; set; }
    /// <summary>Gets or sets the non-secret audit actor reference.</summary>
    public required string Actor { get; set; }
    /// <summary>Gets or sets the publication reason.</summary>
    public required string Reason { get; set; }
    /// <summary>Gets or sets the optional expiry.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    /// <summary>Gets or sets the historical restore source.</summary>
    public long? RestoredFromRevision { get; set; }
}
