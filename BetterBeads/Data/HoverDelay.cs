namespace BetterBeads.Data;

internal sealed class HoverDelay
{
    private string? identity;
    private double since;
    public bool Ready(string? target,double now)
    {
        if(identity!=target){identity=target;since=now;}
        return target is not null&&now-since>=0.35;
    }
    public void Reset(){identity=null;since=0;}
}
