namespace CasCap.Common.Diagnostics.HealthChecks;

/// <summary>
/// Base class for health checks that verify HTTP endpoint accessibility.
/// Implements <see cref="IHealthCheck"/> with a standard probe-and-report pattern so that
/// derived classes only need to supply their DI parameters and a display name.
/// </summary>
public abstract class HttpEndpointCheckBase(
    ILogger logger,
    HttpClient client,
    IHealthCheckConfig healthCheckConfig,
    string serviceDisplayName,
    bool initialConnectionActive = false) : IHealthCheck
{
#if NETSTANDARD2_0
    private static readonly IReadOnlyList<int> DefaultExpectedStatusCodes = new[] { 200 };
#else
    private static readonly IReadOnlyList<int> DefaultExpectedStatusCodes = [200];
#endif

    private volatile bool _connectionActive = initialConnectionActive;

    /// <summary>
    /// Indicates whether the most recent health check probe succeeded.
    /// </summary>
    public bool ConnectionActive
    {
        get => _connectionActive;
        set => _connectionActive = value;
    }

    /// <inheritdoc/>
    public virtual async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{ClassName} healthcheck executing...", GetType().Name);
        var result = await IsAccessible(healthCheckConfig, cancellationToken);
        if (result)
        {
            ConnectionActive = true;
            return HealthCheckResult.Healthy($"{serviceDisplayName} is accessible!");
        }
        else
        {
            ConnectionActive = false;
            return HealthCheckResult.Unhealthy($"{serviceDisplayName} is inaccessible!");
        }
    }

    /// <summary>
    /// Checks whether the HTTP endpoint specified in <paramref name="config"/> is accessible
    /// and returns one of the expected status codes.
    /// </summary>
    /// <param name="config">The health check configuration containing the URI and expected status codes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected Task<bool> IsAccessible(IHealthCheckConfig config, CancellationToken cancellationToken = default)
        => IsAccessible(config.HealthCheckUri, config.HealthCheckExpectedHttpStatusCodes, cancellationToken);

    /// <summary>
    /// Checks whether the specified HTTP endpoint is accessible and returns one of the expected status codes.
    /// </summary>
    /// <param name="requestUri">The URI to probe.</param>
    /// <param name="healthCheckExpectedHttpStatusCodes">
    /// HTTP status codes considered healthy. When <see langword="null"/>, defaults to <c>[200]</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task<bool> IsAccessible(string requestUri, IReadOnlyList<int>? healthCheckExpectedHttpStatusCodes = null, CancellationToken cancellationToken = default)
    {
        requestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        healthCheckExpectedHttpStatusCodes ??= DefaultExpectedStatusCodes;
        logger.LogDebug("{ClassName} health check executing", nameof(HttpEndpointCheckBase));
        HttpResponseMessage? result = null;
        try
        {
            result = await client.GetAsync(requestUri, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Handle timeout.
            logger.LogWarning(ex, "{ClassName} failure", nameof(HttpEndpointCheckBase));
        }
        // Filter by InnerException.
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Handle timeout.
            logger.LogDebug(ex, "{ClassName} timed out", nameof(HttpEndpointCheckBase));
        }
        catch (TaskCanceledException ex)
        {
            // Handle cancellation.
            logger.LogWarning(ex, "{ClassName} canceled", nameof(HttpEndpointCheckBase));
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "{ClassName} http endpoint failure", nameof(HttpEndpointCheckBase));
        }
        if (result is not null && healthCheckExpectedHttpStatusCodes.Contains((int)result.StatusCode))
        {
            logger.LogDebug("{ClassName} {RequestUri} is accessible", nameof(HttpEndpointCheckBase), requestUri);
            return true;
        }
        else
        {
            logger.LogDebug("{ClassName} {RequestUri} is inaccessible", nameof(HttpEndpointCheckBase), requestUri);
            return false;
        }
    }
}
