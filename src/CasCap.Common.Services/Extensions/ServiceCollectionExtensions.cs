namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for registering feature flag services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FeatureFlagBgService{T}"/> and binds <see cref="FeatureOptions{T}"/> from the specified configuration section.
    /// </summary>
    public static void AddFeatureFlagService<T>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where T : Enum
    {
        services.AddOptionsWithValidateOnStart<FeatureOptions<T>>().BindConfiguration(sectionName).ValidateDataAnnotations();
        services.AddHostedService<FeatureFlagBgService<T>>();
    }
}
