namespace BetterBeads.Data;

/// <summary>Bounded cache; resources referenced by the current draw batch retire after it ends.</summary>
internal sealed class DeferredCache<TKey,TValue> where TKey:notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey,TValue> entries=new();
    private readonly Queue<TKey> order=new();
    private readonly List<TValue> retired=new();
    public DeferredCache(int capacity)=>this.capacity=Math.Max(1,capacity);
    public TValue Get(TKey key,Func<TValue> create)
    {
        if(entries.TryGetValue(key,out var value))return value;
        return Add(key,create());
    }
    public TValue Get(TKey key,Func<TKey,TValue> create)
    {
        if(entries.TryGetValue(key,out var value))return value;
        return Add(key,create(key));
    }
    private TValue Add(TKey key,TValue value)
    {
        entries.Add(key,value);order.Enqueue(key);
        while(entries.Count>capacity){var old=order.Dequeue();retired.Add(entries[old]);entries.Remove(old);}
        return value;
    }
    public void Clear(){retired.AddRange(entries.Values);entries.Clear();order.Clear();}
    public void ReleaseRetired(Action<TValue> dispose){foreach(var value in retired)dispose(value);retired.Clear();}
}
