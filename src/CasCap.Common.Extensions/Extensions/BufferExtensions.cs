#if NET8_0_OR_GREATER
using System.Buffers;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for <see cref="ReadOnlySequence{T}"/> and buffer operations.</summary>
public static class BufferExtensions
{
    /// <summary>Attempts to read a single line from a <see cref="ReadOnlySequence{T}"/> buffer, advancing past the newline.</summary>
    /// <param name="buffer">The buffer to read from; advanced past the consumed line on success.</param>
    /// <param name="line">The line content excluding the trailing newline (and optional \r).</param>
    /// <returns><c>true</c> if a complete line was found; otherwise <c>false</c>.</returns>
    public static bool TryReadLine(ref this ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        var position = buffer.PositionOf((byte)'\n');
        if (position is null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value);
        // Trim trailing \r if present
        if (line.Length > 0)
        {
            var lastPosition = line.GetPosition(line.Length - 1);
            if (line.Slice(lastPosition).FirstSpan[0] == (byte)'\r')
                line = line.Slice(0, lastPosition);
        }

        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }
}
#endif
