namespace CasCap.Common.Configuration.Runtime.Abstractions;

/// <summary>Reads and publishes against the durable authority, never the optional cache.</summary>
public interface IRuntimeConfigurationPublisher<T> : IRuntimeConfigurationStore<T> where T : class
{
    /// <summary>Atomically appends a validated publication if the expected scoped head still matches.</summary>
    public Task<RuntimeConfigurationRevision<T>> PublishAsync(
        T value,
        long expectedRevision,
        string actor,
        string reason,
        DateTimeOffset? expiresAtUtc = null,
        CancellationToken cancellationToken = default);
}
