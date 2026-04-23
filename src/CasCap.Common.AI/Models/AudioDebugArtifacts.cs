namespace CasCap.Models;

/// <summary>
/// Captures the original and transcoded audio bytes during sub-agent delegation
/// so callers (e.g. debug messages) can inspect the conversion pipeline.
/// </summary>
/// <param name="OriginalBytes">The raw audio bytes received from the messaging platform (e.g. AAC).</param>
/// <param name="OriginalMimeType">The MIME type of the original audio (e.g. <c>"audio/aac"</c>).</param>
/// <param name="TranscodedWav">The transcoded WAV bytes, or <see langword="null"/> if transcoding failed.</param>
public sealed record AudioDebugArtifacts(
    byte[] OriginalBytes,
    string OriginalMimeType,
    byte[]? TranscodedWav);
