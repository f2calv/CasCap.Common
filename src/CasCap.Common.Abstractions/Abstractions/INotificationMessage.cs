namespace CasCap.Common.Abstractions;

/// <summary>Represents an outgoing notification message.</summary>
public interface INotificationMessage
{
    /// <summary>The message text to send.</summary>
    public string Message { get; }

    /// <summary>The sender's identifier (e.g. a phone number or account name).</summary>
    public string Sender { get; }

    /// <summary>The intended recipients of the message.</summary>
    public string[] Recipients { get; }
}
