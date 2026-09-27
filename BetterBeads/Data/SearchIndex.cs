namespace BetterBeads.Data;

/// <summary>Read detached records at explicit refresh boundaries; typing only filters that snapshot.</summary>
internal sealed class SearchIndex<T>
{
    private readonly Func<T[]> read;
    private readonly Func<T,string> name;
    private T[] all=Array.Empty<T>(),filtered=Array.Empty<T>();
    private string? query;
    private bool dirty=true;
    public SearchIndex(Func<T[]> read,Func<T,string> name){this.read=read;this.name=name;}
    public void Invalidate()=>dirty=true;
    public T[] Find(string search)
    {
        if(dirty){all=read();dirty=false;query=null;}
        if(query!=search){filtered=search.Length==0?all:all.Where(item=>name(item).Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();query=search;}
        return filtered;
    }
}
