using Azure.Core;
using System.Reflection;

namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for <see cref="IConfigurationBuilder"/> that standardise configuration
/// bootstrapping.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds the standard configuration sources used by all projects in the solution:
    /// base path, <c>appsettings.json</c>, environment-specific
    /// <c>appsettings.{environmentName}.json</c>, environment variables, and optionally
    /// user secrets from the supplied assembly.
    /// </summary>
    /// <param name="builder">The configuration builder to configure.</param>
    /// <param name="environmentName">
    /// The hosting environment name (e.g. <c>"Development"</c>). Defaults to <c>"Development"</c>.
    /// </param>
    /// <param name="assembly">
    /// When provided, user secrets are loaded from this assembly's user secrets ID attribute.
    /// Pass <see langword="null"/> to skip user secrets.
    /// </param>
    public static IConfigurationBuilder AddStandardConfiguration(
        this IConfigurationBuilder builder,
        string environmentName = "Development",
        Assembly? assembly = null)
    {
        builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
               .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
               .AddJsonFile($"appsettings.Local.{environmentName}.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();

        if (assembly is not null)
            builder.AddUserSecrets(assembly, optional: true);

        return builder;
    }

    /// <summary>
    /// Adds Azure Key Vault as a configuration source. If either
    /// <paramref name="keyVaultUri"/> or <paramref name="credential"/> is
    /// <see langword="null"/>, the step is skipped silently.
    /// </summary>
    /// <param name="builder">The configuration builder to configure.</param>
    /// <param name="keyVaultUri">
    /// The URI of the Azure Key Vault. Pass <see langword="null"/> to skip.
    /// </param>
    /// <param name="credential">
    /// The token credential used to authenticate with Key Vault.
    /// Pass <see langword="null"/> to skip.
    /// </param>
    public static IConfigurationBuilder AddKeyVaultConfiguration(
        this IConfigurationBuilder builder,
        Uri? keyVaultUri,
        TokenCredential? credential)
    {
        if (keyVaultUri is not null && credential is not null)
            builder.AddAzureKeyVault(keyVaultUri, credential);

        return builder;
    }

    /// <summary>
    /// Performs a partial build of <paramref name="builder"/>, invokes
    /// <paramref name="getCredentials"/> to extract the Key Vault URI and
    /// <see cref="TokenCredential"/> from that snapshot, then conditionally adds
    /// Azure Key Vault as a further configuration source before returning the
    /// builder for further chaining or a final <c>Build()</c>.
    /// </summary>
    /// <param name="builder">The configuration builder to configure.</param>
    /// <param name="getCredentials">
    /// A delegate that receives the partial <see cref="IConfiguration"/> (built from
    /// sources added so far) and returns the Key Vault URI and credential tuple.
    /// Return <see langword="null"/> values to skip Key Vault.
    /// </param>
    public static IConfigurationBuilder AddKeyVaultConfigurationFrom(
        this IConfigurationBuilder builder,
        Func<IConfiguration, (Uri? KeyVaultUri, TokenCredential? Credential)> getCredentials)
    {
        var (uri, cred) = getCredentials(builder.Build());
        return builder.AddKeyVaultConfiguration(uri, cred);
    }
}
