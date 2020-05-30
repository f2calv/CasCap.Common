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

        static string sectionKey = $"{nameof(CasCap)}:{nameof(DistCacheOptions)}";

        public static void AddCasCapCaching(this IServiceCollection services, Action<DistCacheOptions> configure)
        {
            //services.AddMemoryCache();//now added inside RedisCacheService
            services.AddSingleton<IRedisCacheService, RedisCacheService>();
            services.AddSingleton<IDistCacheService, DistCacheService>();
            services.AddSingleton<IConfigureOptions<DistCacheOptions>>(s =>
            {
                var configuration = s.GetService<IConfiguration?>();
                return new ConfigureOptions<DistCacheOptions>(options => configuration?.Bind(sectionKey, options));
            });
        }
    }
}