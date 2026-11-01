using CasCap.Common.Configuration.PostgreSql.Extensions;
using CasCap.Common.Configuration.Sqlite.Extensions;
using CasCap.Common.Configuration.TestHost.Services;
using CasCap.Common.Configuration.Management.Extensions;
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
        var settings = new RuntimeConfigurationOptions
        {
            Scope = new RuntimeConfigurationScope("test-application",
                builder.Configuration["RuntimeConfiguration:Environment"] ?? throw new InvalidOperationException("Test environment is required.")),
            ContractId = "controls",
            RefreshInterval = TimeSpan.FromMilliseconds(100),
        };
        var connectionString = builder.Configuration.GetConnectionString("RuntimeConfiguration")
            ?? throw new InvalidOperationException("Test database connection is required.");
        switch (builder.Configuration["RuntimeConfiguration:Backend"] ?? "PostgreSql")
        {
            case "PostgreSql":
                builder.Services.AddPostgresRuntimeConfiguration(settings, connectionString, new RuntimeControls());
                break;
            case "Sqlite":
                builder.Services.AddSqliteRuntimeConfiguration(settings, connectionString, new RuntimeControls());
                break;
            default:
                throw new InvalidOperationException("Unsupported runtime configuration backend.");
        }
        var managementEnabled = builder.Configuration.GetValue<bool>("RuntimeConfiguration:ManagementEnabled");
        if (managementEnabled)
            builder.Services.AddRuntimeConfigurationManagement<RuntimeControls>();
        var app = builder.Build();
        if (managementEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
        }
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
