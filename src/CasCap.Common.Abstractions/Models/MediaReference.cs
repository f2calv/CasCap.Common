#if NET8_0_OR_GREATER
using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Abstractions;

/// <summary>Pointer to binary media cached in Redis, carried in <see cref="CommsEvent.JsonPayload"/> instead of the bytes.</summary>
/// <remarks>
/// A producer caches the bytes under <see cref="MediaRedisKey"/> with a TTL and serialises this record's
/// properties at the top level of <see cref="CommsEvent.JsonPayload"/>. The comms consumer fetches the
/// bytes, sends them as an attachment and deletes the key. Producer and consumer share only Redis, so no
/// shared filesystem is assumed.
/// </remarks>
public sealed record MediaReference
{
    /// <summary>Redis key where the media bytes are cached with a TTL.</summary>
    [Required, MinLength(1)]
    public required string MediaRedisKey { get; init; }

    /// <summary>MIME type of the media, for example <c>"image/png"</c>.</summary>
    public string? MimeType { get; init; }

    /// <summary>Suggested file name for the attachment.</summary>
    public string? FileName { get; init; }
}
#endif
