namespace CasCap.Common.Diagnostics.HealthChecks;

public abstract class HttpEndpointCheckBase(ILogger logger, HttpClient client)
{
    protected /*readonly */ILogger _logger = logger;

    protected async Task<bool> IsAccessible(string requestUri, int HealthCheckExpectedHttpStatusCode = 0, CancellationToken cancellationToken = default)
    {
        requestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        _logger.LogDebug("{ClassName} health check executing", nameof(HttpEndpointCheckBase));
        HttpResponseMessage? result = null;
        try
        {
            result = await client.GetAsync(requestUri, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Handle timeout.
            _logger.LogWarning(ex, "{ClassName} failure", nameof(HttpEndpointCheckBase));
        }
        // Filter by InnerException.
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Handle timeout.
            _logger.LogDebug(ex, "{ClassName} timed out", nameof(HttpEndpointCheckBase));
        }
        catch (TaskCanceledException ex)
        {
            // Handle cancellation.
            _logger.LogWarning(ex, "{ClassName} canceled", nameof(HttpEndpointCheckBase));
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "{ClassName} http endpoint failure", nameof(HttpEndpointCheckBase));
        }
        if (result is not null && (result.IsSuccessStatusCode || (int)result.StatusCode == HealthCheckExpectedHttpStatusCode))
        {
            _logger.LogDebug("{ClassName} {RequestUri} is accessible", nameof(HttpEndpointCheckBase), requestUri);
            return true;
        }
        else
        {
            _logger.LogDebug("{ClassName} {RequestUri} is inaccessible", nameof(HttpEndpointCheckBase), requestUri);
            return false;
        }
    }
}
