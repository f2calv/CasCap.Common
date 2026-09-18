# CasCap.Common.Configuration

Configuration bootstrapping helpers for .NET applications — standard `IConfiguration` pipeline setup, Azure Key Vault integration, and validated `IOptions<T>` binding for `IAppConfig` records.

## Installation

```bash
dotnet add package CasCap.Common.Configuration
```

## Purpose

Provides a standardised way to build the configuration pipeline (`appsettings.json`, environment and local overrides, user secrets, environment variables, Azure Key Vault) and to bind configuration sections to `IAppConfig` record types with DataAnnotations validation on startup.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Extensions

| Class | Key Methods |
| --- | --- |
| `ConfigurationBuilderExtensions` | `AddStandardConfiguration()` — sets base path, registers base/environment/local JSON, optional user secrets, then environment variables |
| | `AddKeyVaultConfiguration()` — conditionally adds Azure Key Vault (skips silently when URI or credential is `null`) |
| | `AddKeyVaultConfigurationFrom()` — partial-builds configuration, extracts Key Vault credentials via delegate, then adds Key Vault |
| `ConfigurationServiceCollectionExtensions` | `AddCasCapConfiguration<TConfig>()` — binds a configuration section to an `IAppConfig` record with `ValidateDataAnnotations` and `ValidateOnStart` |

## Usage

```csharp
var configuration = new ConfigurationBuilder()
    .AddStandardConfiguration(environmentName, Assembly.GetExecutingAssembly())
    .AddKeyVaultConfigurationFrom(cfg =>
    {
        var appConfig = cfg.GetSection(AppConfig.ConfigurationSectionName).Get<AppConfig>();
        return (appConfig?.KeyVaultUri, appConfig?.TokenCredential);
    })
    .Build();
```

## Configuration Hierarchy

Configuration bootstrapping flow with layered sources:

```mermaid
flowchart TD
    START["ConfigurationBuilder"]

    subgraph StandardConfig["AddStandardConfiguration()"]
        BASE["SetBasePath(contentRoot)"]
        APPSETTINGS["appsettings.json"]
        ENV_FILE["appsettings.{Environment}.json"]
        LOCAL["appsettings.Local.json"]
        LOCAL_ENV["appsettings.Local.{Environment}.json"]
        SECRETS["User Secrets<br/>(optional)"]
        ENV_VARS["Environment Variables"]
    end

    subgraph KeyVaultConfig["AddKeyVaultConfiguration()"]
        PARTIAL["Partial Build Config"]
        EXTRACT["Extract Key Vault URI<br/>+ TokenCredential"]
        KV["Azure Key Vault<br/>(secrets override)"]
    end

    subgraph Binding["IOptions Binding"]
        VALIDATE["AddCasCapConfiguration<TConfig>()<br/>ValidateDataAnnotations<br/>ValidateOnStart"]
        OPTIONS["IOptions<TConfig><br/>(DI injectable)"]
    end

    START --> BASE
    BASE --> APPSETTINGS
    APPSETTINGS --> ENV_FILE
    ENV_FILE --> LOCAL
    LOCAL --> LOCAL_ENV
    LOCAL_ENV --> SECRETS
    SECRETS --> ENV_VARS

    ENV_VARS --> PARTIAL
    PARTIAL --> EXTRACT
    EXTRACT -."if URI present".-> KV

    KV --> VALIDATE
    ENV_VARS -."if no Key Vault".-> VALIDATE
    VALIDATE --> OPTIONS
```

**Layering Priority** (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. `appsettings.Local.json` (optional)
4. `appsettings.Local.{Environment}.json` (optional)
5. User Secrets (when an assembly is supplied)
6. Environment Variables
7. Azure Key Vault (if configured)

## Dependencies

### NuGet Packages

| Package | Purpose |
| --- | --- |
| `Azure.Extensions.AspNetCore.Configuration.Secrets` | Azure Key Vault configuration provider |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | Environment variable configuration source |
| `Microsoft.Extensions.Configuration.FileExtensions` | File-based configuration helpers (`SetBasePath`) |
| `Microsoft.Extensions.Configuration.Json` | JSON file configuration source |
| `Microsoft.Extensions.Configuration.UserSecrets` | User secrets configuration source |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `IOptions<T>` binding to `IConfiguration` |
| `Microsoft.Extensions.Options.DataAnnotations` | `ValidateDataAnnotations` / `ValidateOnStart` |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | `IAppConfig` contract used as a generic constraint |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
