using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Serilog;

/// <summary>Extension methods for configuring Serilog request logging on a <see cref="WebApplication" />.</summary>
public static class SerilogWebApplicationExtensions
{
    private const string RequestSummaryTemplate = "HTTP {RequestMethod} {RequestRoute} responded {StatusCode} in {Elapsed:0.0} ms";
    private const string UnmatchedRoute = "(unmatched)";
    private const string HealthPathPrefix = "/healthz";
    private static readonly TimeSpan DefaultSlowRequestThreshold = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes static application logging and emits one privacy-safe summary event per request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The summary records the HTTP method, the matched route template (never the raw path or query string),
    /// the status code and the elapsed time. Requests that match no endpoint are logged as <c>(unmatched)</c>.
    /// </para>
    /// <para>
    /// Levels: unhandled exceptions and 5xx responses are <see cref="LogEventLevel.Error"/>; failed health probes and
    /// non-streaming requests slower than the threshold are <see cref="LogEventLevel.Warning"/>; other 1xx-4xx responses
    /// are <see cref="LogEventLevel.Information"/>; client cancellations and unmatched 404s are
    /// <see cref="LogEventLevel.Debug"/>; successful health probes and static files are <see cref="LogEventLevel.Verbose"/>.
    /// </para>
    /// <para>
    /// Register it first, before <c>UseExceptionHandler</c>, so it times the whole pipeline, sees authentication and
    /// authorization rejections, and leaves logging the exception itself to the exception handler.
    /// </para>
    /// </remarks>
    /// <param name="app">The web application.</param>
    /// <param name="slowRequestThreshold">Elapsed time from which a completed non-streaming request is a warning; defaults to five seconds.</param>
    /// <returns>The supplied <paramref name="app" />.</returns>
    public static WebApplication UseCasCapRequestLogging(this WebApplication app, TimeSpan? slowRequestThreshold = null)
    {
        var slowRequestMs = (slowRequestThreshold ?? DefaultSlowRequestThreshold).TotalMilliseconds;

        app.Services.AddStaticLogging();

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = RequestSummaryTemplate;
            options.GetLevel = (httpContext, elapsedMs, exception) => GetLevel(httpContext, elapsedMs, exception, slowRequestMs);
            options.GetMessageTemplateProperties = (httpContext, _, elapsedMs, statusCode) =>
            [
                new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
                new LogEventProperty("RequestRoute", new ScalarValue(GetRoute(httpContext))),
                new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
            ];
        });

        return app;
    }

    private static LogEventLevel GetLevel(HttpContext httpContext, double elapsedMs, Exception? exception, double slowRequestMs)
        => (exception, httpContext.Response.StatusCode) switch
        {
            _ when IsClientCancellation(httpContext, exception) => LogEventLevel.Debug,
            ({ }, _) => LogEventLevel.Error,
            (_, var statusCode) when httpContext.Request.Path.StartsWithSegments(HealthPathPrefix, StringComparison.OrdinalIgnoreCase)
                => statusCode < StatusCodes.Status400BadRequest ? LogEventLevel.Verbose : LogEventLevel.Warning,
            (_, >= StatusCodes.Status500InternalServerError) => LogEventLevel.Error,
            (_, StatusCodes.Status404NotFound) when GetEndpoint(httpContext) is null => LogEventLevel.Debug,
            (_, < StatusCodes.Status400BadRequest) when IsStaticFile(httpContext) => LogEventLevel.Verbose,
            _ when elapsedMs >= slowRequestMs && !IsLongLived(httpContext) => LogEventLevel.Warning,
            _ => LogEventLevel.Information,
        };

    private static bool IsClientCancellation(HttpContext httpContext, Exception? exception)
        => (httpContext.RequestAborted.IsCancellationRequested && exception is null or OperationCanceledException)
            || httpContext.Response.StatusCode == StatusCodes.Status499ClientClosedRequest;

    // The exception handler clears the active endpoint before re-executing, so prefer the endpoint it recorded.
    private static Endpoint? GetEndpoint(HttpContext httpContext)
        => httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint();

    private static string GetRoute(HttpContext httpContext)
        => GetEndpoint(httpContext) switch
        {
            RouteEndpoint routeEndpoint => "/" + routeEndpoint.RoutePattern.RawText?.TrimStart('/'),
            { DisplayName: { Length: > 0 } displayName } => displayName,
            _ => UnmatchedRoute,
        };

    private static bool IsStaticFile(HttpContext httpContext)
        => (HttpMethods.IsGet(httpContext.Request.Method) || HttpMethods.IsHead(httpContext.Request.Method))
            && GetEndpoint(httpContext)?.Metadata.GetMetadata<ControllerActionDescriptor>() is null
            && Path.HasExtension(httpContext.Request.Path.Value);

    private static bool IsLongLived(HttpContext httpContext)
        => httpContext.Response.StatusCode == StatusCodes.Status101SwitchingProtocols
            || httpContext.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true
            || httpContext.Response.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true;
}
