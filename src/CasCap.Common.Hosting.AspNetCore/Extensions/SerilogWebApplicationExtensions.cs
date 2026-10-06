using Microsoft.AspNetCore.Http;

namespace Serilog;

/// <summary>Extension methods for configuring Serilog request logging on a <see cref="WebApplication" />.</summary>
public static class SerilogWebApplicationExtensions
{
    /// <summary>
    /// Initializes static application logging and emits request summaries while suppressing successful health probes.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The supplied <paramref name="app" />.</returns>
    public static WebApplication UseCasCapRequestLogging(this WebApplication app)
    {
        app.Services.AddStaticLogging();

        app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) => exception is null
                    && httpContext.Response.StatusCode == StatusCodes.Status200OK
                    && httpContext.Request.Path.StartsWithSegments("/healthz")
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information);

        return app;
    }
}
