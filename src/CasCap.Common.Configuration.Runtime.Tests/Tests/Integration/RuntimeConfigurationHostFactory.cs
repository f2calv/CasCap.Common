using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestProgram = global::Program;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

public sealed class RuntimeConfigurationHostFactory(
    string connectionString,
    string environment
) : WebApplicationFactory<TestProgram>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:RuntimeConfiguration"] = connectionString,
                ["RuntimeConfiguration:Environment"] = environment,
            }));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureLogging(logging => logging.ClearProviders());
}
