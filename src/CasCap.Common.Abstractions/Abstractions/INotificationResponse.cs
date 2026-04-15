namespace CasCap.Common.Abstractions;

/// <summary>Represents the response returned after sending a notification.</summary>
public interface INotificationResponse
{
    /// <summary>The server-assigned timestamp for the sent notification.</summary>
    string Timestamp { get; }
}
