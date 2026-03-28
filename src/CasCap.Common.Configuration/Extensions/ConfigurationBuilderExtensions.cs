namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for building a standard <see cref="IConfiguration"/> pipeline.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds the standard CasCap configuration sources: <c>appsettings.json</c>,
    /// environment-specific <c>appsettings.{environmentName}.json</c>, and environment variables.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to configure.</param>
    /// <param name="environmentName">The hosting environment name (e.g. Development, Production).</param>
    /// <returns>The same <see cref="IConfigurationBuilder"/> for chaining.</returns>
    public static IConfigurationBuilder AddStandardSources(
        this IConfigurationBuilder builder,
        string environmentName)
    {
        builder
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        return builder;
    }
}
