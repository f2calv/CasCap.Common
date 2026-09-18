#if NET8_0_OR_GREATER
using CasCap.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;

namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for adding a standardised resilience handler to <see cref="IHttpClientBuilder"/> registrations.
/// </summary>
/// <remarks>
/// Wraps <see cref="Microsoft.Extensions.Http.Resilience"/> so every service in the solution
/// gets the same retry/circuit-breaker/timeout defaults with structured logging.
/// </remarks>
public static class HttpClientBuilderResilienceExtensions
{
    /// <summary>
    /// Adds the standard resilience handler with retry logging to the HTTP client pipeline.
    /// </summary>
    /// <remarks>
    /// Applies <see cref="HttpResiliencePipelineBuilderExtensions.AddStandardResilienceHandler"/>
    /// with an <c>OnRetry</c> callback that emits a structured log message including the caller name,
    /// attempt number, delay and outcome.
    /// </remarks>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <param name="callerName">
    /// A display name for the calling service (typically <c>nameof(MyService)</c>)
    /// used as the <c>{ClassName}</c> structured-log parameter.
    /// </param>
    /// <param name="retrySafety">
    /// Which requests the pipeline may retry. Defaults to
    /// <see cref="HttpRetrySafety.SafeMethodsOnly"/>, which never replays a POST or PATCH the server
    /// may already have processed.
    /// </param>
    /// <returns>The resilience pipeline builder for further configuration.</returns>
    public static IHttpStandardResiliencePipelineBuilder AddStandardResilience(this IHttpClientBuilder builder,
        string callerName, HttpRetrySafety retrySafety = HttpRetrySafety.SafeMethodsOnly) =>
        builder.AddStandardResilienceHandler(options =>
        {
            var shouldHandle = options.Retry.ShouldHandle;
            options.Retry.ShouldHandle = args => retrySafety switch
            {
                HttpRetrySafety.Never => PredicateResult.False(),
                HttpRetrySafety.AllMethods => shouldHandle(args),
                _ => IsReplaySafe(args) ? shouldHandle(args) : PredicateResult.False(),
            };

            options.Retry.OnRetry = args =>
            {
                var logger = args.Context.Properties.GetValue(
                    new ResiliencePropertyKey<ILogger>("logger"), null!);

                if (logger is not null)
                {
                    logger.LogWarning(
                        "{ClassName} resilience retry attempt {AttemptNumber} after {Delay}ms, outcome: {Outcome}",
                        callerName,
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                }

                return ValueTask.CompletedTask;
            };
        });

    /// <summary>
    /// Whether replaying this request cannot duplicate a side effect the server already applied.
    /// </summary>
    /// <remarks>
    /// An idempotent method is always safe. Anything else is safe only when the failure happened
    /// before the request reached the server, so a timeout and any status code are both treated as
    /// unsafe — the server may have completed the work and only the response was lost.
    /// </remarks>
    public static bool IsReplaySafe(RetryPredicateArguments<HttpResponseMessage> args)
    {
        var method = args.Context.GetRequestMessage()?.Method;
        if (method is null || IsIdempotent(method))
            return true;

        return args.Outcome.Exception is HttpRequestException
        {
            HttpRequestError: HttpRequestError.ConnectionError
                or HttpRequestError.NameResolutionError
                or HttpRequestError.SecureConnectionError
                or HttpRequestError.ProxyTunnelError
        };
    }

    private static bool IsIdempotent(HttpMethod method) =>
        method == HttpMethod.Get
        || method == HttpMethod.Head
        || method == HttpMethod.Put
        || method == HttpMethod.Delete
        || method == HttpMethod.Options
        || method == HttpMethod.Trace;
}
#endif
