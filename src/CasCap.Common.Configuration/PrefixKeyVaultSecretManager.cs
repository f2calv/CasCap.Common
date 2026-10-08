using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace CasCap.Common.Extensions;

/// <summary>Loads one Key Vault secret-name prefix and remaps it beneath a standard configuration section.</summary>
public sealed class PrefixKeyVaultSecretManager : KeyVaultSecretManager
{
    private const string Separator = "--";

    private readonly string _destinationPrefix;
    private readonly string? _exclusiveRootPrefix;
    private readonly string _sourcePrefix;

    /// <summary>Initializes a prefix-filtering and remapping secret manager.</summary>
    /// <param name="sourcePrefix">The Key Vault name prefix to load, without a trailing separator.</param>
    /// <param name="destinationPrefix">The configuration section receiving the stripped secret-name suffix.</param>
    /// <param name="exclusiveRootPrefix">
    /// Optional reserved root. Secrets outside this root retain default mapping; secrets inside it
    /// must match <paramref name="sourcePrefix"/> or are excluded.
    /// </param>
    public PrefixKeyVaultSecretManager(
        string sourcePrefix,
        string destinationPrefix,
        string? exclusiveRootPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePrefix))
            throw new ArgumentException("A non-empty Key Vault source prefix is required.", nameof(sourcePrefix));
        if (string.IsNullOrWhiteSpace(destinationPrefix))
            throw new ArgumentException("A non-empty configuration destination prefix is required.", nameof(destinationPrefix));

        _sourcePrefix = sourcePrefix.TrimEnd('-') + Separator;
        _destinationPrefix = destinationPrefix.TrimEnd(ConfigurationPath.KeyDelimiter.ToCharArray());
        _exclusiveRootPrefix = string.IsNullOrWhiteSpace(exclusiveRootPrefix)
            ? null
            : exclusiveRootPrefix.TrimEnd('-') + Separator;
    }

    /// <inheritdoc/>
    public override bool Load(SecretProperties secret) =>
        base.Load(secret)
        && (secret.Name.StartsWith(_sourcePrefix, StringComparison.OrdinalIgnoreCase)
            || (_exclusiveRootPrefix is { } rootPrefix
                && !secret.Name.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc/>
    public override string GetKey(KeyVaultSecret secret)
    {
        if (!secret.Name.StartsWith(_sourcePrefix, StringComparison.OrdinalIgnoreCase))
            return _exclusiveRootPrefix is { } rootPrefix
                && !secret.Name.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? base.GetKey(secret)
                    : throw new InvalidOperationException(
                        $"Secret '{secret.Name}' is outside the configured Key Vault prefix.");

        var suffix = secret.Name.Substring(_sourcePrefix.Length)
            .Replace(Separator, ConfigurationPath.KeyDelimiter);
        return string.IsNullOrWhiteSpace(suffix)
            ? throw new InvalidOperationException("A Key Vault secret name must contain a suffix after the configured prefix.")
            : ConfigurationPath.Combine(_destinationPrefix, suffix);
    }
}
