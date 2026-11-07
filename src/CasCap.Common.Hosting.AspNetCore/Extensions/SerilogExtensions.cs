namespace Serilog;

/// <summary>Extension methods for configuring Serilog with standard CasCap defaults.</summary>
public static class SerilogExtensions
{
    private static readonly Lock _lock = new();
    private static bool _bootstrapLoggerInitialized;
    private const string HealthCheckCategoryPrefix = "Microsoft.Extensions.Diagnostics.HealthChecks";

    /// <summary>
    /// Controls the verbosity of the console sink at runtime. Defaults to <see cref="LogEventLevel.Verbose"/> so the
    /// console mirrors the configured pipeline level. Interactive console apps (e.g. a full-screen Spectre UI) can
    /// raise this to <see cref="LogEventLevel.Warning"/> or higher once startup completes to stop log lines
    /// interleaving with rendered output, without affecting the file/Seq/Loki sinks.
    /// </summary>
    public static readonly Core.LoggingLevelSwitch ConsoleLevelSwitch = new(LogEventLevel.Verbose);

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
                    applyThemeToRedirectedOutput: true,
                    levelSwitch: ConsoleLevelSwitch)
                .CreateBootstrapLogger();

            ApplicationLogging.LoggerFactory = new SerilogLoggerFactory(Log.Logger);

            SelfLog.Enable(Console.WriteLine);
            _bootstrapLoggerInitialized = true;
        }
    }

    /// <summary>
    /// Applies the standard CasCap Serilog configuration: platform-aware console sink,
    /// common enrichers, the <see cref="IsHealthCheckNoise"/> filter, and <c>appsettings</c> binding via
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
                applyThemeToRedirectedOutput: true,
                levelSwitch: ConsoleLevelSwitch)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .Enrich.WithAssemblyName()
            .Enrich.WithSpan()
            .Enrich.With(new RequestPathRemovalEnricher())
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Filter.ByExcluding(IsHealthCheckNoise)
            .ReadFrom.Configuration(configuration);

    /// <summary>
    /// Whether an event is routine output from the framework health-check service, which is dropped below
    /// <see cref="LogEventLevel.Warning"/> so failed checks stay visible.
    /// </summary>
    /// <remarks>
    /// Matches on <c>SourceContext</c> only, so application events whose property values mention health checks
    /// are kept and no other property is rendered. Health probe request summaries are demoted separately by
    /// <c>UseCasCapRequestLogging</c>.
    /// </remarks>
    /// <param name="logEvent">The event to evaluate.</param>
    /// <returns><see langword="true"/> when the event should be excluded.</returns>
    public static bool IsHealthCheckNoise(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        return logEvent.Level < LogEventLevel.Warning
            && logEvent.Properties.TryGetValue(Core.Constants.SourceContextPropertyName, out var sourceContext)
            && sourceContext is ScalarValue { Value: string category }
            && category.StartsWith(HealthCheckCategoryPrefix, StringComparison.Ordinal);
    }
}
