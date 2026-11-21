using Microsoft.Extensions.DependencyInjection;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for configuring static logging on an <see cref="IServiceProvider"/>.</summary>
public static class LoggingServiceProviderExtensions
{
    /// <summary>Assign the registered ILoggerFactory service to the static LoggerFactory instance.</summary>
    public static IServiceProvider AddStaticLogging(this IServiceProvider serviceProvider)
    {
        ApplicationLogging.LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return serviceProvider;
    }
}
