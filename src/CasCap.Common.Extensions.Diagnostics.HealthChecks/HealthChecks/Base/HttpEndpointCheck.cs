﻿namespace CasCap.Services;

public abstract class HttpEndpointCheck(ILogger logger, HttpClient client)
{
    protected /*readonly */ILogger _logger = logger;

    protected async Task<bool> IsAccessible(string requestUri, int HealthCheckExpectedHttpStatusCode = 0, CancellationToken cancellationToken = default)
    {
        requestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        _logger.LogDebug("{serviceName} health check executing", nameof(HttpEndpointCheck));
        HttpResponseMessage? result = null;
        try
        {
            result = await client.GetAsync(requestUri, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Handle timeout.
            _logger.LogWarning(ex, "{serviceName} failure", nameof(HttpEndpointCheck));
        }
        // Filter by InnerException.
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Handle timeout.
            _logger.LogDebug(ex, "{serviceName} timed out", nameof(HttpEndpointCheck));
        }
        catch (TaskCanceledException ex)
        {
            // Handle cancellation.
            _logger.LogWarning(ex, "{serviceName} canceled", nameof(HttpEndpointCheck));
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "{serviceName} http endpoint failure", nameof(HttpEndpointCheck));
        }
        if (result is not null && (result.IsSuccessStatusCode || (int)result.StatusCode == HealthCheckExpectedHttpStatusCode))
        {
            _logger.LogDebug("{serviceName} {requestUri} is accessible", nameof(HttpEndpointCheck), requestUri);
            return true;
        }
        else
        {
            _logger.LogDebug("{serviceName}, {requestUri} is inaccessible", nameof(HttpEndpointCheck), requestUri);
            return false;
        }
    }
}
