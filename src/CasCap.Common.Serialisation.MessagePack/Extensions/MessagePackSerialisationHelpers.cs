using Microsoft.Extensions.Logging;
using System;

namespace CasCap.Common.Extensions;

public static class MessagePackSerialisationHelpers
{
    static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(MessagePackSerialisationHelpers));

    public static byte[] ToMessagePack<T>(this T data)
    {
        try
        {
            return MessagePackSerializer.Serialize(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(MessagePackSerializer.Serialize)} failed");
            throw;
        }
    }

    //public static byte[] ToMessagePackLZ4<T>(this T data)
    //{
    //    var lz4Options = new MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
    //    return MessagePackSerializer.Serialize(data, lz4Options);
    //}

    public static T FromMessagePack<T>(this byte[] bytes/*, MessagePack.Resolvers.StandardResolver.Instance*/)
    {
        try
        {
            return MessagePackSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(MessagePackSerializer.Deserialize)} failed");
            throw;
        }
    }

    //public static T FromMessagePackLZ4<T>(this byte[] bytes)
    //{
    //    var lz4Options = new MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
    //    return MessagePackSerializer.Deserialize<T>(bytes, lz4Options);
    //}
}
