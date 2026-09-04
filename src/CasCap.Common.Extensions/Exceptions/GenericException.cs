namespace CasCap.Common.Exceptions;

/// <summary>A generic/catch-all custom exception.</summary>
public class GenericException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GenericException" /> class.</summary>
    public GenericException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GenericException" /> class with an error message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public GenericException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GenericException" /> class with an error message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that caused the current exception.</param>
    public GenericException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}
