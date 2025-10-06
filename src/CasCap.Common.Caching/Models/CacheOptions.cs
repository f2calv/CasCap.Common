namespace CasCap.Models;

public record CacheOptions
{
    public bool IsEnabled { get; init; } = true;

    public int DatabaseId { get; init; } = 0;

    public bool ClearOnStartup { get; init; } = false;

    public SerializationType SerializationType { get; init; } = SerializationType.MessagePack;
}
