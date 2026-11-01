namespace CasCap.Common.Configuration.Management.Contracts.Constants;

/// <summary>Stable management routes. Each host serves exactly one configured destination.</summary>
public static class RuntimeConfigurationManagementRoutes
{
    /// <summary>The controller route template.</summary>
    public const string Destination = "api/runtime-configuration/{application}/{environment}/{contract}";

    /// <summary>The antiforgery token endpoint suffix.</summary>
    public const string Antiforgery = "antiforgery";

    /// <summary>The non-mutating typed validation endpoint suffix.</summary>
    public const string Preview = "preview";

    /// <summary>The optimistic publication endpoint suffix.</summary>
    public const string Publish = "publish";
}
