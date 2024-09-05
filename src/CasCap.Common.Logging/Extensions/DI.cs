namespace Microsoft.Extensions.DependencyInjection;

public static class DI
{
    /// <summary>
    /// Assign the registered ILoggerFactory service to the static LoggerFactory instance.
    /// </summary>
    /// <param name="serviceProvider"></param>
    public static void AddStaticLogging(this IServiceProvider serviceProvider)
    {
        ApplicationLogging.LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }
}
