namespace Microsoft.Extensions.DependencyInjection;

public static class DI
{
    public static void AddStaticLogging(this ServiceProvider serviceProvider)
    {
        //assign to the static LoggerFactory instance
        ApplicationLogging.LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }
}
