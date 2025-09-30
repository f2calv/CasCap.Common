namespace Microsoft.Extensions.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXUnitLogging(this IServiceCollection services, ITestOutputHelper testOutputHelper)
    {
        services.AddLogging(logging =>
        {
            logging.AddProvider(new TestLogProvider(testOutputHelper));
            logging.SetMinimumLevel(LogLevel.Trace);
        });
        //assign to the static LoggerFactory instance before exiting!
        services.BuildServiceProvider().AddStaticLogging();
        return services;
    }
}
