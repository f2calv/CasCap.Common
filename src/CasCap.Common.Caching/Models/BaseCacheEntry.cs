using MessagePack;
using System;
namespace CasCap.Models
{
    [MessagePackObject(true)]
    public class CacheItem<T>
    {
        public CacheItem(T data, DateTime? absexp, DateTime? sldexp)
        {
            this.data = data;
            this.absexp = absexp;
            this.sldexp = sldexp;
        }

        public T data;
        public DateTime? absexp;
        public DateTime? sldexp;
    }
}