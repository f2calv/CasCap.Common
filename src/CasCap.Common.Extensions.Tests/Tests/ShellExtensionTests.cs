using CasCap.Common.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace CasCap.Common.Extensions.Tests;

/// <summary>
/// Tests for the external process helpers in <see cref="ShellExtensions"/>.
/// </summary>
/// <remarks>
/// <c>dotnet</c> is used as the subject because it is guaranteed present wherever these tests run,
/// and an unrecognised flag gives a deterministic non-zero exit with standard error output.
/// </remarks>
public class ShellExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    private const string _host = "dotnet";
    private static readonly string[] _failingArguments = ["--a-flag-that-does-not-exist"];

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_MissingExecutable()
    {
        var result = await ShellExtensions.RunProcessWithStdinAsync(
            "an-executable-that-does-not-exist", [], [], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(-1, result.ExitCode);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(result.Error!.Length, result.ErrorLength);
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_CapturesStandardErrorText()
    {
        var result = await ShellExtensions.RunProcessWithStdinAsync(
            _host, _failingArguments, [], ProcessErrorCapture.Text, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(result.Error!.Length, result.ErrorLength);
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_LengthCaptureDiscardsErrorText()
    {
        var result = await ShellExtensions.RunProcessWithStdinAsync(
            _host, _failingArguments, [], ProcessErrorCapture.Length, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        //The count proves something was written without the text ever being retained.
        Assert.Null(result.Error);
        Assert.True(result.ErrorLength > 0);
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ShellExtensions.RunProcessWithStdinAsync(_host, _failingArguments, [], ProcessErrorCapture.Text, cts.Token));
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_LegacyOverloadKeepsItsShape()
    {
        //Deliberately exercising the obsolete overload; it still ships and must keep working.
#pragma warning disable CS0618
        var (output, error, exitCode) = await ShellExtensions.RunProcessWithStdinAsync(
            _host, "--a-flag-that-does-not-exist", [], TestContext.Current.CancellationToken);
#pragma warning restore CS0618

        Assert.NotEqual(0, exitCode);
        Assert.NotNull(error);
        Assert.NotNull(output);
    }

    [Fact, Trait("Category", "Shell")]
    public void RunProcess_MissingExecutable()
    {
        var (output, error, exitCode) = ShellExtensions.RunProcessDiagnostic("an-executable-that-does-not-exist");

        Assert.Equal(-1, exitCode);
        Assert.Null(output);
        Assert.NotNull(error);
        Assert.Null(ShellExtensions.RunProcess("an-executable-that-does-not-exist"));
    }

    [Fact, Trait("Category", "Shell")]
    public void RunProcess_Success_ReturnsTrimmedOutput()
    {
        var output = ShellExtensions.RunProcess(_host, "--version");

        Assert.False(string.IsNullOrWhiteSpace(output));
        Assert.Equal(output, output!.Trim());
    }

    [Fact, Trait("Category", "Shell")]
    public void RunProcess_NonZeroExit_ReturnsNull()
    {
        //RunProcess discards output on failure, which RunProcessDiagnostic deliberately does not.
        Assert.Null(ShellExtensions.RunProcess(_host, _failingArguments[0]));

        var (_, _, exitCode) = ShellExtensions.RunProcessDiagnostic(_host, _failingArguments[0]);
        Assert.NotEqual(0, exitCode);
    }

    [Fact, Trait("Category", "Shell")]
    public void RunProcessDiagnostic_Success_ReturnsOutputAndZeroExitCode()
    {
        var (output, _, exitCode) = ShellExtensions.RunProcessDiagnostic(_host, "--version");

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_Success_ReturnsStdoutAndZeroExitCode()
    {
        var result = await ShellExtensions.RunProcessWithStdinAsync(
            _host, ["--version"], [], ProcessErrorCapture.Text, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Success);
        Assert.NotEmpty(result.Output);
        Assert.Equal(0, result.ErrorLength);
    }

    //Proves the stdin write and the two output reads really do run concurrently; a sequential
    //  implementation deadlocks here once the payload exceeds the pipe buffer.
    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_RoundTripsPayloadLargerThanThePipeBuffer()
    {
        var (fileName, arguments) = EchoCommand();
        var payload = Encoding.ASCII.GetBytes(new string('x', 256 * 1024));

        var result = await ShellExtensions.RunProcessWithStdinAsync(
            fileName, arguments, payload, ProcessErrorCapture.Text, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Output.Length >= payload.Length,
            $"Expected at least {payload.Length} bytes back but got {result.Output.Length}.");
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_NullArguments_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ShellExtensions.RunProcessWithStdinAsync(_host, null!, [],
                ProcessErrorCapture.Text, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_NullStdin_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ShellExtensions.RunProcessWithStdinAsync(_host, [], null!,
                ProcessErrorCapture.Text, TestContext.Current.CancellationToken));
    }

    //The contract that makes the legacy overload obsolete: one array element is always one argument,
    //  where the string overload would split this into four. printf prints each argument on its own
    //  line, so the line count is the proof; there is no equivalent on Windows.
    [Fact, Trait("Category", "Shell")]
    public async Task RunProcessWithStdin_ArrayOverloadPreservesArgumentsContainingSpaces()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            "Needs printf to report each argument separately.");

        const string withSpaces = "a value with spaces";

        var result = await ShellExtensions.RunProcessWithStdinAsync(
            "printf", ["%s\n", withSpaces], [], ProcessErrorCapture.Text, TestContext.Current.CancellationToken);

        var lines = Encoding.UTF8.GetString(result.Output)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(0, result.ExitCode);
        Assert.Single(lines);
        Assert.Equal(withSpaces, lines[0]);
    }

    [Fact, Trait("Category", "Shell")]
    public void Bash_NullOrWhitespace_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => string.Empty.Bash());
        Assert.Throws<ArgumentNullException>(() => "   ".Bash());
    }

    [Fact, Trait("Category", "Shell")]
    public void Bash_OffLinux_Throws()
    {
        Assert.SkipWhen(RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            "Bash is supported on this platform, so the guard cannot be observed.");

        Assert.Throws<PlatformNotSupportedException>(() => "echo hello".Bash());
    }

    [Fact, Trait("Category", "Shell")]
    public void Bash_OnLinux_ReturnsStandardOutput()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Bash requires Linux.");

        Assert.Equal("hello", "echo hello".Bash());
    }

    //cat and more both copy stdin to stdout, which is all this needs from the host platform.
    private static (string fileName, string[] arguments) EchoCommand() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", ["/c", "more"])
            : ("cat", []);
}
