#if NET8_0_OR_GREATER
namespace CasCap.Common.Models;

/// <summary>Controls how much of an external process's standard error stream is retained.</summary>
public enum ProcessErrorCapture
{
    /// <summary>Retain the full standard error text. This is the default.</summary>
    Text = 0,

    /// <summary>
    /// Retain only the character count, discarding the text as it is read.
    /// </summary>
    /// <remarks>
    /// Diagnostic output frequently echoes the content a process was given — ffmpeg, for example,
    /// prints container metadata and input filenames. Where that content belongs to a third party,
    /// the count is enough to prove something was written without ever holding the text in memory.
    /// </remarks>
    Length = 1,
}
#endif
