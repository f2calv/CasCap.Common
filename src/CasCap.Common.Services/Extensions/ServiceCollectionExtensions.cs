namespace CasCap.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddFeatureFlagService<T>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where T : Enum
    {
        //var config = configuration.GetSection(sectionName).Get<IFeatureOptions<ApplicationMode>()
        //    ?? throw new GenericException($"{nameof(BuderusKm200Options)} not found!");
        services.AddOptionsWithValidateOnStart<FeatureOptions<T>>().BindConfiguration(sectionName).ValidateDataAnnotations();
        services.AddHostedService<FeatureFlagBgService<T>>();
    }
}
