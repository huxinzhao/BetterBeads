namespace BetterBeads.Data;

// The owner increments its version whenever the source changes. Callers receive a detached copy.
internal sealed class VersionedSnapshotCache<T> where T:class
{
    private long version=long.MinValue;
    private T? snapshot;

    public T Get(long currentVersion,Func<T> capture)
    {
        if(snapshot is null || version!=currentVersion)
        {
            snapshot=capture();
            version=currentVersion;
        }
        return snapshot;
    }
}
