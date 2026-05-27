namespace CasCap.Common.Converters;

/// <summary>Case-insensitive JSON string-to-enum converter using <see cref="Enum.Parse{TEnum}(string, bool)"/>.</summary>
public sealed class ParseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
#if NETSTANDARD2_0
        (TEnum)Enum.Parse(typeof(TEnum), reader.GetString()!, true);
#else
        Enum.Parse<TEnum>(reader.GetString()!, true);
#endif

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
