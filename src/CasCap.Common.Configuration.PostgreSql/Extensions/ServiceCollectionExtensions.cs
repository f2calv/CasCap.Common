using CasCap.Common.Configuration.PostgreSql.Services;
using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace CasCap.Common.Configuration.PostgreSql.Extensions;

/// <summary>Registers PostgreSQL as the only authority for a typed runtime configuration scope.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers a short-lived context factory, typed store and standard monitor reload.</summary>
    [RequiresDynamicCode("Use generated JSON metadata and an explicit contract validator for Native AOT.")]
    [RequiresUnreferencedCode("Reflection-based runtime configuration requires the complete DTO graph.")]
    public static IServiceCollection AddPostgresRuntimeConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
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

    /// <summary>Registers PostgreSQL with generated JSON metadata and a complete reflection-free DTO validator.</summary>
    /// <remarks>EF Native AOT additionally requires compiled models and precompiled queries; it remains experimental.</remarks>
    public static IServiceCollection AddPostgresRuntimeConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
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
        services.AddDbContextFactory<RuntimeConfigurationDbContext>(options => options.UseNpgsql(
            connectionString,
            provider => provider.MigrationsHistoryTable("__ef_migrations_history", RuntimeConfigurationStorage.Schema)));
        services.AddSingleton<PostgresRuntimeConfigurationStore<T>>();
        services.AddSingleton<IRuntimeConfigurationPublisher<T>>(sp => sp.GetRequiredService<PostgresRuntimeConfigurationStore<T>>());
        services.AddSingleton<IRuntimeConfigurationStore<T>>(sp => sp.GetRequiredService<PostgresRuntimeConfigurationStore<T>>());
        return services;
    }
}
