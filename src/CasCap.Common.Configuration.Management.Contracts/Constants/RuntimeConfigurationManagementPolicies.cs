namespace CasCap.Common.Configuration.Management.Contracts.Constants;

/// <summary>Host-defined, resource-aware policies; the module never supplies permissive defaults.</summary>
public static class RuntimeConfigurationManagementPolicies
{
    /// <summary>Allows viewing the fixed destination and obtaining a CSRF token.</summary>
    public const string View = "RuntimeConfiguration.View";

    /// <summary>Allows validating a candidate for the fixed destination.</summary>
    public const string Edit = "RuntimeConfiguration.Edit";

    /// <summary>Allows publishing a candidate for the fixed destination.</summary>
    public const string Publish = "RuntimeConfiguration.Publish";
}
