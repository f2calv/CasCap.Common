using Azure.Core.Diagnostics;
using System.Diagnostics.Tracing;

namespace CasCap.Common.Services;

/// <summary>
/// Counts HTTP 429 responses and retries reported by the Azure SDK, which retries throttled requests
/// silently and so hides throttling inside request latency.
/// </summary>
/// <remarks>
/// Listens to the process-wide <c>Azure-Core</c> event source, so run evaluations sequentially and take
/// a <see cref="Snapshot"/> around each run. Providers that do not use the Azure SDK report zero.
/// </remarks>
public sealed class AzureThrottlingMonitor : IDisposable
{
    private const int TooManyRequests = 429;

    private readonly AzureEventSourceListener _listener;
    private long _throttledResponses;
    private long _retries;

    /// <summary>Starts listening.</summary>
    public AzureThrottlingMonitor() =>
        _listener = new AzureEventSourceListener(OnEvent, EventLevel.Informational);

    /// <summary>The counts observed so far.</summary>
    public (long ThrottledResponses, long Retries) Snapshot() =>
        (Interlocked.Read(ref _throttledResponses), Interlocked.Read(ref _retries));

    /// <inheritdoc/>
    public void Dispose() => _listener.Dispose();

    private void OnEvent(EventWrittenEventArgs e, string _)
    {
        if (e.EventName == "RequestRetrying")
            Interlocked.Increment(ref _retries);
        else if (e.EventName == "ErrorResponse" && GetStatus(e) == TooManyRequests)
            Interlocked.Increment(ref _throttledResponses);
    }

    private static int? GetStatus(EventWrittenEventArgs e)
    {
        var index = e.PayloadNames?.IndexOf("status") ?? -1;
        return index >= 0 && e.Payload?[index] is int status ? status : null;
    }
}
