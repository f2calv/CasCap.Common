namespace CasCap.Models;

//https://stackoverflow.com/questions/5852863/fixed-size-queue-which-automatically-dequeues-old-values-upon-new-enques
[Serializable]
[DebuggerDisplay("Count = {" + nameof(Count) + "}, Limit = {" + nameof(Limit) + "}")]
public class FixedSizedQueue<T> : IReadOnlyCollection<T>
{
    private readonly Queue<T> _queue = new();
    private readonly object _lock = new();

    public int Count { get { lock (_lock) { return _queue.Count; } } }
    public int Limit { get; }

    public FixedSizedQueue(int limit)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
#endif
        Limit = limit;
    }

    public FixedSizedQueue(int limit, IEnumerable<T> collection)//own addition
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
#endif
        if (collection is null || !collection.Any())
            throw new ArgumentException("Can not initialize the Queue with a null or empty collection", nameof(collection));

        _queue = new Queue<T>(collection);
        Limit = limit;
    }

    public FixedSizedQueue(IEnumerable<T> collection)
    {
        if (collection is null || !collection.Any())
            throw new ArgumentException("Can not initialize the Queue with a null or empty collection", nameof(collection));

        _queue = new Queue<T>(collection);
        Limit = _queue.Count;
    }

    public void Enqueue(T obj)
    {
        lock (_lock)
        {
            _queue.Enqueue(obj);

            while (_queue.Count > Limit)
                _queue.Dequeue();
        }
    }

    public void Clear()
    {
        lock (_lock)
            _queue.Clear();
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
            return new List<T>(_queue).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
