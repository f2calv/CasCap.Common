namespace CasCap.Common.Configuration.Sqlite.Entities;

/// <summary>Scoped pointer updated atomically with the immutable journal.</summary>
internal sealed class RuntimeHeadEntity
{
    public required string Application { get; set; }
    public required string Environment { get; set; }
    public required string ContractId { get; set; }
    public long Revision { get; set; }
}
