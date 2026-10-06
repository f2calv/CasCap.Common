#if NET8_0_OR_GREATER
namespace CasCap.Common.Converters;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonConverter{T}"/> that converts a millisecond Unix epoch
/// value (as a numeric or string token) to and from a nullable <see cref="DateTime"/>.
/// </summary>
public sealed class MillisecondEpochConverter : JsonConverter<DateTime?>
{
    /// <inheritdoc/>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null
            ? null
            : reader.TokenType == JsonTokenType.Number
            ? reader.TryGetInt64(out var n) ? n.FromUnixTimeMs() : null
            : long.TryParse(reader.GetString(), out var t) ? t.FromUnixTimeMs() : null;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime? dateTimeValue, JsonSerializerOptions options)
    {
        if (dateTimeValue.HasValue)
            writer.WriteRawValue(dateTimeValue.Value.ToUnixTimeMs().ToString());
    }
}
#endif
