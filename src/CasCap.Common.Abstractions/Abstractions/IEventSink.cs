namespace CasCap.Common.Abstractions;

/// <summary>
/// Defines a sink that can receive and retrieve events of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The event type handled by this sink.</typeparam>
public interface IEventSink<T> where T : class
{
    /// <summary>
    /// Performs any one-time initialization required by the sink (e.g. starting background flush loops).
    /// The default implementation is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Token that signals when the application is shutting down.</param>
#if NETSTANDARD2_0
    Task InitializeAsync(CancellationToken cancellationToken);
#else
    Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
#endif

    /// <summary>
    /// Writes a single event to the sink.
    /// </summary>
    Task WriteEvent(T @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves events from the sink, optionally filtered by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">Optional identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return. Defaults to <c>1000</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<T> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs housekeeping by removing entries whose identifiers are not in <paramref name="validIds"/>.
    /// The default implementation is a no-op.
    /// </summary>
    /// <param name="validIds">The set of identifiers that should be retained.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
#if NETSTANDARD2_0
    Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default);
#else
    Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
#endif
}
