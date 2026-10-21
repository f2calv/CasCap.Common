using Azure.Security.KeyVault.Secrets;

namespace CasCap.Common.Configuration.Tests.Unit;

/// <summary>Tests deterministic Key Vault prefix filtering and configuration-key mapping.</summary>
[Trait("Category", "Key Vault")]
public sealed class PrefixKeyVaultSecretManagerTests
{
    [Theory]
    [InlineData("", "CasCap:AgentRuntimeAzureAuthConfig")]
    [InlineData("AgentRuntime--SmartHaus--Exec", "")]
    public void Constructor_EmptyPrefixIsRejected(string sourcePrefix, string destinationPrefix) =>
        Assert.Throws<ArgumentException>(() => new PrefixKeyVaultSecretManager(sourcePrefix, destinationPrefix));

    [Fact]
    public void Load_MatchingPrefixOnlyIsAccepted()
    {
        var manager = new PrefixKeyVaultSecretManager(
            "AgentRuntime--SmartHaus--Exec",
            "CasCap:AgentRuntimeAzureAuthConfig");

        var selected = manager.Load(new KeyVaultSecret(
            "AgentRuntime--SmartHaus--Exec--ClientId",
            "selected").Properties);
        var excluded = manager.Load(new KeyVaultSecret(
            "AgentRuntime--CAS--Exec--ClientId",
            "excluded").Properties);

        Assert.True(selected);
        Assert.False(excluded);
    }

    [Fact]
    public void GetKey_StripsSourcePrefixAndPrependsDestinationSection()
    {
        var manager = new PrefixKeyVaultSecretManager(
            "AgentRuntime--SmartHaus--Exec",
            "CasCap:AgentRuntimeAzureAuthConfig");
        var secret = new KeyVaultSecret(
            "AgentRuntime--SmartHaus--Exec--Certificate--Pem",
            "value");

        var key = manager.GetKey(secret);

        Assert.Equal("CasCap:AgentRuntimeAzureAuthConfig:Certificate:Pem", key);
    }

    [Fact]
    public void GetKey_OutsideSourcePrefixIsRejected()
    {
        var manager = new PrefixKeyVaultSecretManager(
            "AgentRuntime--SmartHaus--Exec",
            "CasCap:AgentRuntimeAzureAuthConfig");
        var secret = new KeyVaultSecret("AgentRuntime--CAS--Exec--ClientId", "value");

        Assert.Throws<InvalidOperationException>(() => manager.GetKey(secret));
    }

    [Fact]
    public void ExclusiveRoot_LoadsSelectedAndLegacyButRejectsOtherRootSecrets()
    {
        var manager = new PrefixKeyVaultSecretManager(
            "AgentRuntime--SmartHaus--Exec",
            "CasCap:AgentRuntimeAzureAuthConfig",
            "AgentRuntime");

        var selected = manager.Load(new KeyVaultSecret(
            "AgentRuntime--SmartHaus--Exec--ClientId",
            "selected").Properties);
        var otherApplication = manager.Load(new KeyVaultSecret(
            "AgentRuntime--CAS--Exec--ClientId",
            "excluded").Properties);
        var legacy = manager.Load(new KeyVaultSecret("AppConfig--KeyVaultName", "legacy").Properties);

        Assert.True(selected);
        Assert.False(otherApplication);
        Assert.True(legacy);
    }

    [Fact]
    public void ExclusiveRoot_LegacySecretUsesDefaultMapping()
    {
        var manager = new PrefixKeyVaultSecretManager(
            "AgentRuntime--SmartHaus--Exec",
            "CasCap:AgentRuntimeAzureAuthConfig",
            "AgentRuntime");
        var secret = new KeyVaultSecret("AppConfig--KeyVaultName", "legacy");

        var key = manager.GetKey(secret);

        Assert.Equal("AppConfig:KeyVaultName", key);
    }
}
