namespace CasCap.Common.Converters;

public class StringToIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return int.TryParse(reader.GetString(), out var t) ? t : null;
    }

    public override void Write(Utf8JsonWriter writer, int? intValue, JsonSerializerOptions options)
    {
        if (intValue.HasValue)
            writer.WriteRawValue(intValue.Value.ToString());
    }
}
