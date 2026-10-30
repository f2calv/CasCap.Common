using CasCap.Common.Configuration.PostgreSql.Services;
using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace CasCap.Common.Configuration.PostgreSql.Extensions;

/// <summary>Registers PostgreSQL as the only authority for a typed runtime configuration scope.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers a short-lived context factory, typed store and standard monitor reload.</summary>
    public static IServiceCollection AddPostgresRuntimeConfiguration<T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        string connectionString,
        T baseline
    ) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddRuntimeConfiguration(settings, baseline);
        services.AddDbContextFactory<RuntimeConfigurationDbContext>(options => options.UseNpgsql(
            connectionString,
            provider => provider.MigrationsHistoryTable("__ef_migrations_history", RuntimeConfigurationStorage.Schema)));
        services.AddSingleton<PostgresRuntimeConfigurationStore<T>>();
        services.AddSingleton<IRuntimeConfigurationPublisher<T>>(sp => sp.GetRequiredService<PostgresRuntimeConfigurationStore<T>>());
        services.AddSingleton<IRuntimeConfigurationStore<T>>(sp => sp.GetRequiredService<PostgresRuntimeConfigurationStore<T>>());
        return services;
    }
}
