using CasCap.Common.Configuration.PostgreSql.Extensions;
using CasCap.Common.Configuration.TestHost.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace CasCap.Common.Configuration.TestHost;

/// <summary>Builds and runs the permanent runtime-configuration integration host.</summary>
public static class AppHost
{
    /// <summary>Wires a required database-backed contract and an observation endpoint, then runs the host.</summary>
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<CacheSentinel>();
        builder.Services.AddPostgresRuntimeConfiguration(
            new RuntimeConfigurationOptions
            {
                Scope = new RuntimeConfigurationScope("test-application",
                    builder.Configuration["RuntimeConfiguration:Environment"] ?? throw new InvalidOperationException("Test environment is required.")),
                ContractId = "controls",
                RefreshInterval = TimeSpan.FromMilliseconds(100),
            },
            builder.Configuration.GetConnectionString("RuntimeConfiguration")
                ?? throw new InvalidOperationException("Test database connection is required."),
            new RuntimeControls());
        var app = builder.Build();
        app.MapGet("/observe", (IOptionsMonitor<RuntimeControls> monitor, CacheSentinel sentinel) =>
            new HostObservation
            {
                Sentinel = sentinel.Id,
                Controls = monitor.CurrentValue,
            });
        app.MapGet("/feature", Results<Ok, NotFound>
            (IOptionsMonitor<RuntimeControls> monitor) => monitor.CurrentValue.FeatureEnabled
                ? TypedResults.Ok() : TypedResults.NotFound());
        await app.RunAsync();
    }
}
