namespace CasCap.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddFeatureFlagService<T>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where T : Enum
    {
        services.AddOptionsWithValidateOnStart<FeatureOptions<T>>().BindConfiguration(sectionName).ValidateDataAnnotations();
        services.AddHostedService<FeatureFlagBgService<T>>();
    }
}
