namespace CasCap.Common.Converters;

/// <summary>Case-insensitive JSON string-to-enum converter using <see cref="Enum.Parse{TEnum}(string, bool)"/>.</summary>
public class ParseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Enum.Parse<TEnum>(reader.GetString()!, true);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
