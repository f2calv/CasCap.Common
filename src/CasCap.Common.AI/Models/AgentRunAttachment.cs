namespace CasCap.Models;

/// <summary>Represents a binary attachment produced by a tool during an agent run.</summary>
public sealed record AgentRunAttachment
{
    /// <summary>Base64-encoded content.</summary>
    public required string Base64Content { get; init; }

    /// <summary>MIME type of the content (e.g. <c>image/jpeg</c>).</summary>
    public required string MimeType { get; init; }

    /// <summary>Optional display name for the attachment.</summary>
    public string? FileName { get; init; }
}
