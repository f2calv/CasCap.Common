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

    /// <summary>
    /// Lists available groups for the specified account.
    /// </summary>
    /// <param name="account">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<INotificationGroup[]?> ListGroupsAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals the recipient that the bot is actively processing a request.
    /// </summary>
    /// <param name="account">The sender account identifier.</param>
    /// <param name="recipient">The recipient identifier (phone number or group ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> StartProcessingAsync(string account, string recipient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals the recipient that the bot has finished processing.
    /// </summary>
    /// <param name="account">The sender account identifier.</param>
    /// <param name="recipient">The recipient identifier (phone number or group ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> StopProcessingAsync(string account, string recipient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a progress-update indicator (e.g. an emoji reaction) to a specific message.
    /// </summary>
    /// <param name="account">The sender account identifier.</param>
    /// <param name="recipient">The recipient identifier (phone number or group ID).</param>
    /// <param name="reaction">The reaction emoji.</param>
    /// <param name="targetAuthor">The author of the message being reacted to.</param>
    /// <param name="timestamp">The timestamp of the target message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> SendProgressUpdateAsync(string account, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default);
}
