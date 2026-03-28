namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for binding and validating <see cref="IAppConfig"/> configuration sections.
/// </summary>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Binds a configuration section to <typeparamref name="TConfig"/> and registers it as a
    /// validated <see cref="IOptions{TConfig}"/> / <see cref="IOptionsMonitor{TConfig}"/> in the DI container.
    /// The section name defaults to <c>nameof(TConfig)</c> when <paramref name="sectionName"/> is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type implementing <see cref="IAppConfig"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="configuration">The root <see cref="IConfiguration"/>.</param>
    /// <param name="sectionName">
    /// Optional configuration section name. When <see langword="null"/>, <c>nameof(TConfig)</c> is used.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCasCapConfiguration<TConfig>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null)
        where TConfig : class, IAppConfig
    {
        var name = sectionName ?? typeof(TConfig).Name;

        services
            .AddOptionsWithValidateOnStart<TConfig>()
            .BindConfiguration(name)
            .ValidateDataAnnotations();

        return services;
    }
}
