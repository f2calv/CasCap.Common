namespace CasCap.Common.Configuration.Sqlite.Entities;

/// <summary>Scoped pointer updated atomically with the immutable journal.</summary>
// EF 10's generated tracked materializer casts to IInjectableService, which cannot compile for a sealed entity.
public class RuntimeHeadEntity
{
    /// <summary>Gets or sets the application scope.</summary>
    public required string Application { get; set; }

    /// <summary>Gets or sets the environment scope.</summary>
    public required string Environment { get; set; }

    /// <summary>Gets or sets the contract identifier.</summary>
    public required string ContractId { get; set; }

    /// <summary>Gets or sets the optimistic-concurrency head.</summary>
    public long Revision { get; set; }
}
