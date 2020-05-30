using MessagePack;
namespace CasCap.Common.Extensions
{
    public static class Helpers
    {
        public static byte[] ToMessagePack<T>(this T data)
            => MessagePackSerializer.Serialize(data);

        //public static byte[] ToMessagePackLZ4<T>(this T data)
        //{
        //    var lz4Options = new MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        //    return MessagePackSerializer.Serialize(data, lz4Options);
        //}

        public static T FromMessagePack<T>(this byte[] bytes)
            => MessagePackSerializer.Deserialize<T>(bytes/*, MessagePack.Resolvers.StandardResolver.Instance*/);

        //public static T FromMessagePackLZ4<T>(this byte[] bytes)
        //{
        //    var lz4Options = new MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        //    return MessagePackSerializer.Deserialize<T>(bytes, lz4Options);
        //}
    }
}