using Microsoft.Extensions.Caching.Memory;
namespace CasCap.Models;

public class PostEvictionEventArgs : EventArgs
{
    public PostEvictionEventArgs(object key, object value, EvictionReason reason, object state)
    {
        this.key = key;
        this.value = value;
        this.reason = reason;
        this.state = state;
    }

    public object key { get; set; }
    public object value { get; set; }
    public EvictionReason reason { get; set; }
    public object state { get; set; }
}