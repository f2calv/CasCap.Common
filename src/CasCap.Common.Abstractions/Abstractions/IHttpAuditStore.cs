#if NET8_0_OR_GREATER
namespace CasCap.Common.Abstractions;

/// <summary>Abstraction for persisting HTTP audit entries.</summary>
public interface IHttpAuditStore
{
    /// <summary>Persists a single audit entry.</summary>
    /// <param name="entry">The HTTP audit entry to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(HttpAuditEntry entry, CancellationToken cancellationToken = default);
}
#endif
