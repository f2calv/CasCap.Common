using CasCap.Common.Models;

namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for <see cref="FixedSizedQueue{T}"/>.</summary>
public class FixedSizedQueueTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact, Trait("Category", "Collections")]
    public void Enqueue_EvictsOldestBeyondLimit()
    {
        var queue = new FixedSizedQueue<int>(3);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        queue.Enqueue(4);

        Assert.Equal(3, queue.Count);
        Assert.Equal([2, 3, 4], queue);
    }

    [Fact, Trait("Category", "Collections")]
    public void Clear_RemovesAllItems()
    {
        var queue = new FixedSizedQueue<int>(3);
        queue.Enqueue(1);
        queue.Clear();
        Assert.Empty(queue);
    }

    [Fact, Trait("Category", "Collections")]
    public void Ctor_FromCollection_SetsLimitToCount()
    {
        var queue = new FixedSizedQueue<int>([1, 2, 3]);
        Assert.Equal(3, queue.Limit);
        Assert.Equal(3, queue.Count);
    }

    [Fact, Trait("Category", "Collections")]
    public void Ctor_NullOrEmptyCollection_Throws()
        => Assert.Throws<ArgumentException>(() => new FixedSizedQueue<int>([]));
}
