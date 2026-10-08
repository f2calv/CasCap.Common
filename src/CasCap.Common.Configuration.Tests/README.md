# CasCap.Common.Configuration.Tests

Unit and local integration coverage for `CasCap.Common.Configuration`.

## Tests

| Class | Methods | Cases | Category | Description |
| --- | ---: | ---: | --- | --- |
| `PrefixKeyVaultSecretManagerTests` | 4 | 5 | Key Vault | Prefix validation, filtering, remapping, and rejection |
| `KeyVaultPrefixIntegrationTests` | 1 | 1 | Integration, Key Vault | Live selected-prefix loading and exclusion from a caller-configured vault |

## Local Configuration

The live integration test reads `KeyVaultConfigurationTests` from the repository's shared .NET User Secrets store:

```json
{
  "KeyVaultConfigurationTests": {
    "KeyVaultUri": "https://example.vault.azure.net/",
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "ClientId": "00000000-0000-0000-0000-000000000000",
    "CertificateThumbprint": "0123456789ABCDEF0123456789ABCDEF01234567"
  }
}
```

Missing configuration or an unavailable certificate skips the live test. Values and credentials are never logged.

## Running Locally

```powershell
dotnet test --project src/CasCap.Common.Configuration.Tests/CasCap.Common.Configuration.Tests.csproj
```

## Layout

```text
Tests/
├── Integration/
│   └── KeyVaultPrefixIntegrationTests.cs
└── Unit/
    └── PrefixKeyVaultSecretManagerTests.cs
```
