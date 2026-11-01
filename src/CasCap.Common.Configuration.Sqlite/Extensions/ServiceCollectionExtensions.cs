using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Extensions;
using CasCap.Common.Configuration.Sqlite.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CasCap.Common.Configuration.Sqlite.Extensions;

/// <summary>Registers a SQLite authority independently of optional read caches.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers short-lived contexts and standard typed runtime reload; never applies schema changes.</summary>
    public static IServiceCollection AddSqliteRuntimeConfiguration<T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        string connectionString,
        T baseline
    ) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddRuntimeConfiguration(settings, baseline);
        services.AddDbContextFactory<RuntimeConfigurationDbContext>(options => options.UseSqlite(
            connectionString, provider => provider.MigrationsHistoryTable(RuntimeConfigurationStorage.HistoryTable)));
        services.AddSingleton<SqliteRuntimeConfigurationStore<T>>();
        services.AddSingleton<IRuntimeConfigurationPublisher<T>>(sp => sp.GetRequiredService<SqliteRuntimeConfigurationStore<T>>());
        services.AddSingleton<IRuntimeConfigurationStore<T>>(sp => sp.GetRequiredService<SqliteRuntimeConfigurationStore<T>>());
        return services;
    }
}
