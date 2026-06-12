using System.Buffers;
using System.Text;

namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for <see cref="BufferExtensions.TryReadLine"/>.</summary>
public class BufferExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact, Trait("Category", "Parsing")]
    public void TryReadLine_SplitsLines_TrimmingCarriageReturns()
    {
        var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("line1\r\nline2\n"));
        var lines = new List<string>();

        while (buffer.TryReadLine(out var line))
            lines.Add(Encoding.UTF8.GetString(line.ToArray()));

        Assert.Equal(["line1", "line2"], lines);
    }

    [Fact, Trait("Category", "Parsing")]
    public void TryReadLine_NoNewline_ReturnsFalse()
    {
        var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("partial"));
        Assert.False(buffer.TryReadLine(out _));
    }
}
