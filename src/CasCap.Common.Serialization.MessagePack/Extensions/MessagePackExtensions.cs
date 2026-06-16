using Microsoft.Extensions.Logging;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for MessagePack serialization and deserialization.</summary>
public static class MessagePackExtensions
{
    private static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(MessagePackExtensions));

    /// <summary>Serializes the specified object to a MessagePack byte array.</summary>
    public static byte[] ToMessagePack<T>(this T data)
    {
        data = data ?? throw new ArgumentNullException(paramName: nameof(data));
        try
        {
            var bytes = MessagePackSerializer.Serialize(data);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failed", nameof(MessagePackExtensions), nameof(MessagePackSerializer.Serialize));
            throw;
        }
    }

    /// <summary>Deserializes a MessagePack byte array to an instance of <typeparamref name="T"/>.</summary>
    public static T FromMessagePack<T>(this byte[] bytes)
    {
        bytes = bytes ?? throw new ArgumentNullException(paramName: nameof(bytes));
        ReadOnlyMemory<byte> buffer = bytes;
        return buffer.FromMessagePack<T>();
    }

    /// <summary>Deserializes a MessagePack buffer to an instance of <typeparamref name="T"/>.</summary>
    /// <remarks>
    /// Buffer overload for transport-neutral callers (e.g. snapshot/gRPC payloads) that already hold a
    /// <see cref="ReadOnlyMemory{T}"/> slice and want to avoid an intermediate <see cref="byte"/> array copy.
    /// </remarks>
    public static T FromMessagePack<T>(this ReadOnlyMemory<byte> bytes)
    {
        try
        {
            T obj = MessagePackSerializer.Deserialize<T>(bytes);
            return obj;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failed", nameof(MessagePackExtensions), nameof(MessagePackSerializer.Deserialize));
            throw;
        }
    }
}
