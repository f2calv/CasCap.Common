namespace CasCap.Common.Models;

/// <summary>
/// A fixed-size queue that automatically dequeues the oldest items when the limit is reached.
/// </summary>
/// <remarks>See <see href="https://stackoverflow.com/questions/5852863/fixed-size-queue-which-automatically-dequeues-old-values-upon-new-enques"/>.</remarks>
/// <typeparam name="T">The type of elements in the queue.</typeparam>
[Serializable]
[DebuggerDisplay("Count = {" + nameof(Count) + "}, Limit = {" + nameof(Limit) + "}")]
public class FixedSizedQueue<T> : IReadOnlyCollection<T>
{
    private readonly Queue<T> _queue = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public int Count { get { lock (_lock) { return _queue.Count; } } }

    /// <summary>
    /// The maximum number of items the queue can hold.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedSizedQueue{T}"/> class with the specified limit.
    /// </summary>
    /// <param name="limit">The maximum number of items the queue can hold.</param>
    public FixedSizedQueue(int limit)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
#endif
        Limit = limit;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedSizedQueue{T}"/> class with the specified limit and initial collection.
    /// </summary>
    /// <param name="limit">The maximum number of items the queue can hold.</param>
    /// <param name="collection">The initial collection to populate the queue.</param>
    public FixedSizedQueue(int limit, IEnumerable<T> collection)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
#endif
        if (collection is null || !collection.Any())
            throw new ArgumentException("Can not initialize the Queue with a null or empty collection", nameof(collection));

        _queue = new Queue<T>(collection);
        Limit = limit;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedSizedQueue{T}"/> class from the specified collection, using its count as the limit.
    /// </summary>
    /// <param name="collection">The initial collection to populate the queue.</param>
    public FixedSizedQueue(IEnumerable<T> collection)
    {
        if (collection is null || !collection.Any())
            throw new ArgumentException("Can not initialize the Queue with a null or empty collection", nameof(collection));

        _queue = new Queue<T>(collection);
        Limit = _queue.Count;
    }

    /// <summary>
    /// Adds an item to the queue, dequeuing the oldest items if the limit is exceeded.
    /// </summary>
    /// <param name="obj">The item to enqueue.</param>
    public void Enqueue(T obj)
    {
        lock (_lock)
        {
            _queue.Enqueue(obj);

            while (_queue.Count > Limit)
                _queue.Dequeue();
        }
    }

    /// <summary>
    /// Removes all items from the queue.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
            _queue.Clear();
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
            return new List<T>(_queue).GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
