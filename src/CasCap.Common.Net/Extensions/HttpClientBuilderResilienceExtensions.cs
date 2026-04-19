#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

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
    /// <returns>The resilience pipeline builder for further configuration.</returns>
    public static IHttpStandardResiliencePipelineBuilder AddStandardResilience(this IHttpClientBuilder builder, string callerName) =>
        builder.AddStandardResilienceHandler(options =>
        {
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
}
#endif
