namespace Microsoft.Extensions.Logging;

//https://stackoverflow.com/questions/48676152/asp-net-core-web-api-logging-from-a-static-class
public static class ApplicationLogging
{
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    /// <inheritdoc cref="ILoggerFactory"/>
    public static ILoggerFactory LoggerFactory { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

    /// <inheritdoc cref="LoggerFactoryExtensions.CreateLogger{T}(ILoggerFactory)"/>
    public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    /// <inheritdoc cref="ILoggerFactory.CreateLogger(string)"/>
    public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
}
