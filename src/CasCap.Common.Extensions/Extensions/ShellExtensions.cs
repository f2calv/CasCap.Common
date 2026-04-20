using System.Runtime.InteropServices;

namespace CasCap.Common.Extensions;

/// <summary>
/// Extension and helper methods for running external processes.
/// </summary>
public static class ShellExtensions
{
    /// <summary>
    /// Runs an external process and returns its trimmed standard output,
    /// or <see langword="null"/> when the process fails or produces no output.
    /// </summary>
    public static string? RunProcess(string fileName, string? arguments = null)
    {
        var (output, _, exitCode) = RunProcessDiagnostic(fileName, arguments);
        return exitCode == 0 ? output : null;
    }

    /// <summary>
    /// Runs an external process and returns its trimmed standard output, standard error, and exit code.
    /// </summary>
    /// <remarks>Unlike <see cref="RunProcess"/>, this overload preserves diagnostic information for troubleshooting.</remarks>
    public static (string? Output, string? Error, int ExitCode) RunProcessDiagnostic(string fileName, string? arguments = null)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            return (
                output.Length > 0 ? output : null,
                error.Length > 0 ? error : null,
                process.ExitCode);
        }
        catch (Exception ex)
        {
            return (null, ex.Message, -1);
        }
    }


#if NET8_0_OR_GREATER
    /// <summary>
    /// Runs an external process with binary data piped to stdin and returns the stdout bytes.
    /// </summary>
    /// <remarks>
    /// Useful for in-memory media transcoding (e.g. <c>ffmpeg -i pipe:0 ... pipe:1</c>)
    /// where no temporary files are needed.
    /// </remarks>
    /// <param name="fileName">The executable to run (e.g. <c>"ffmpeg"</c>).</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="stdinBytes">Binary payload to write to the process's standard input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of stdout bytes, stderr text, and exit code.</returns>
    public static async Task<(byte[] Output, string? Error, int ExitCode)> RunProcessWithStdinAsync(
        string fileName, string arguments, byte[] stdinBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            // Write input bytes to stdin then close to signal EOF.
            await process.StandardInput.BaseStream.WriteAsync(stdinBytes, cancellationToken);
            process.StandardInput.Close();

            // Read stdout and stderr concurrently to avoid deadlocks.
            using var outputStream = new MemoryStream();
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(copyTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            var error = await errorTask;
            return (
                outputStream.ToArray(),
                error.Length > 0 ? error : null,
                process.ExitCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ([], ex.Message, -1);
        }
    }
#endif

    /// <summary>
    /// Executes a Bash command on Linux and returns the standard output.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Linux.</exception>
    /// <exception cref="GenericException">Thrown when the command exits with a non-zero code.</exception>
    public static string Bash(this string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) throw new ArgumentNullException(nameof(cmd));
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) throw new PlatformNotSupportedException(RuntimeInformation.OSDescription);
        var escapedArgs = cmd.Replace("\"", "\\\"");
        return RunProcess("/bin/bash", $"-c \"{escapedArgs}\"")
            ?? throw new GenericException($"error, args={cmd}");
    }
}
