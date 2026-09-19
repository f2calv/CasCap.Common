#if NET8_0_OR_GREATER
namespace CasCap.Common.Models;

/// <summary>The outcome of running an external process.</summary>
/// <param name="Output">The bytes the process wrote to standard output.</param>
/// <param name="Error">
/// The standard error text, or <see langword="null"/> when the process wrote none or when
/// <see cref="ProcessErrorCapture.Length"/> was requested.
/// </param>
/// <param name="ErrorLength">
/// The number of characters written to standard error, regardless of whether the text was retained.
/// </param>
/// <param name="ExitCode">The process exit code, or <c>-1</c> when it could not be started.</param>
public readonly record struct ProcessResult(byte[] Output, string? Error, int ErrorLength, int ExitCode)
{
    /// <summary>Whether the process exited cleanly and produced output.</summary>
    public bool Success => ExitCode == 0 && Output.Length > 0;
}
#endif
