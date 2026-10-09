using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace CasCap.Common.Extensions;

/// <summary>Loads one Key Vault secret-name prefix and remaps it beneath a standard configuration section.</summary>
/// <param name="sourcePrefix">The Key Vault name prefix to load, without a trailing separator.</param>
/// <param name="destinationPrefix">The configuration section receiving the stripped secret-name suffix.</param>
/// <param name="exclusiveRootPrefix">
/// Optional reserved root. Secrets outside this root retain default mapping; secrets inside it
/// must match <paramref name="sourcePrefix"/> or are excluded.
/// </param>
public sealed class PrefixKeyVaultSecretManager(
    string sourcePrefix,
    string destinationPrefix,
    string? exclusiveRootPrefix = null) : KeyVaultSecretManager
{
    private const string Separator = "--";

    private readonly (string Source, string Destination, string? ExclusiveRoot) _prefixes =
        NormalizePrefixes(sourcePrefix, destinationPrefix, exclusiveRootPrefix);

    /// <inheritdoc/>
    public override bool Load(SecretProperties secret) =>
        base.Load(secret)
        && (secret.Name.StartsWith(_prefixes.Source, StringComparison.OrdinalIgnoreCase)
            || (_prefixes.ExclusiveRoot is { } rootPrefix
                && !secret.Name.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc/>
    public override string GetKey(KeyVaultSecret secret)
    {
        if (!secret.Name.StartsWith(_prefixes.Source, StringComparison.OrdinalIgnoreCase))
            return _prefixes.ExclusiveRoot is { } rootPrefix
                && !secret.Name.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? base.GetKey(secret)
                    : throw new InvalidOperationException(
                        $"Secret '{secret.Name}' is outside the configured Key Vault prefix.");

        var suffix = secret.Name.Substring(_prefixes.Source.Length)
            .Replace(Separator, ConfigurationPath.KeyDelimiter);
        return string.IsNullOrWhiteSpace(suffix)
            ? throw new InvalidOperationException("A Key Vault secret name must contain a suffix after the configured prefix.")
            : ConfigurationPath.Combine(_prefixes.Destination, suffix);
    }

    private static (string Source, string Destination, string? ExclusiveRoot) NormalizePrefixes(
        string sourcePrefix,
        string destinationPrefix,
        string? exclusiveRootPrefix)
        => (
            string.IsNullOrWhiteSpace(sourcePrefix)
                ? throw new ArgumentException("A non-empty Key Vault source prefix is required.", nameof(sourcePrefix))
                : sourcePrefix.TrimEnd('-') + Separator,
            string.IsNullOrWhiteSpace(destinationPrefix)
                ? throw new ArgumentException("A non-empty configuration destination prefix is required.", nameof(destinationPrefix))
                : destinationPrefix.TrimEnd(ConfigurationPath.KeyDelimiter.ToCharArray()),
            exclusiveRootPrefix is null || string.IsNullOrWhiteSpace(exclusiveRootPrefix)
                ? null
                : exclusiveRootPrefix.TrimEnd('-') + Separator);
}
