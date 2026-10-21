# CasCap.Common.Configuration.Tests

Unit and optional live Key Vault tests for prefix filtering and configuration-key mapping in `CasCap.Common.Configuration`.

**Target framework:** `net10.0`

## Running

From the repository root with the .NET 10 SDK:

```bash
dotnet test --project src/CasCap.Common.Configuration.Tests/CasCap.Common.Configuration.Tests.csproj
```

The unit cases are offline. To exclude the live test explicitly:

```bash
dotnet test --project src/CasCap.Common.Configuration.Tests/CasCap.Common.Configuration.Tests.csproj --filter-not-trait "Category=Integration"
```

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

The example values are placeholders. Supply actual values through the repository's User Secrets
store, never tracked configuration. The certificate must be in the current user's personal
certificate store and the identity must be allowed to read the vault.

The test is read-only: prepare the selected-prefix and excluded-prefix fixture secrets separately,
using the constants and expectations in
[KeyVaultPrefixIntegrationTests](Tests/Integration/KeyVaultPrefixIntegrationTests.cs).
It does not create vault contents or initiate interactive sign-in.

## Tests

Counts are declared methods and expanded data rows, not execution results.

| Test class | Methods | Test cases | Scope |
| --- | ---: | ---: | --- |
| `PrefixKeyVaultSecretManagerTests` | 6 | 7 | Prefix validation, filtering, remapping, exclusive-root isolation and rejection |
| `KeyVaultPrefixIntegrationTests` | 1 | 1 | Live selected-prefix loading and exclusion |
| **Total** | **7** | **8** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `Key Vault` | Both classes |
| `Integration` | Live vault test |

### Skipped Tests

One case has conditional skips; these are alternatives, not two skipped cases:

| Test | Cases | Skip condition |
| --- | ---: | --- |
| Live prefix integration test | 1 | Missing/invalid bootstrap configuration or configured certificate not installed |

Once prerequisites are present, authentication, access and fixture assertion failures fail the test
rather than being treated as skips.

## Layout

```text
Tests/
├── Integration/
│   └── KeyVaultPrefixIntegrationTests.cs
└── Unit/
    └── PrefixKeyVaultSecretManagerTests.cs
```
