namespace CasCap.Common.Abstractions;

/// <summary>
/// Represents a notification received from an external messaging service.
/// </summary>
public interface IReceivedNotification
{
    /// <summary>
    /// The sender's identifier (e.g. a phone number or account name).
    /// </summary>
    string Sender { get; }

    /// <summary>
    /// The message text, or <see langword="null"/> for attachment-only notifications.
    /// </summary>
    string? Message { get; }

    /// <summary>
    /// Whether the notification contains actionable content (text and/or attachments).
    /// </summary>
    bool HasContent { get; }

    /// <summary>
    /// Attachments included with the notification, or <see langword="null"/> if none are present.
    /// </summary>
    IReadOnlyList<INotificationAttachment>? Attachments { get; }
}
