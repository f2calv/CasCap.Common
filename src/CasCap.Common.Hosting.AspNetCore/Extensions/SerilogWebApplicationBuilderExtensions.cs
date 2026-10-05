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
    /// Initializes Serilog as the host logging pipeline via <c>UseSerilog</c>, forwarding to the
    /// registered Microsoft.Extensions.Logging providers (including the OpenTelemetry log exporter).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Serilog deliberately owns the logging pipeline (<c>UseSerilog(..., writeToProviders: true)</c>)
    /// so a single <c>Serilog</c> config section drives both the coloured console sink and the OTLP
    /// export, and <c>UseSerilogRequestLogging</c> summaries reach Loki. Application logs are forwarded
    /// to the native OpenTelemetry log exporter registered by <c>InitializeOpenTelemetry</c> as an MEL
    /// provider — <c>writeToProviders: true</c> is what feeds it (without it the exporter is starved).
    /// </para>
    /// <para>
    /// This "Serilog-owns" model is a deliberate choice over an MEL-owned / OTel-native pipeline. Do NOT switch to
    /// <c>AddSerilog</c> (Serilog as a console-only provider): it splits level config into a separate MEL
    /// <c>Logging</c> section and drops <c>UseSerilogRequestLogging</c> output from Loki.
    /// </para>
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

            // Serilog owns the pipeline and forwards to the MEL providers (writeToProviders: true) so
            // the native OpenTelemetry log exporter (registered later by InitializeOpenTelemetry)
            // receives every event — one Serilog config section drives console + OTLP. ClearProviders
            // drops the default MEL Console/Debug providers so Serilog is the only console writer.
            // InitializeOpenTelemetry MUST run after this because ClearProviders removes providers registered earlier.
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
