namespace CasCap.Common.Configuration.TestHost.Services;

/// <summary>Represents warm process state that a pod restart would discard.</summary>
public sealed class CacheSentinel
{
    /// <summary>Gets the immutable identity of this singleton instance.</summary>
    public Guid Id { get; } = Guid.NewGuid();
}
