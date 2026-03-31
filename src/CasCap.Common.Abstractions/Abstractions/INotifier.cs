namespace CasCap.Common.Abstractions;

/// <summary>
/// Abstracts a notification service capable of sending and receiving messages with optional
/// attachment support.
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Sends a notification message to the specified recipients.
    /// </summary>
    /// <param name="message">The outgoing notification message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<INotificationResponse?> SendAsync(INotificationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives pending notifications for the specified account.
    /// </summary>
    /// <param name="account">The account identifier to poll for incoming messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReceivedNotification[]?> ReceiveAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the raw bytes of an attachment by its identifier.
    /// </summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<byte[]?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);
}
