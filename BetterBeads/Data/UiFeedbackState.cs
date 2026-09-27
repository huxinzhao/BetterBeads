namespace BetterBeads.Data;

public enum HoverFeedback { None, Enter, Leave }

/// <summary>Only pointer movement between active hit targets makes hover sounds.</summary>
public sealed class UiFeedbackState
{
    public UiRect? Target {get;private set;}
    private bool initialized;
    private int previousX,previousY;
    private double nextHover;
    private string? targetIdentity;
    public HoverFeedback Observe(UiRect? target,int x,int y,double now,string? identity=null)
    {
        bool moved=initialized&&(x!=previousX||y!=previousY),changed=Target!=target||targetIdentity!=identity;
        targetIdentity=identity;
        var before=Target;Target=target;previousX=x;previousY=y;initialized=true;
        if(!moved||!changed||now<nextHover)return HoverFeedback.None;
        nextHover=now+0.08;
        return target is not null?HoverFeedback.Enter:before is not null?HoverFeedback.Leave:HoverFeedback.None;
    }
    public void Reset(){Target=null;targetIdentity=null;initialized=false;nextHover=0;}
}
