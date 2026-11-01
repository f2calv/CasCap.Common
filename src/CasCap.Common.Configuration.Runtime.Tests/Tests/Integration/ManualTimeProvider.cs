namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

public sealed class ManualTimeProvider : TimeProvider
{
    private long _utcTicks = DateTimeOffset.UtcNow.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan duration) => Interlocked.Add(ref _utcTicks, duration.Ticks);
}
