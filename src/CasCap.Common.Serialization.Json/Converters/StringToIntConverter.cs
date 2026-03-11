namespace CasCap.Common.Converters;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonConverter{T}"/> that converts a JSON string token
/// to and from a nullable <see cref="int"/>.
/// </summary>
public class StringToIntConverter : JsonConverter<int?>
{
    /// <inheritdoc/>
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return int.TryParse(reader.GetString(), out var t) ? t : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, int? intValue, JsonSerializerOptions options)
    {
        if (intValue.HasValue)
            writer.WriteRawValue(intValue.Value.ToString());
    }
}
