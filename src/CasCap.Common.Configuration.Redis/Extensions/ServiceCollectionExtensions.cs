using CasCap.Common.Abstractions;
using CasCap.Common.Configuration.Redis.Models;
using CasCap.Common.Configuration.Redis.Services;
using CasCap.Common.Configuration.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CasCap.Common.Configuration.Redis.Extensions;

/// <summary>Opt-in cache wiring; durable providers do not acquire a Redis dependency.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Decorates reads after registering the durable contract and shared CasCap caching.</summary>
    public static IServiceCollection AddRuntimeConfigurationCache<T, TStore>(
        this IServiceCollection services,
        RuntimeConfigurationCacheOptions settings
    ) where T : class where TStore : class, IRuntimeConfigurationStore<T>
    {
        settings.Validate();
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(TStore))
            || !services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedCache)))
            throw new InvalidOperationException("Register the concrete durable store and CasCap caching before the configuration cache.");
        if (services.Any(descriptor => descriptor.ServiceType == typeof(CachedRuntimeConfigurationStore<T>)))
            throw new InvalidOperationException("The runtime configuration cache is already registered.");
        services.AddSingleton(provider => new CachedRuntimeConfigurationStore<T>(
            provider.GetRequiredService<ILogger<CachedRuntimeConfigurationStore<T>>>(),
            provider.GetRequiredService<TimeProvider>(),
            settings,
            provider.GetRequiredService<RuntimeConfigurationOptions>(),
            provider.GetRequiredService<IDistributedCache>(),
            provider.GetRequiredService<TStore>(),
            provider.GetRequiredService<RuntimeConfigurationCodec<T>>()));
        services.Replace(ServiceDescriptor.Singleton<IRuntimeConfigurationStore<T>>(
            provider => provider.GetRequiredService<CachedRuntimeConfigurationStore<T>>()));
        return services;
    }
}
