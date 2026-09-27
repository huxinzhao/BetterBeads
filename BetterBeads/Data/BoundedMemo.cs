namespace BetterBeads.Data;

/// <summary>Bounded memoization for disposable-free derived values. Factories receive the key to avoid hit-path closures.</summary>
internal sealed class BoundedMemo<TKey,TValue> where TKey:notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey,TValue> entries=new();
    private readonly Queue<TKey> order=new();
    public BoundedMemo(int capacity)=>this.capacity=Math.Max(1,capacity);
    public TValue Get(TKey key,Func<TKey,TValue> create)
    {
        if(entries.TryGetValue(key,out var value))return value;
        value=create(key);
        if(entries.Count==capacity)entries.Remove(order.Dequeue());
        entries.Add(key,value);order.Enqueue(key);return value;
    }
    public void Clear(){entries.Clear();order.Clear();}
}
