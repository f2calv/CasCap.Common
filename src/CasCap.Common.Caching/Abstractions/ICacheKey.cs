namespace CasCap.Abstractions;

public interface ICacheKey<T>
{
    string CacheKey { get; }
}
