﻿using Microsoft.Extensions.Logging;

namespace CasCap.Common.Extensions;

public static class MessagePackSerializationHelpers
{
    private static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(MessagePackSerializationHelpers));

    public static byte[] ToMessagePack<T>(this T data)
    {
        data = data ?? throw new ArgumentNullException(paramName: nameof(data));
        try
        {
            var bytes = MessagePackSerializer.Serialize(data);
            //_logger.LogTrace("{className} serialized object {typeof} into {count} bytes",
            //    nameof(MessagePackSerializationHelpers), typeof(T), bytes.Length);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{className} {methodName} failed", nameof(MessagePackSerializationHelpers), nameof(MessagePackSerializer.Serialize));
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
        bytes = bytes ?? throw new ArgumentNullException(paramName: nameof(bytes));
        try
        {
            T obj = MessagePackSerializer.Deserialize<T>(bytes);
            //_logger.LogTrace("{className} deserialized object {typeof} from {count} bytes",
            //    nameof(MessagePackSerializationHelpers), typeof(T), bytes.Length);
            return obj;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{className} {methodName} failed", nameof(MessagePackSerializationHelpers), nameof(MessagePackSerializer.Deserialize));
            throw;
        }
    }

    //public static T FromMessagePackLZ4<T>(this byte[] bytes)
    //{
    //    var lz4Options = new MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
    //    return MessagePackSerializer.Deserialize<T>(bytes, lz4Options);
    //}
}
