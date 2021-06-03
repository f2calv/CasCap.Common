using CasCap.Models;
using CasCap.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
namespace Microsoft.Extensions.DependencyInjection
{
    public static class DI
    {
        public static void AddCasCapCaching(this IServiceCollection services)
            => services.AddCasCapCaching(_ => { });

        public static void AddCasCapCaching(this IServiceCollection services, Action<CachingOptions> configure)
        {
            //services.AddMemoryCache();//now added inside DistCacheService
            services.AddSingleton<IRedisCacheService, RedisCacheService>();
            services.AddSingleton<IDistCacheService, DistCacheService>();
            services.AddHostedService<LocalCacheInvalidationService>();
            services.AddSingleton<IConfigureOptions<CachingOptions>>(s =>
            {
                var configuration = s.GetService<IConfiguration?>();
                return new ConfigureOptions<CachingOptions>(options => configuration?.Bind(CachingOptions.sectionKey, options));
            });
        }
    }
}