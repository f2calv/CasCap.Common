namespace Serilog;

/// <summary>Extension methods for configuring Serilog with standard CasCap defaults.</summary>
public static class SerilogExtensions
{
    private static readonly object _lock = new();
    private static bool _bootstrapLoggerInitialized;

    /// <summary>
    /// Creates a bootstrap <see cref="Log.Logger"/> with a platform-aware console sink
    /// and wires it into <see cref="ApplicationLogging.LoggerFactory"/>.
    /// Call this early in <c>Program.cs</c> before the DI container is built.
    /// </summary>
    public static void GetBootstrapLogger()
    {
        lock (_lock)
        {
            if (_bootstrapLoggerInitialized)
                return;

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    theme: RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? SystemConsoleTheme.Literate
                        : AnsiConsoleTheme.Code,
                    applyThemeToRedirectedOutput: true)
                .CreateBootstrapLogger();

            ApplicationLogging.LoggerFactory = new SerilogLoggerFactory(Log.Logger);

            SelfLog.Enable(Console.WriteLine);
            _bootstrapLoggerInitialized = true;
        }
    }

    /// <summary>
    /// Applies the standard CasCap Serilog configuration: platform-aware console sink,
    /// common enrichers, health-check noise filter, and <c>appsettings</c> binding via
    /// <see cref="Configuration.LoggerSettingsConfiguration"/>.
    /// </summary>
    /// <param name="loggerConfiguration">The <see cref="LoggerConfiguration"/> to configure.</param>
    /// <param name="configuration">
    /// The <see cref="IConfiguration"/> root (typically from <c>hostContext.Configuration</c>).
    /// Used for <c>ReadFrom.Configuration()</c> binding.
    /// </param>
    /// <returns>The same <see cref="LoggerConfiguration"/> for fluent chaining.</returns>
    public static LoggerConfiguration AddCasCapDefaults(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration) =>
        loggerConfiguration
            .WriteTo.Console(
                theme: RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? SystemConsoleTheme.Literate
                    : AnsiConsoleTheme.Code,
                applyThemeToRedirectedOutput: true)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .Enrich.WithAssemblyName()
            .Enrich.WithSpan()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Filter.ByExcluding(c =>
                c.Properties.Any(p => p.Value.ToString().Contains("HealthCheck", StringComparison.OrdinalIgnoreCase))
                && new[] { LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information }.Contains(c.Level))
            .ReadFrom.Configuration(configuration);
}
