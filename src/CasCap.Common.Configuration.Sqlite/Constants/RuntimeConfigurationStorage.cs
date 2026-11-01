namespace CasCap.Common.Configuration.Sqlite.Constants;

/// <summary>Fixed ownership boundary within a possibly shared SQLite file.</summary>
public static class RuntimeConfigurationStorage
{
    /// <summary>Table prefix reserved for this provider.</summary>
    public const string Prefix = "cascap_config_";

    /// <summary>Provider-owned migration history table.</summary>
    public const string HistoryTable = Prefix + "migrations_history";
}
