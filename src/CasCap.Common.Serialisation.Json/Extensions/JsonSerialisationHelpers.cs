using Microsoft.Extensions.Logging;

namespace CasCap.Common.Extensions;

public static class JsonSerializationHelpers
{
    static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(JsonSerializationHelpers));

    //https://stackoverflow.com/questions/24066400/checking-for-empty-or-null-jtoken-in-a-jobject/24067483#24067483
    public static bool IsNullOrEmpty(this JToken token)
    {
        return token is null ||
               (token.Type == JTokenType.Array && !token.HasValues) ||
               (token.Type == JTokenType.Object && !token.HasValues) ||
               (token.Type == JTokenType.String && token.ToString() == string.Empty) ||
               token.Type == JTokenType.Null;
    }

    public static string ToJSON(this object obj) => obj.ToJSON(formatting: Formatting.None, settings: null);

    public static string ToJSON(this object obj, Formatting formatting) => obj.ToJSON(formatting, settings: null);

    public static string ToJSON(this object obj, JsonSerializerSettings? settings) => obj.ToJSON(Formatting.None, settings);

    public static string ToJSON(this object obj, Formatting formatting = Formatting.None, JsonSerializerSettings? settings = null)
    {
        try
        {
            return JsonConvert.SerializeObject(obj, formatting, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(JsonConvert.SerializeObject)} failed");
            throw;
        }
    }

    //public static void ToJSON(this object value, Stream s)
    //{
    //    using (var writer = new StreamWriter(s))
    //    using (var jsonWriter = new JsonTextWriter(writer))
    //    {
    //        var ser = new JsonSerializer();
    //        ser.Serialize(jsonWriter, value);
    //        jsonWriter.Flush();
    //    }
    //}

    public static T? FromJSON<T>(this string json, JsonSerializerSettings? settings = null)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(JsonConvert.DeserializeObject)} failed");
            throw;
        }
    }

    //not sure why I added this? Maybe to deserialize massive json files?
    //public static T FromJSONv2<T>(this string json)
    //{
    //    T output = default;
    //    using (var sr = new StringReader(json))
    //    using (var jtr = new JsonTextReader(sr))
    //    {
    //        var ser = JsonSerializer.Create();
    //        output = ser.Deserialize<T>(jtr);
    //    }
    //    return output;
    //}

    //public static T FromJSON<T>(this Stream s)
    //{
    //    using (var reader = new StreamReader(s))
    //    using (var jsonReader = new JsonTextReader(reader))
    //    {
    //        var ser = new JsonSerializer();
    //        return ser.Deserialize<T>(jsonReader);
    //    }
    //}
}
