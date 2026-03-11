#if NET8_0_OR_GREATER
namespace CasCap.Common.Converters;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonConverter{T}"/> that converts a millisecond Unix epoch
/// value (as a string token) to and from a nullable <see cref="DateTime"/>.
/// </summary>
public class MillisecondEpochConverter : JsonConverter<DateTime?>
{
    /// <inheritdoc/>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return long.TryParse(reader.GetString(), out var t) ? t.FromUnixTimeMs() : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime? dateTimeValue, JsonSerializerOptions options)
    {
        if (dateTimeValue.HasValue)
            writer.WriteRawValue(dateTimeValue.Value.ToUnixTimeMs().ToString());
    }
}
#endif
