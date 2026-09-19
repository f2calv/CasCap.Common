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
    /// <param name="arguments">Command-line arguments, split on spaces.</param>
    /// <param name="stdinBytes">Binary payload to write to the process's standard input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of stdout bytes, stderr text, and exit code.</returns>
    [Obsolete("Arguments are split on spaces, so any argument containing one is passed as several. " +
        "Use the overload taking IEnumerable<string>, which passes each argument through untouched.")]
    public static async Task<(byte[] Output, string? Error, int ExitCode)> RunProcessWithStdinAsync(
        string fileName, string arguments, byte[] stdinBytes, CancellationToken cancellationToken = default)
    {
        var result = await RunProcessWithStdinAsync(fileName, SplitArguments(arguments), stdinBytes,
            ProcessErrorCapture.Text, cancellationToken).ConfigureAwait(false);
        return (result.Output, result.Error, result.ExitCode);
    }

    /// <summary>
    /// Runs an external process with binary data piped to stdin, passing arguments individually so
    /// no quoting or escaping is required.
    /// </summary>
    /// <param name="fileName">The executable to run (e.g. <c>"ffmpeg"</c>).</param>
    /// <param name="arguments">Command-line arguments, one element per argument.</param>
    /// <param name="stdinBytes">Binary payload to write to the process's standard input.</param>
    /// <param name="errorCapture">How much of the standard error stream to retain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="ProcessResult"/> describing the run.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <remarks>
    /// The process is killed, along with any children, whenever this method stops waiting on it —
    /// including on cancellation — so a hung or slow process cannot outlive the call.
    /// </remarks>
    public static async Task<ProcessResult> RunProcessWithStdinAsync(
        string fileName, IEnumerable<string> arguments, byte[] stdinBytes,
        ProcessErrorCapture errorCapture = ProcessErrorCapture.Text,
        CancellationToken cancellationToken = default)
    {
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        if (stdinBytes is null) throw new ArgumentNullException(nameof(stdinBytes));

        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new([], ex.Message, ex.Message.Length, -1);
        }

        try
        {
            //Both output pipes must drain concurrently with the write, otherwise the process blocks on a full buffer.
            var outputTask = ReadAllBytesAsync(process.StandardOutput.BaseStream, cancellationToken);
            var errorTask = ReadErrorAsync(process.StandardError, errorCapture, cancellationToken);

#if NET8_0_OR_GREATER
            await using (var input = process.StandardInput.BaseStream)
                await input.WriteAsync(stdinBytes, cancellationToken).ConfigureAwait(false);
#endif

            var output = await outputTask.ConfigureAwait(false);
            var (error, errorLength) = await errorTask.ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new(output, error, errorLength, process.ExitCode);
        }
        catch (IOException ex)
        {
            return new([], ex.Message, ex.Message.Length, -1);
        }
        finally
        {
            //Disposing a Process does not stop it, so an unfinished child is terminated explicitly.
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException
                    or System.ComponentModel.Win32Exception)
                {
                    //The process ended between the check and the kill, or the platform refused; either
                    //  way it is no longer running, which is all this guard wanted.
                }
            }
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static async Task<(string? Error, int Length)> ReadErrorAsync(StreamReader reader,
        ProcessErrorCapture errorCapture, CancellationToken cancellationToken)
    {
        if (errorCapture is ProcessErrorCapture.Text)
        {
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return (text.Length > 0 ? text : null, text.Length);
        }

        //The text is counted and discarded as it arrives, so it is never held in full.
        var length = 0;
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            length += read;
        return (null, length);
    }

    //ArgumentList requires individual arguments; this preserves the legacy single-string overload.
    private static string[] SplitArguments(string? arguments) =>
        string.IsNullOrWhiteSpace(arguments)
            ? []
            : arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
