using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
namespace CasCap.Common.Extensions
{
    public static class JsonSerialisationHelpers
    {
        //https://stackoverflow.com/questions/24066400/checking-for-empty-or-null-jtoken-in-a-jobject/24067483#24067483
        public static bool IsNullOrEmpty(this JToken token)
        {
            return token is null ||
                   (token.Type == JTokenType.Array && !token.HasValues) ||
                   (token.Type == JTokenType.Object && !token.HasValues) ||
                   (token.Type == JTokenType.String && token.ToString() == string.Empty) ||
                   token.Type == JTokenType.Null;
        }

        public static string ToJSON(this object obj) => JsonConvert.SerializeObject(obj, Formatting.None);

        public static string ToJSON(this object obj, Formatting formatting) => JsonConvert.SerializeObject(obj, formatting);

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

        public static T? FromJSON<T>(this string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }
        }

        //not sure why I added this? Maybe to deserialise massive json files?
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
}