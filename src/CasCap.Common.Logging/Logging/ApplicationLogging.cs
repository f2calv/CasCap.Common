namespace Microsoft.Extensions.Logging;

/// <summary>
/// Provides a static <see cref="ILoggerFactory"/> for creating loggers outside of dependency injection.
/// </summary>
//https://stackoverflow.com/questions/48676152/asp-net-core-web-api-logging-from-a-static-class
public static class ApplicationLogging
{
    /// <inheritdoc cref="ILoggerFactory"/>
    /// <remarks>Defaults to <see cref="Abstractions.NullLoggerFactory"/> so loggers can be created safely before <c>AddStaticLogging</c> runs.</remarks>
    public static ILoggerFactory LoggerFactory { get; set; } = Abstractions.NullLoggerFactory.Instance;

    /// <inheritdoc cref="LoggerFactoryExtensions.CreateLogger{T}(ILoggerFactory)"/>
    public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    /// <inheritdoc cref="ILoggerFactory.CreateLogger(string)"/>
    public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
}
