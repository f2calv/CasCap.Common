using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace CasCap.Common.Configuration.Tests.Integration;

/// <summary>Verifies prefix filtering against a caller-configured Azure Key Vault.</summary>
[Trait("Category", "Integration")]
[Trait("Category", "Key Vault")]
public sealed class KeyVaultPrefixIntegrationTests
{
    private const string ConfigurationSection = "KeyVaultConfigurationTests";
    private const string DestinationSection = "CasCap:AgentRuntimeAzureAuthConfig";
    private const string ExcludedKey = $"{DestinationSection}:ExcludedOnly";
    private const string SelectedClientId = "00000000-0000-0000-0000-000000000001";
    private const string SourcePrefix = "ConfigurationTests--SmartHaus--Exec";

    [Fact]
    public void AddKeyVaultConfiguration_SelectedPrefixIsMappedAndOtherPrefixIsExcluded()
    {
        var bootstrap = new ConfigurationBuilder()
            .AddUserSecrets(typeof(KeyVaultPrefixIntegrationTests).Assembly, optional: true)
            .Build()
            .GetSection(ConfigurationSection);
        var keyVaultUri = bootstrap[nameof(KeyVaultUri)];
        var tenantId = bootstrap[nameof(TenantId)];
        var clientId = bootstrap[nameof(ClientId)];
        var certificateThumbprint = bootstrap[nameof(CertificateThumbprint)];
        Assert.SkipUnless(
            Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var vaultUri)
            && !string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(certificateThumbprint),
            $"Configure {ConfigurationSection} in the repository user-secrets store to run the live Key Vault test.");

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var certificates = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            certificateThumbprint,
            validOnly: false);
        Assert.SkipUnless(certificates.Count > 0, "The configured edge identity certificate is not installed.");

        var credential = new ClientCertificateCredential(tenantId, clientId, certificates[0]);
        var configuration = new ConfigurationBuilder()
            .AddKeyVaultConfiguration(
                vaultUri,
                credential,
                new PrefixKeyVaultSecretManager(SourcePrefix, DestinationSection))
            .Build();

        Assert.Equal(SelectedClientId, configuration[$"{DestinationSection}:ClientId"]);
        Assert.Null(configuration[ExcludedKey]);
    }

    private static string CertificateThumbprint => nameof(CertificateThumbprint);

    private static string ClientId => nameof(ClientId);

    private static string KeyVaultUri => nameof(KeyVaultUri);

    private static string TenantId => nameof(TenantId);
}
