#if NET8_0_OR_GREATER
namespace CasCap.Common.Auditing;

/// <summary>No-op <see cref="IHttpAuditStore"/> used as a safe default when no persistent store is configured.</summary>
public sealed class NullHttpAuditStore : IHttpAuditStore
{
    /// <inheritdoc/>
    public ValueTask SaveAsync(HttpAuditEntry entry, CancellationToken cancellationToken = default)
        => default;
}
#endif
