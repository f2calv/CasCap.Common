using CasCap.Common.Models;

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
        var (output, error, exitCode) = await ShellExtensions.RunProcessWithStdinAsync(
            _host, "--a-flag-that-does-not-exist", [], TestContext.Current.CancellationToken);

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
}
