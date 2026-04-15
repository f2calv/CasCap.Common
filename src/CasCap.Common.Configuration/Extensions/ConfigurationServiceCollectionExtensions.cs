namespace CasCap.Common.Extensions;

/// <summary>Extension methods for binding and validating <see cref="IAppConfig"/> configuration sections.</summary>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Binds a configuration section to <typeparamref name="TConfig"/> and registers it as a
    /// validated <see cref="IOptions{TConfig}"/> / <see cref="IOptionsMonitor{TConfig}"/> in the DI container.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type implementing <see cref="IAppConfig"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="sectionName">The configuration section name to bind.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCasCapConfiguration<TConfig>(
        this IServiceCollection services,
        string sectionName,
        Action<TConfig>? configure = null)
        where TConfig : class, IAppConfig
    {
        var optionsBuilder = services
            .AddOptionsWithValidateOnStart<TConfig>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations();
        if (configure is not null)
            optionsBuilder.Configure(configure);
        return services;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Binds a configuration section to <typeparamref name="TConfig"/> using
    /// <see cref="IAppConfig.ConfigurationSectionName"/> and registers it as a
    /// validated <see cref="IOptions{TConfig}"/> / <see cref="IOptionsMonitor{TConfig}"/> in the DI container.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type implementing <see cref="IAppConfig"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCasCapConfiguration<TConfig>(
        this IServiceCollection services,
        Action<TConfig>? configure = null)
        where TConfig : class, IAppConfig =>
        services.AddCasCapConfiguration<TConfig>(TConfig.ConfigurationSectionName, configure);

    /// <summary>
    /// Binds a configuration section to <typeparamref name="TConfig"/> using
    /// <see cref="IAppConfig.ConfigurationSectionName"/>, registers it as a validated
    /// <see cref="IOptions{TConfig}"/> / <see cref="IOptionsMonitor{TConfig}"/>, and returns the
    /// eagerly-bound instance for immediate startup use.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type implementing <see cref="IAppConfig"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="configuration">The root <see cref="IConfiguration"/> to read from.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <returns>The eagerly-bound configuration instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration section is missing or cannot be bound.
    /// </exception>
    public static TConfig AddAndGetCasCapConfiguration<TConfig>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TConfig>? configure = null)
        where TConfig : class, IAppConfig
    {
        services.AddCasCapConfiguration<TConfig>(configure);
        return configuration.GetCasCapConfiguration<TConfig>();
    }

    /// <summary>
    /// Reads and returns a <typeparamref name="TConfig"/> instance from its
    /// <see cref="IAppConfig.ConfigurationSectionName"/> configuration section.
    /// </summary>
    /// <remarks>Does not register anything in DI — use <see cref="AddCasCapConfiguration{TConfig}(IServiceCollection, Action{TConfig}?)"/> for that.</remarks>
    /// <typeparam name="TConfig">The configuration type implementing <see cref="IAppConfig"/>.</typeparam>
    /// <param name="configuration">The root <see cref="IConfiguration"/> to read from.</param>
    /// <returns>The bound configuration instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration section is missing or cannot be bound.
    /// </exception>
    public static TConfig GetCasCapConfiguration<TConfig>(this IConfiguration configuration)
        where TConfig : class, IAppConfig =>
        configuration.GetSection(TConfig.ConfigurationSectionName).Get<TConfig>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{TConfig.ConfigurationSectionName}' is missing or empty.");
#endif
}
