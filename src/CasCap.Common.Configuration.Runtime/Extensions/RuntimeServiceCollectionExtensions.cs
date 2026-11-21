using CasCap.Common.Configuration.Runtime.Providers;
using CasCap.Common.Configuration.Runtime.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace CasCap.Common.Extensions;

/// <summary>Registers an isolated runtime root using the standard options monitor implementation.</summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>Registers one typed contract, its baseline and required durable-reader lifecycle.</summary>
    [RequiresDynamicCode("Use the overload accepting generated JSON metadata and a contract validator for Native AOT.")]
    [RequiresUnreferencedCode("Reflection-based runtime configuration requires the complete DTO graph.")]
    public static IServiceCollection AddRuntimeConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        T baseline
    ) where T : class
    {
        ValidateRegistration(services, settings);
        services.AddSingleton(sp => new RuntimeConfigurationCodec<T>(sp.GetServices<IValidateOptions<T>>()));
        services.AddOptions<T>().ValidateDataAnnotations();
        return AddRuntimeConfigurationCore(services, settings, baseline);
    }

    /// <summary>Registers a typed runtime contract using generated JSON metadata and an explicit graph validator.</summary>
    public static IServiceCollection AddRuntimeConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        T baseline,
        JsonTypeInfo<T> typeInfo,
        IValidateOptions<T> validator
    ) where T : class
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(validator);
        ValidateRegistration(services, settings);
        services.AddSingleton(sp => new RuntimeConfigurationCodec<T>(typeInfo, validator, sp.GetServices<IValidateOptions<T>>()));
        services.AddOptions<T>();
        return AddRuntimeConfigurationCore(services, settings, baseline);
    }

    private static IServiceCollection AddRuntimeConfigurationCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        IServiceCollection services,
        RuntimeConfigurationOptions settings,
        T baseline
    ) where T : class
    {
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new RuntimeConfigurationBaseline<T>(
            sp.GetRequiredService<RuntimeConfigurationCodec<T>>().Serialize(baseline)));
        services.AddSingleton(sp =>
        {
            var provider = new RuntimeSnapshotProvider();
            provider.Publish(sp.GetRequiredService<RuntimeConfigurationBaseline<T>>().Json);
            return provider;
        });
        services.AddSingleton<RuntimeConfigurationRoot<T>>();
        services.AddSingleton<IOptionsFactory<T>, RuntimeOptionsFactory<T>>();
        services.AddSingleton<IOptionsChangeTokenSource<T>>(sp => new ConfigurationChangeTokenSource<T>(
            sp.GetRequiredService<RuntimeConfigurationRoot<T>>().Root));
        services.AddSingleton<IHostedService, RuntimeConfigurationReader<T>>();
        return services;
    }

    private static void ValidateRegistration(IServiceCollection services, RuntimeConfigurationOptions settings)
    {
        settings.Validate();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RuntimeConfigurationOptions)))
            throw new InvalidOperationException("This initial implementation supports one runtime contract per host.");
    }
}
