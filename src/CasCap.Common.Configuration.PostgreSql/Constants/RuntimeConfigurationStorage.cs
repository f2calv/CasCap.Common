namespace CasCap.Common.Configuration.PostgreSql.Constants;

/// <summary>Names the schema owned by the runtime-configuration add-on.</summary>
internal static class RuntimeConfigurationStorage
{
    /// <summary>Schema shared by runtime mappings and migration history.</summary>
    internal const string Schema = "cascap_config";
}
