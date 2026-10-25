namespace CasCap.Common.Configuration.PostgreSql.Entities;

/// <summary>Scoped current pointer, updated in the same transaction as the immutable journal.</summary>
internal sealed class RuntimeHeadEntity
{
    /// <summary>Gets or sets the application identifier.</summary>
    public required string Application { get; set; }
    /// <summary>Gets or sets the environment identifier.</summary>
    public required string Environment { get; set; }
    /// <summary>Gets or sets the registered contract identifier.</summary>
    public required string ContractId { get; set; }
    /// <summary>Gets or sets the current revision and optimistic concurrency token.</summary>
    public long Revision { get; set; }
}
