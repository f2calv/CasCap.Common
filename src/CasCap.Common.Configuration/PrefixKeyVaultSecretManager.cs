using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace CasCap.Common.Extensions;

/// <summary>Loads one Key Vault secret-name prefix and remaps it beneath a standard configuration section.</summary>
public sealed class PrefixKeyVaultSecretManager : KeyVaultSecretManager
{
    private const string Separator = "--";

    private readonly string _destinationPrefix;
    private readonly string _sourcePrefix;

    /// <summary>Initializes a prefix-filtering and remapping secret manager.</summary>
    /// <param name="sourcePrefix">The Key Vault name prefix to load, without a trailing separator.</param>
    /// <param name="destinationPrefix">The configuration section receiving the stripped secret-name suffix.</param>
    public PrefixKeyVaultSecretManager(string sourcePrefix, string destinationPrefix)
    {
        if (string.IsNullOrWhiteSpace(sourcePrefix))
            throw new ArgumentException("A non-empty Key Vault source prefix is required.", nameof(sourcePrefix));
        if (string.IsNullOrWhiteSpace(destinationPrefix))
            throw new ArgumentException("A non-empty configuration destination prefix is required.", nameof(destinationPrefix));

        _sourcePrefix = sourcePrefix.TrimEnd('-') + Separator;
        _destinationPrefix = destinationPrefix.TrimEnd(ConfigurationPath.KeyDelimiter.ToCharArray());
    }

    /// <inheritdoc/>
    public override bool Load(SecretProperties secret) =>
        base.Load(secret)
        && secret.Name.StartsWith(_sourcePrefix, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override string GetKey(KeyVaultSecret secret)
    {
        if (!secret.Name.StartsWith(_sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Secret '{secret.Name}' is outside the configured Key Vault prefix.");

        var suffix = secret.Name[_sourcePrefix.Length..]
            .Replace(Separator, ConfigurationPath.KeyDelimiter);
        if (string.IsNullOrWhiteSpace(suffix))
            throw new InvalidOperationException("A Key Vault secret name must contain a suffix after the configured prefix.");

        return ConfigurationPath.Combine(_destinationPrefix, suffix);
    }
}
