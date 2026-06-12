namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extension methods for configuring static logging on an <see cref="IServiceProvider"/>.</summary>
public static class ServiceProviderExtensions
{
    /// <summary>Assign the registered ILoggerFactory service to the static LoggerFactory instance.</summary>
    public static IServiceProvider AddStaticLogging(this IServiceProvider serviceProvider)
    {
        ApplicationLogging.LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return serviceProvider;
    }
}
