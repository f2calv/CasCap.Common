namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for registering feature flag services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FeatureFlagBgService{T}"/> and binds <see cref="FeatureOptions{T}"/> from the specified configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="sectionName">Configuration section name for <see cref="FeatureOptions{T}"/>.</param>
    /// <param name="addGitMetadataService">
    /// When <see langword="true"/>, registers <see cref="GitMetadataBgService"/> as a hosted service
    /// that periodically logs git build metadata from environment variables.
    /// </param>
    public static void AddFeatureFlagService<T>(this IServiceCollection services, IConfiguration configuration, string sectionName,
        bool addGitMetadataService = false)
        where T : Enum
    {
        services.AddOptionsWithValidateOnStart<FeatureOptions<T>>().BindConfiguration(sectionName).ValidateDataAnnotations();
        services.AddHostedService<FeatureFlagBgService<T>>();

        if (addGitMetadataService)
        {
            services.TryAddSingleton<GitMetadata>();
            services.AddHostedService<GitMetadataBgService>();
        }
    }
}
