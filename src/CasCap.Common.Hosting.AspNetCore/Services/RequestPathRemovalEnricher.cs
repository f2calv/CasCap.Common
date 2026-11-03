namespace CasCap.Common.Services;

/// <summary>
/// Removes the raw <c>RequestPath</c> that the ASP.NET Core hosting log scope attaches to every event logged during a request.
/// </summary>
/// <remarks>
/// Raw paths can carry record, user or tenant identifiers. The request summary logs the matched route template instead,
/// and <c>RequestId</c> and the trace identifiers still correlate an event with its request.
/// </remarks>
internal sealed class RequestPathRemovalEnricher : ILogEventEnricher
{
    private const string RequestPathProperty = "RequestPath";

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        => logEvent.RemovePropertyIfPresent(RequestPathProperty);
}
