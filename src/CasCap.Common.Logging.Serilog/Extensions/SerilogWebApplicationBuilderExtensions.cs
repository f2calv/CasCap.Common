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
    /// Registers Serilog as a console-only logging provider on a <see cref="WebApplicationBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Microsoft.Extensions.Logging owns the logging pipeline; Serilog is added via <c>AddSerilog</c>
    /// (not <c>UseSerilog</c>) purely to render the coloured console sink from
    /// <see cref="SerilogExtensions.AddCasCapDefaults"/>. Application logs reach the OTEL collector
    /// through the native OpenTelemetry log exporter registered separately by
    /// <c>InitializeOpenTelemetry</c> as a sibling MEL provider — so the exported message body is
    /// rendered by the MEL formatter (no Serilog string-quoting), and neither a
    /// <c>Serilog.Sinks.OpenTelemetry</c> sink nor <c>writeToProviders</c> forwarding is involved.
    /// The MEL minimum level is opened to <see cref="LogLevel.Trace"/> so each provider self-filters:
    /// the Serilog console via its own <c>Serilog:MinimumLevel</c> config, and the OpenTelemetry
    /// export via the standard <c>Logging</c> section. <c>InitializeOpenTelemetry</c> MUST run after
    /// this (the default-provider clear happens here).
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

            // Build the full Serilog logger (coloured console sink + enrichers + appsettings level
            // config) and publish it as the static Log.Logger for Log.* / Log.CloseAndFlushAsync,
            // keeping the static factory (used by early-startup / static ILogger fields) in sync.
            Log.Logger = new LoggerConfiguration()
                .AddCasCapDefaults(builder.Configuration)
                .CreateLogger();
            ApplicationLogging.LoggerFactory = new SerilogLoggerFactory(Log.Logger);

            // MEL owns the pipeline. Clear the default Console/Debug/EventSource providers so Serilog
            // is the only console writer, open the minimum level so each provider self-filters, then
            // add Serilog as a console-only provider. The native OpenTelemetry log exporter is added
            // later by InitializeOpenTelemetry as a sibling provider (hence the ordering requirement).
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Logging.AddSerilog(Log.Logger);

            _mainLoggingInitialized = true;
        }

        return ApplicationLogging.CreateLogger(categoryName);
    }
}
