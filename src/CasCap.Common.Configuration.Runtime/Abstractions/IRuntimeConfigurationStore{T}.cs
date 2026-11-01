namespace CasCap.Common.Configuration.Runtime.Abstractions;

/// <summary>Reads the durable typed authority, not a cache or an arbitrary key/value bag.</summary>
public interface IRuntimeConfigurationStore<T> where T : class
{
    /// <summary>Reads the committed current revision, or reports an explicitly absent scope.</summary>
    public Task<RuntimeConfigurationRevision<T>?> ReadAsync(CancellationToken cancellationToken);
}
