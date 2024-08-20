namespace CasCap.Models;

public class ColumnCleaner : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(DateTime);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        //if (reader.Value is null) return null;
        var sDate = (string?)reader.Value;
        if (sDate == "N/A") return null;
        if (!DateTime.TryParse(sDate, out var time))
            throw new Exception($"{nameof(ReadJson)} unable to parse '{nameof(time)}'??");
        return time;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => throw new NotImplementedException($"{nameof(ColumnCleaner)}.{nameof(WriteJson)} not implemented");
}
//todo: Uri convertor
