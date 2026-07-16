using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Serilog;

/// <summary>
/// Extension methods for initializing Serilog on a <see cref="WebApplicationBuilder"/>.
/// </summary>
public static class SerilogWebApplicationBuilderExtensions
{
#if NET9_0_OR_GREATER
    private static readonly Lock _lock = new();
#else
    private static readonly object _lock = new();
#endif
    private static bool _mainLoggingInitialized;

    /// <summary>
    /// Configures Serilog via <see cref="SerilogExtensions.AddCasCapDefaults"/> for console/file logging.
    /// </summary>
    /// <remarks>
    /// Log export to the OTEL collector is handled by the native OpenTelemetry log exporter
    /// registered separately via <c>InitializeOpenTelemetry</c>, replacing the
    /// previous <c>Serilog.Sinks.OpenTelemetry</c> sink so the entire monitoring stack uses
    /// consistent OTEL libraries for metrics, traces and logs.
    /// </remarks>
    /// <param name="builder">The web application builder.</param>
    /// <param name="categoryName">Logger category name (typically <c>nameof(Program)</c>).</param>
    /// <returns>An <see cref="Microsoft.Extensions.Logging.ILogger"/> for early startup logging.</returns>
    public static Microsoft.Extensions.Logging.ILogger InitializeSerilog(this WebApplicationBuilder builder, string categoryName = "Program")
    {
        lock (_lock)
        {
            if (_mainLoggingInitialized)
                return ApplicationLogging.CreateLogger(categoryName);

            // Remove the default MEL Console/Debug/EventSource providers so writeToProviders: true
            // forwards only to the native OpenTelemetry log exporter (registered later via
            // InitializeOpenTelemetry) — avoiding a redundant second console writer alongside
            // Serilog's own console sink. InitializeOpenTelemetry MUST run after InitializeSerilog.
            builder.Logging.ClearProviders();

            builder.Host.UseSerilog((hostContext, loggerConfiguration) =>
            {
                loggerConfiguration.AddCasCapDefaults(hostContext.Configuration);
            }, writeToProviders: true);

            _mainLoggingInitialized = true;
        }

        return ApplicationLogging.CreateLogger(categoryName);
    }
}
