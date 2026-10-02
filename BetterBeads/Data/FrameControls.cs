namespace BetterBeads.Data;

/// <summary>Hit targets belong to the last rendered state and are consumed before an action changes it.</summary>
public sealed class FrameControls
{
    private readonly List<(UiRect Rect,Action Action)> entries=new();
    public bool IsReady=>entries.Count>0;
    public IReadOnlyList<UiRect> Targets=>entries.Select(e=>e.Rect).ToArray();
    public void Add((UiRect Rect,Action Action) entry)=>entries.Add(entry);
    public void Clear()=>entries.Clear();
    public UiRect? HitTest(int x,int y)
    {
        for(int i=entries.Count-1;i>=0;i--)if(entries[i].Rect.Contains(x,y))return entries[i].Rect;
        return null;
    }
    public bool TryInvoke(int x,int y)
    {
        for(int i=entries.Count-1;i>=0;i--)
        {
            if(!entries[i].Rect.Contains(x,y))continue;
            var action=entries[i].Action;
            Clear();
            action();
            return true;
        }
        return false;
    }
}
