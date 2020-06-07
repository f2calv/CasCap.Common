namespace CasCap.Models
{
    public class CachingConfig
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public string redisConnectionString { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public int MemoryCacheSizeLimit { get; set; }
    }
}