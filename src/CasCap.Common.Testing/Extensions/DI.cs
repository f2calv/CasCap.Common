namespace Microsoft.Extensions.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class DI
{
    public static IServiceCollection AddXUnitLogging(this IServiceCollection services, ITestOutputHelper testOutputHelper)
    {
        services.AddLogging(logging =>
        {
            logging.AddProvider(new TestLogProvider(testOutputHelper));
            logging.SetMinimumLevel(LogLevel.Trace);
        });
        //assign to the static LoggerFactory instance before exiting!
        //ApplicationLogging.LoggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        services.BuildServiceProvider().AddStaticLogging();
        return services;
    }
}
