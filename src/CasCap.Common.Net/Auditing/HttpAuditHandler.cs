#if NET8_0_OR_GREATER
using System.Text.Json;

namespace CasCap.Common.Auditing;

/// <summary>
/// <see cref="DelegatingHandler"/> that captures HTTP request/response pairs and persists them via <see cref="IHttpAuditStore"/>.
/// </summary>
public sealed class HttpAuditHandler(
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
            RequestBody = NormalizeBody(requestBody),
            ResponseBody = NormalizeBody(responseBody),
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

    private static string? NormalizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = int.MaxValue });
            return body;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(body);
        }
    }
}
#endif
