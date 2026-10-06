namespace CasCap.Common.Abstractions;

/// <summary>Represents metadata for an attachment received as part of a notification.</summary>
public interface INotificationAttachment
{
    /// <summary>The attachment identifier used to retrieve the attachment content.</summary>
    public string? Id { get; }

    /// <summary>The MIME content type (e.g. <c>"image/jpeg"</c>, <c>"audio/aac"</c>).</summary>
    public string? ContentType { get; }
}
