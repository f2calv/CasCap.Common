//namespace CasCap.Models;

//public class MicrosecondEpochConverter : DateTimeConverterBase
//{
//    static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

//    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//    {
//        if (reader.Value is null) { return null; }
//        var t = long.Parse((string)reader.Value);
//        return _epoch.AddMilliseconds(t / 1000d);
//    }

//    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//    {
//        writer.WriteRawValue(((DateTime)value - _epoch).TotalMilliseconds + "000");
//    }
//}
