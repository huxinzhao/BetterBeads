#if BEADS_LITE
namespace BetterBeads.Data;

internal sealed class DraftRecoveryEntry
{
    public int Version {get;set;}=1;
    public Blueprint Draft {get;set;}=new();
    public Blueprint Baseline {get;set;}=new();
    public string View {get;set;}="front";
    public bool UnsavedIdentity {get;set;}
    public bool Valid=>Version==1&&Draft is not null&&Baseline is not null
        &&SimpleCrafting.Supported(Draft)&&SimpleCrafting.Supported(Baseline)
        &&Draft.Id==Baseline.Id&&Draft.Views.ContainsKey(View);
}

public sealed partial class EditorDocument
{
    internal DraftRecoveryEntry RecoverySnapshot()=>new(){Draft=draft.Copy(),Baseline=savedDesign.Copy(),View=View,UnsavedIdentity=unsavedIdentity};
    internal bool RestoreRecovery(DraftRecoveryEntry record)
    {
        if(IsDirty||!record.Valid)return false;
        EndStroke();draft=record.Draft.Copy();savedDesign=record.Baseline.Copy();View=record.View;
        saved=ContentKey(savedDesign);currentContent=null;unsavedIdentity=record.UnsavedIdentity;
        undo.Clear();redo.Clear();ChangeVersion++;return true;
    }
    internal bool TransformRegion(UiRect region,int dx,int dy,bool copy)
    {
        var grid=draft.Views[View];
        if(!PatternTransform.CanPlace(grid,region,dx,dy)||dx==0&&dy==0)return false;
        return Change(()=>PatternTransform.Apply(grid,region,dx,dy,copy));
    }
}

internal static class PatternTransform
{
    internal static bool Valid(BeadGrid grid,UiRect r)=>r.Width>0&&r.Height>0&&r.X>=0&&r.Y>=0&&r.Right<=grid.Width&&r.Bottom<=grid.Height;
    internal static bool CanPlace(BeadGrid grid,UiRect r,int dx,int dy)=>Valid(grid,r)
        &&(long)r.X+dx>=0&&(long)r.Y+dy>=0&&(long)r.Right+dx<=grid.Width&&(long)r.Bottom+dy<=grid.Height;
    internal static int Overwritten(BeadGrid grid,UiRect r,int dx,int dy,bool copy)
    {
        if(!CanPlace(grid,r,dx,dy))return 0;
        int count=0;
        for(int y=r.Y;y<r.Bottom;y++)for(int x=r.X;x<r.Right;x++)
            if(grid.Cells[y*grid.Width+x] is not null&&(copy||!r.Contains(x+dx,y+dy))
                &&grid.Cells[(y+dy)*grid.Width+x+dx] is not null)count++;
        return count;
    }
    internal static void Apply(BeadGrid grid,UiRect r,int dx,int dy,bool copy)
    {
        if(!CanPlace(grid,r,dx,dy))throw new ArgumentException("Pattern destination is outside the canvas");
        var original=grid.Copy();
        if(!copy)for(int y=r.Y;y<r.Bottom;y++)for(int x=r.X;x<r.Right;x++)grid.Cells[y*grid.Width+x]=null;
        for(int y=r.Y;y<r.Bottom;y++)for(int x=r.X;x<r.Right;x++)
            if(original.Cells[y*grid.Width+x] is {} bead)grid.Cells[(y+dy)*grid.Width+x+dx]=bead.Copy();
    }
}

internal sealed record PatternMoveLayout(UiRect Dialog,UiRect Close,UiRect Preview,UiRect Description,UiRect Up,UiRect Left,UiRect Down,UiRect Right,UiRect Mode,UiRect Copy,UiRect Apply,UiRect Cancel)
{
    internal static PatternMoveLayout Calculate(UiRect frame)
    {
        int w=Math.Min(560,frame.Width-16),h=Math.Min(404,frame.Height-16);
        var r=new UiRect(frame.X+(frame.Width-w)/2,frame.Y+(frame.Height-h)/2,w,h);
        int row=r.Bottom-116,mode=(w-248)/2;
        return new(r,new(r.Right-56,r.Y+16,40,40),new(r.X+16,r.Y+64,w-32,Math.Max(24,h-268)),new(r.X+16,r.Bottom-196,w-32,72),
            new(r.X+68,row,44,44),new(r.X+16,row,44,44),new(r.X+120,row,44,44),new(r.X+172,row,44,44),
            new(r.X+224,row,mode,44),new(r.X+232+mode,row,w-248-mode,44),
            new(r.Right-112,r.Bottom-64,96,48),new(r.Right-216,r.Bottom-64,96,48));
    }
}
internal sealed record RecoveryLayout(UiRect Dialog,UiRect Close,UiRect Preview,UiRect Name,UiRect Description,UiRect Delete,UiRect Restore)
{
    internal static RecoveryLayout Calculate(UiRect frame)
    {
        int w=Math.Min(560,frame.Width-16),h=Math.Min(348,frame.Height-16);
        var r=new UiRect(frame.X+(frame.Width-w)/2,frame.Y+(frame.Height-h)/2,w,h);
        int side=Math.Min(144,h-152),bw=(w-40)/2;
        return new(r,new(r.Right-56,r.Y+16,40,40),new(r.X+16,r.Y+64,side,side),
            new(r.X+16,r.Y+72+side,side,48),new(r.X+side+32,r.Y+64,w-side-48,h-144),
            new(r.X+16,r.Bottom-64,bw,48),new(r.X+24+bw,r.Bottom-64,bw,48));
    }
}
internal sealed record WeaponExplanationLayout(UiRect Body,UiRect Picture,UiRect Legend,UiRect Text,UiRect Previous,UiRect Next,UiRect Page)
{
    internal static WeaponExplanationLayout Calculate(ScenePreviewLayout scene)
    {
        var r=new UiRect(scene.Stage.X,scene.Stage.Y,scene.Stage.Width,scene.Toggle.Y-scene.Stage.Y-16);
        int side=Math.Min(160,Math.Min((r.Width-40)/3,r.Height-88)),x=r.X+side+24,tw=r.Right-x-8;
        return new(r,new(r.X+8,r.Y+8,side,side),new(r.X+8,r.Y+side+16,side,r.Height-side-24),
            new(x,r.Y+8,tw,r.Height-68),new(x,r.Bottom-52,44,44),new(r.Right-52,r.Bottom-52,44,44),
            new(x+52,r.Bottom-52,tw-104,44));
    }
}
internal enum GallerySiteState {Ready,Unsupported,Unavailable}
internal static class GiftFeedback
{
    internal static string Key(GallerySiteState site,int hearts)=>site switch
    {
        GallerySiteState.Unsupported=>"gift.display-unsupported",
        GallerySiteState.Unavailable=>"gift.display-unavailable",
        _=>hearts<GiftGallery.RequiredHearts?"gift.display-hearts":"gift.display-tomorrow"
    };
}
#endif
