namespace CasCap.Common.Abstractions;

/// <summary>Defines a sink that can receive and retrieve events of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The event type handled by this sink.</typeparam>
public interface IEventSink<T>
{
    /// <summary>The sink type identifier used for targeted dispatch filtering.</summary>
    string SinkType { get; }

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

    /// <summary>Writes a single event to the sink.</summary>
    Task WriteEvent(T @event, CancellationToken cancellationToken = default);

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
