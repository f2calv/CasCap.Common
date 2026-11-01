namespace CasCap.Common.Configuration.Runtime.Models;

/// <summary>Identifies one application and deployment-defined environment.</summary>
public sealed record RuntimeConfigurationScope
{
    /// <summary>Creates a scope without interpreting environment names as a fixed enumeration.</summary>
    public RuntimeConfigurationScope(string application, string environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        if (application.Length > 100 || environment.Length > 100)
            throw new ArgumentException("Configuration scope identifiers cannot exceed 100 characters.");
        Application = application;
        Environment = environment;
    }

    /// <summary>Gets the application identifier.</summary>
    public string Application { get; }

    /// <summary>Gets the environment identifier.</summary>
    public string Environment { get; }
}
