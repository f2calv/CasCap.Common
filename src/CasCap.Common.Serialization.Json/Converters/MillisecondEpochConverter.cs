﻿namespace CasCap.Models;

public class MillisecondEpochConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return long.TryParse(reader.GetString(), out var t) ? t.FromUnixTimeMs() : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? dateTimeValue, JsonSerializerOptions options)
    {
        if (dateTimeValue.HasValue)
            writer.WriteRawValue(dateTimeValue.Value.ToUnixTimeMs().ToString());
    }
}
