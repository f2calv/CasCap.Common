namespace CasCap.Common.Exceptions;

/// <summary>Thrown when an imperative startup check finds missing, empty or conflicting configuration that options validation cannot express.</summary>
public sealed class ConfigurationException : GenericException
{
    /// <summary>Initializes a new instance of the <see cref="ConfigurationException" /> class.</summary>
    public ConfigurationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConfigurationException" /> class with an error message.</summary>
    /// <param name="message">A description of the configuration failure without secrets or local paths.</param>
    public ConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConfigurationException" /> class with an error message and inner exception.</summary>
    /// <param name="message">A description of the configuration failure without secrets or local paths.</param>
    /// <param name="inner">The exception that caused the current exception.</param>
    public ConfigurationException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}
