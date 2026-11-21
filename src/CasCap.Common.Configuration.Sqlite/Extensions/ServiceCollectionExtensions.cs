using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Extensions;
using CasCap.Common.Configuration.Sqlite.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace CasCap.Common.Configuration.Sqlite.Extensions;

/// <summary>Registers a SQLite authority independently of optional read caches.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers short-lived contexts and standard typed runtime reload; never applies schema changes.</summary>
    [RequiresDynamicCode("Use generated JSON metadata and an explicit contract validator for Native AOT.")]
    [RequiresUnreferencedCode("Reflection-based runtime configuration requires the complete DTO graph.")]
    public static IServiceCollection AddSqliteRuntimeConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        string connectionString,
        T baseline
    ) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddRuntimeConfiguration(settings, baseline);
        return AddStore<T>(services, connectionString);
    }

    /// <summary>Registers SQLite with generated JSON metadata and a complete reflection-free DTO validator.</summary>
    /// <remarks>EF Native AOT additionally requires compiled models and precompiled queries; it remains experimental.</remarks>
    public static IServiceCollection AddSqliteRuntimeConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        string connectionString,
        T baseline,
        JsonTypeInfo<T> typeInfo,
        IValidateOptions<T> validator
    ) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddRuntimeConfiguration(settings, baseline, typeInfo, validator);
        return AddStore<T>(services, connectionString);
    }

    private static IServiceCollection AddStore<T>(IServiceCollection services, string connectionString) where T : class
    {
        services.AddDbContextFactory<RuntimeConfigurationDbContext>(options => options.UseSqlite(
            connectionString, provider => provider.MigrationsHistoryTable(RuntimeConfigurationStorage.HistoryTable)));
        services.AddSingleton<SqliteRuntimeConfigurationStore<T>>();
        services.AddSingleton<IRuntimeConfigurationPublisher<T>>(sp => sp.GetRequiredService<SqliteRuntimeConfigurationStore<T>>());
        services.AddSingleton<IRuntimeConfigurationStore<T>>(sp => sp.GetRequiredService<SqliteRuntimeConfigurationStore<T>>());
        return services;
    }
}
