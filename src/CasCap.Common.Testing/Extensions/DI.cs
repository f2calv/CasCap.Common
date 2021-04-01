using CasCap.Common.Testing;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
namespace Microsoft.Extensions.DependencyInjection
{
    public static class DI
    {
        public static IServiceCollection AddXUnitLogging(this IServiceCollection services, ITestOutputHelper output)
        {
            services.AddLogging(logging =>
            {
                logging.AddProvider(new TestLogProvider(output));
                logging.SetMinimumLevel(LogLevel.Trace);
                ApplicationLogging.LoggerFactory = logging.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            });
            return services;
        }
    }
}