namespace CasCap.Common.Exceptions;

/// <summary>A generic/catch-all custom exception.</summary>
public class GenericException : Exception
{
    /// <inheritdoc/>
    public GenericException()
    {
    }

    /// <inheritdoc/>
    public GenericException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public GenericException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
