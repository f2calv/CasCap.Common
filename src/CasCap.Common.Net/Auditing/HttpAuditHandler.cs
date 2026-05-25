#if NET8_0_OR_GREATER
namespace CasCap.Common.Auditing;

/// <summary>
/// <see cref="DelegatingHandler"/> that captures HTTP request/response pairs and persists them via <see cref="IHttpAuditStore"/>.
/// </summary>
public class HttpAuditHandler(
    IHttpAuditStore auditStore,
    ILogger<HttpAuditHandler> logger,
    TimeProvider timeProvider
) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? requestBody = null;
        if (request.Content is not null)
            requestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var start = timeProvider.GetTimestamp();
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var elapsed = timeProvider.GetElapsedTime(start);

        string? responseBody = null;
        if (response.Content is not null)
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var entry = new HttpAuditEntry
        {
            TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
            Source = request.Options.TryGetValue(HttpAuditSource.Key, out var source)
                ? source ?? "Unknown"
                : "Unknown",
            HttpMethod = request.Method.Method,
            RequestUri = request.RequestUri?.PathAndQuery ?? string.Empty,
            StatusCode = (int)response.StatusCode,
            ElapsedMs = elapsed.TotalMilliseconds,
            RequestBody = requestBody,
            ResponseBody = responseBody,
        };

        try
        {
            await auditStore.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to persist audit entry for {RequestUri}",
                nameof(HttpAuditHandler), entry.RequestUri);
        }

        return response;
    }
}
#endif
