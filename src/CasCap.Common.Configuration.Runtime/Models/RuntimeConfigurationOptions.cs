namespace CasCap.Common.Configuration.Runtime.Models;

/// <summary>Immutable startup settings for one registered typed configuration contract.</summary>
public sealed record RuntimeConfigurationOptions
{
    /// <summary>Gets the required deployment scope.</summary>
    public required RuntimeConfigurationScope Scope { get; init; }

    /// <summary>Gets the stable registered contract identifier, independent of CLR assembly identity.</summary>
    public required string ContractId { get; init; }

    /// <summary>Gets the supported serialized DTO contract version.</summary>
    public int ContractVersion { get; init; } = 1;

    /// <summary>Gets the reconciliation interval; notifications are not required.</summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets the maximum initial-load duration before startup fails.</summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Rejects invalid registration settings before constructing resources.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(ContractId);
        if (ContractId.Length > 100 || ContractVersion < 1
            || RefreshInterval < TimeSpan.FromMilliseconds(100)
            || RefreshInterval > TimeSpan.FromHours(1)
            || StartupTimeout <= TimeSpan.Zero || StartupTimeout > TimeSpan.FromMinutes(5))
            throw new ArgumentException("Runtime configuration registration settings are outside supported bounds.");
    }
}
