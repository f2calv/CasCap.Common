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

        static readonly string sectionKey = $"{nameof(CasCap)}:{nameof(CachingConfig)}";

        public static void AddCasCapCaching(this IServiceCollection services, Action<CachingConfig> configure)
        {
            //services.AddMemoryCache();//now added inside DistCacheService
            services.AddSingleton<IRedisCacheService, RedisCacheService>();
            services.AddSingleton<IDistCacheService, DistCacheService>();
            services.AddHostedService<LocalCacheInvalidationService>();
            services.AddSingleton<IConfigureOptions<CachingConfig>>(s =>
            {
                var configuration = s.GetService<IConfiguration?>();
                return new ConfigureOptions<CachingConfig>(options => configuration?.Bind(sectionKey, options));
            });
        }
    }
}