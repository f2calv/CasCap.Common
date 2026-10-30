namespace CasCap.Common.Configuration.Sqlite.Entities;

/// <summary>Immutable complete snapshot and simple audit record.</summary>
internal sealed class RuntimeRevisionEntity
{
    public required string Application { get; set; }
    public required string Environment { get; set; }
    public required string ContractId { get; set; }
    public long Revision { get; set; }
    public int ContractVersion { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public required string Actor { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public long? RestoredFromRevision { get; set; }
}
