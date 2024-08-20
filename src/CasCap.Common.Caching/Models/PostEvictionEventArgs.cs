namespace CasCap.Models;

public class PostEvictionEventArgs(object key, object value, EvictionReason reason, object state) : EventArgs
{
    public object key { get; set; } = key;
    public object value { get; set; } = value;
    public EvictionReason reason { get; set; } = reason;
    public object state { get; set; } = state;
}
