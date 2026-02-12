namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods to assist with common Caching tasks.
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Calculates the relative expiration <see cref="TimeSpan"/> from an integer.
    /// </summary>
    public static TimeSpan? GetExpirationFromSeconds(this int ttl)
    {
        TimeSpan? expiry = null;
        if (ttl > -1)//if -1, the key does not have expiry timeout.
            expiry = TimeSpan.FromSeconds(ttl);
        return expiry;
    }

    /// <summary>
    /// Validates that sliding and absolute expirations are not both set and that absolute expiration is not in the past.
    /// </summary>
    internal static void ValidateExpirations(string key, TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
    {
        if (slidingExpiration.HasValue && absoluteExpiration.HasValue)
            throw new NotSupportedException($"{nameof(slidingExpiration)} and {nameof(absoluteExpiration)} are both requested for key {key}!");
        if (absoluteExpiration.HasValue && absoluteExpiration.Value < DateTime.UtcNow)
            throw new NotSupportedException($"{nameof(absoluteExpiration)} is requested for key {key} but {absoluteExpiration} is already expired!");
    }

    /// <summary>
    /// Serializes an object using the specified <see cref="SerializationType"/>.
    /// </summary>
    internal static byte[] SerializeToBytes<T>(this T obj, SerializationType serializationType) where T : class
        => serializationType switch
        {
            SerializationType.Json => System.Text.Encoding.UTF8.GetBytes(obj.ToJson()),
            SerializationType.MessagePack => obj.ToMessagePack(),
            _ => throw new NotSupportedException($"{nameof(serializationType)} {serializationType} is not supported!")
        };

    /// <summary>
    /// Deserializes an object from a byte array using the specified <see cref="SerializationType"/>.
    /// </summary>
    internal static T? DeserializeFromBytes<T>(this byte[] bytes, SerializationType serializationType)
        => serializationType switch
        {
            SerializationType.Json => System.Text.Encoding.UTF8.GetString(bytes).FromJson<T>(),
            SerializationType.MessagePack => bytes.FromMessagePack<T>(),
            _ => throw new NotSupportedException($"{nameof(serializationType)} {serializationType} is not supported!")
        };

    /// <summary>
    /// Deserializes an object from a string using the specified <see cref="SerializationType"/>.
    /// </summary>
    internal static T? DeserializeFromString<T>(this string value, SerializationType serializationType)
        => serializationType switch
        {
            SerializationType.Json => value.FromJson<T>(),
            SerializationType.MessagePack => System.Convert.FromBase64String(value).FromMessagePack<T>(),
            _ => throw new NotSupportedException($"{nameof(serializationType)} {serializationType} is not supported!")
        };
}
