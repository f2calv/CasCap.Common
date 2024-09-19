namespace CasCap.Common.Extensions;

public static class JsonSerializationHelpers
{
    static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(JsonSerializationHelpers));

    public static string ToJson(this object obj) => obj.ToJson(options: null);

    public static string ToJson(this object obj, JsonSerializerOptions? options = null)
    {
        try
        {
            return JsonSerializer.Serialize(obj, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(JsonSerializer.Serialize)} failed");
            throw;
        }
    }

    public static T? FromJson<T>(this string json, JsonSerializerOptions? options = null)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(JsonSerializer.Deserialize)} failed");
            throw;
        }
    }
}
