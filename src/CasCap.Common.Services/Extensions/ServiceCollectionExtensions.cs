namespace CasCap.Common.Extensions;

/// <summary>Extension methods for registering feature flag services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FeatureFlagBgService"/> with the specified set of enabled feature names.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="enabledFeatures">Case-insensitive set of feature names to enable at runtime.</param>
    /// <param name="addGitMetadataService">
    /// When <see langword="true"/>, registers <see cref="GitMetadataBgService"/> as a hosted service
    /// that periodically logs git build metadata from environment variables.
    /// </param>
    public static IServiceCollection AddFeatureFlagService(this IServiceCollection services,
        IReadOnlySet<string> enabledFeatures,
        bool addGitMetadataService = false)
    {
        services.Configure<FeatureFlagConfig>(o => o.EnabledFeatures = new HashSet<string>(enabledFeatures, StringComparer.OrdinalIgnoreCase));
        services.AddHostedService<FeatureFlagBgService>();

        if (addGitMetadataService)
        {
            services.TryAddSingleton<GitMetadata>();
            services.AddHostedService<GitMetadataBgService>();
        }

        return services;
    }

    /// <summary>
    /// Converts a flags enum value into a set of feature name strings and registers the
    /// non-generic <see cref="FeatureFlagBgService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="enabledFeatures">The bitwise combination of enabled feature flags.</param>
    /// <param name="addGitMetadataService">
    /// When <see langword="true"/>, registers <see cref="GitMetadataBgService"/> as a hosted service
    /// that periodically logs git build metadata from environment variables.
    /// </param>
    public static IServiceCollection AddFeatureFlagService<T>(this IServiceCollection services, T enabledFeatures,
        bool addGitMetadataService = false)
        where T : Enum
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (enabledFeatures.HasFlag(value))
                names.Add(value.ToString());
        }
        return services.AddFeatureFlagService(names, addGitMetadataService);
    }

    /// <summary>
    /// Registers <see cref="FeatureFlagBgService{T}"/> and binds <see cref="FeatureConfig{T}"/> from the specified configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="sectionName">Configuration section name for <see cref="FeatureConfig{T}"/>.</param>
    /// <param name="addGitMetadataService">
    /// When <see langword="true"/>, registers <see cref="GitMetadataBgService"/> as a hosted service
    /// that periodically logs git build metadata from environment variables.
    /// </param>
    [Obsolete("Use the non-generic AddFeatureFlagService overload with string-based feature names instead.")]
    public static IServiceCollection AddFeatureFlagService<T>(this IServiceCollection services, IConfiguration configuration, string sectionName,
        bool addGitMetadataService = false)
        where T : Enum
    {
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddOptionsWithValidateOnStart<FeatureConfig<T>>().BindConfiguration(sectionName).ValidateDataAnnotations();
        services.AddHostedService<FeatureFlagBgService<T>>();
#pragma warning restore CS0618

        if (addGitMetadataService)
        {
            services.TryAddSingleton<GitMetadata>();
            services.AddHostedService<GitMetadataBgService>();
        }

        return services;
    }
}
