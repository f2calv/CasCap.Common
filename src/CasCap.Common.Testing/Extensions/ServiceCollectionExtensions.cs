namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extension methods for registering xUnit test logging services.</summary>
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>Adds logging providers that route output to xUnit's <see cref="ITestOutputHelper"/>.</summary>
    /// <param name="services">The service collection to add logging to.</param>
    /// <param name="testOutputHelper">The xUnit test output helper instance.</param>
    /// <returns>The service collection.</returns>
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
