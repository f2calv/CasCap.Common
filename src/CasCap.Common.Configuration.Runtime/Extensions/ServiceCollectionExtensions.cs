using CasCap.Common.Configuration.Runtime.Providers;
using CasCap.Common.Configuration.Runtime.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CasCap.Common.Configuration.Runtime.Extensions;

/// <summary>Registers an isolated runtime root using the standard options monitor implementation.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers one typed contract, its baseline and required durable-reader lifecycle.</summary>
    public static IServiceCollection AddRuntimeConfiguration<T>(
        this IServiceCollection services,
        RuntimeConfigurationOptions settings,
        T baseline
    ) where T : class
    {
        settings.Validate();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RuntimeConfigurationOptions)))
            throw new InvalidOperationException("This initial implementation supports one runtime contract per host.");
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RuntimeConfigurationCodec<T>>();
        services.AddSingleton(sp => new RuntimeConfigurationBaseline<T>(
            sp.GetRequiredService<RuntimeConfigurationCodec<T>>().Serialize(baseline)));
        services.AddSingleton(sp =>
        {
            var provider = new RuntimeSnapshotProvider();
            provider.Publish(sp.GetRequiredService<RuntimeConfigurationBaseline<T>>().Json);
            return provider;
        });
        services.AddSingleton<RuntimeConfigurationRoot<T>>();
        services.AddOptions<T>().ValidateDataAnnotations();
        services.AddSingleton<IOptionsFactory<T>, RuntimeOptionsFactory<T>>();
        services.AddSingleton<IOptionsChangeTokenSource<T>>(sp => new ConfigurationChangeTokenSource<T>(
            sp.GetRequiredService<RuntimeConfigurationRoot<T>>().Root));
        services.AddSingleton<IHostedService, RuntimeConfigurationReader<T>>();
        return services;
    }
}
