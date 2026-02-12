namespace CasCap.Models;

public class PostEvictionEventArgs(object key, object value, EvictionReason reason, object state) : EventArgs
{
    public object Key { get; } = key;
    public object Value { get; } = value;
    public EvictionReason Reason { get; } = reason;
    public object State { get; } = state;
}
