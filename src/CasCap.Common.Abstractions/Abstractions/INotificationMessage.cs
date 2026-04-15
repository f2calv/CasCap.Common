namespace CasCap.Common.Abstractions;

/// <summary>Represents an outgoing notification message.</summary>
public interface INotificationMessage
{
    /// <summary>The message text to send.</summary>
    string Message { get; }

    /// <summary>The sender's identifier (e.g. a phone number or account name).</summary>
    string Sender { get; }

    /// <summary>The intended recipients of the message.</summary>
    string[] Recipients { get; }
}
