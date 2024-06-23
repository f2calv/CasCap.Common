namespace Microsoft.Extensions.DependencyInjection;

public static class DI
{
    public static IServiceCollection AddXUnitLogging(this IServiceCollection services, ITestOutputHelper output)
    {
        services.AddLogging(logging =>
        {
            logging.AddProvider(new TestLogProvider(output));
            logging.SetMinimumLevel(LogLevel.Trace);
        });
        //assign to the static LoggerFactory instance before exiting!
        ApplicationLogging.LoggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        return services;
    }
}
