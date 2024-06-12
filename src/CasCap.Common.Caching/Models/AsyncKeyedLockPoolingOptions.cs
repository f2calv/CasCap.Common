namespace CasCap.Models;

public class AsyncKeyedLockPoolingOptions
{
    public const string SectionKey = $"{nameof(CasCap)}:{nameof(AsyncKeyedLockPoolingOptions)}";

    public int PoolSize { get; set; } = 20;
    public int PoolInitialFill { get; set; } = 1;
}
