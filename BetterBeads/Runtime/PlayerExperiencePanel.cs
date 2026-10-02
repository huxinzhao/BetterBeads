#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private DraftRecoveryEntry? recoveryPrompt;
    private Func<bool,bool>? resolveRecovery;
    private Action? deferRecovery;
    private bool recoveryDeleteConfirm;
    private string recoveryError="";
    private bool patternSelecting,patternMoveOpen,patternCopy;
    private Point? patternAnchor;
    private Point patternEnd;
    private UiRect patternRegion;
    private int patternDx,patternDy,dialogPage;
    private string dialogIdentity="";
    private (long Version,int X,int Y,bool Copy)? patternPreviewKey;
    private Blueprint? patternPreview;
    private int patternOverwritten;
    internal void OfferRecovery(DraftRecoveryEntry entry,Func<bool,bool> resolve,Action defer)
    {recoveryPrompt=entry;resolveRecovery=resolve;deferRecovery=defer;recoveryDeleteConfirm=false;recoveryError="";Suspend();}
    private void DeferRecovery(){Suspend();deferRecovery?.Invoke();}
    private void DrawRecovery(SpriteBatch b)
    {
        void Resolve(bool restore)
        {
            if(resolveRecovery?.Invoke(restore)!=true){recoveryError=T("recovery-failed","草稿未能恢复或删除，原缓存已保留。");recoveryDeleteConfirm=false;return;}
            recoveryPrompt=null;
            if(restore){ResetForOpenedDesign();notice=T("recovery-restored","已恢复未保存作品，请手动保存图纸。 ");noticeUntil=Now+2;}
            else Suspend();
        }
        if(recoveryDeleteConfirm)
        {
            Dialog(b,T("recovery-delete-confirm","删除未保存作品的恢复缓存？"),new[]{
                (T("cancel","取消"),(Action)(()=>recoveryDeleteConfirm=false)),
                (T("recovery-delete","删除恢复缓存"),(Action)(()=>Resolve(false)))});return;
        }
        ArtResources.Scope=FeedbackScope;hoverText=null;controls.Clear();Fill(b,layout.Frame,Color.Black*.65f);
        var p=RecoveryLayout.Calculate(layout.Frame);ArtResources.Panel(b,p.Dialog);
        Line(b,T("recovery-title","发现上次未保存作品"),new(p.Dialog.X+16,p.Dialog.Y+16,p.Dialog.Width-88,40),ArtResources.Ink);
        DrawOverlayClose(b,p.Close,DeferRecovery);
        ArtResources.Panel(b,p.Preview,"inset");
        ArtResources.DesignPreview(b,recoveryPrompt!.Draft,new(p.Preview.X+8,p.Preview.Y+8,p.Preview.Width-16,p.Preview.Height-16));
        ArtResources.Paragraph(b,!string.IsNullOrWhiteSpace(recoveryPrompt.Draft.Name)?recoveryPrompt.Draft.Name:CategoryPath(recoveryPrompt.Draft.Use),p.Name,ArtResources.Ink);
        var note=T("recovery-note","不会自动加入图纸库；恢复后仍需手动保存。");
        if(recoveryError.Length>0)note+="\n\n"+recoveryError;
        int noteHeight=Math.Min(p.Description.Height,ArtResources.ParagraphLines(note,p.Description.Width).Length*24);
        ArtResources.Paragraph(b,note,p.Description with{Y=p.Description.Y+(p.Description.Height-noteHeight)/2,Height=noteHeight},ArtResources.MutedInk);
        TextButton(b,p.Delete,T("recovery-delete","删除恢复缓存"),()=>recoveryDeleteConfirm=true);
        TextButton(b,p.Restore,T("recovery-restore","恢复作品"),()=>Resolve(true),iconKey:"primary");
    }
    private void DrawOverlayClose(SpriteBatch b,UiRect r,Action close)
    {
        ArtResources.CloseButton(b,new ClickableTextureComponent(ArtResources.Rect(r),Game1.mouseCursors,new Rectangle(337,494,12,12),3f));
        controls.Add((r,close));
    }
    private void CancelPattern(){patternSelecting=patternMoveOpen=false;patternAnchor=null;patternPreview=null;patternPreviewKey=null;Suspend();}
    private void StartPattern(bool select)
    {
        selector="";Suspend();patternDx=patternDy=0;patternCopy=false;patternAnchor=null;
        if(select){patternSelecting=true;notice=T("pattern-select-note","拖动框选；手柄按A定起点，再按A定终点。Esc取消。");return;}
        var grid=document.ViewSnapshot();var cells=grid.Cells.Select((c,i)=>(c,i)).Where(p=>p.c is not null).Select(p=>p.i).ToArray();
        if(cells.Length==0){notice=T("place-first","请先摆豆");return;}
        int l=cells.Min(i=>i%grid.Width),r=cells.Max(i=>i%grid.Width),t=cells.Min(i=>i/grid.Width),bottom=cells.Max(i=>i/grid.Width);
        patternRegion=new(l,t,r-l+1,bottom-t+1);patternMoveOpen=true;
    }
    private void SelectPatternCell(int x,int y)
    {
        if(patternAnchor is null){patternAnchor=new(x,y);patternEnd=new(x,y);}
        else{patternEnd=new(x,y);FinishPatternSelection();}
    }
    private void FinishPatternSelection()
    {
        if(patternAnchor is not {} a)return;
        patternRegion=new(Math.Min(a.X,patternEnd.X),Math.Min(a.Y,patternEnd.Y),Math.Abs(a.X-patternEnd.X)+1,Math.Abs(a.Y-patternEnd.Y)+1);
        patternAnchor=null;patternSelecting=false;patternMoveOpen=true;patternDx=patternDy=0;patternCopy=false;notice="";Suspend();
    }
    private void DrawPatternSelection(SpriteBatch b)
    {
        if(!patternSelecting||patternAnchor is not {} a)return;
        int l=Math.Min(a.X,patternEnd.X),t=Math.Min(a.Y,patternEnd.Y),r=Math.Max(a.X,patternEnd.X),bottom=Math.Max(a.Y,patternEnd.Y);
        var first=transform.CellRect(l,t);var last=transform.CellRect(r,bottom);
        int x=Math.Max(first.X,layout.Canvas.X),y=Math.Max(first.Y,layout.Canvas.Y),right=Math.Min(last.Right,layout.Canvas.Right),down=Math.Min(last.Bottom,layout.Canvas.Bottom);
        if(right<=x||down<=y)return;
        var ink=new Color(255,218,84);
        Fill(b,new(x,y,right-x,2),ink);Fill(b,new(x,down-2,right-x,2),ink);
        Fill(b,new(x,y,2,down-y),ink);Fill(b,new(right-2,y,2,down-y),ink);
    }
    private void DrawPatternMove(SpriteBatch b)
    {
        ArtResources.Scope=FeedbackScope;hoverText=null;controls.Clear();Fill(b,layout.Frame,Color.Black*.65f);
        var p=PatternMoveLayout.Calculate(layout.Frame);ArtResources.Panel(b,p.Dialog);
        Line(b,T("pattern-edit","移动与复制"),new(p.Dialog.X+16,p.Dialog.Y+16,p.Dialog.Width-88,40),ArtResources.Ink);
        DrawOverlayClose(b,p.Close,CancelPattern);
        var key=(document.ChangeVersion,patternDx,patternDy,patternCopy);
        if(patternPreviewKey!=key)
        {
            patternPreview=document.Snapshot();PatternTransform.Apply(patternPreview.Views[document.View],patternRegion,patternDx,patternDy,patternCopy);
            patternOverwritten=PatternTransform.Overwritten(display.Get(document.ChangeVersion,document.Snapshot).Views[document.View],patternRegion,patternDx,patternDy,patternCopy);
            patternPreviewKey=key;
        }
        ArtResources.Panel(b,p.Preview,"inset");
        ArtResources.DesignPreview(b,patternPreview!,new(p.Preview.X+8,p.Preview.Y+8,p.Preview.Width-16,p.Preview.Height-16),framed:false,trimTransparent:false);
        var grid=display.Get(document.ChangeVersion,document.Snapshot).Views[document.View];
        int overwritten=patternOverwritten;
        string info=ContentText.Format("simple.pattern-offset",$"位移：横向 {patternDx}，纵向 {patternDy}");
        Line(b,info,new(p.Description.X,p.Description.Y,p.Description.Width,24),ArtResources.Ink);
        ArtResources.Paragraph(b,overwritten>0?ContentText.Format("simple.pattern-overwrite",$"将覆盖 {overwritten} 颗豆，可撤销。"):
            T("pattern-safe","保留颜色与材料；应用后可一步撤销。"),new(p.Description.X,p.Description.Y+24,p.Description.Width,48),ArtResources.MutedInk);
        void Move(int dx,int dy){patternDx+=dx;patternDy+=dy;}
        TextButton(b,p.Up,"▲",()=>Move(0,-1),PatternTransform.CanPlace(grid,patternRegion,patternDx,patternDy-1));
        TextButton(b,p.Left,"◀",()=>Move(-1,0),PatternTransform.CanPlace(grid,patternRegion,patternDx-1,patternDy));
        TextButton(b,p.Down,"▼",()=>Move(0,1),PatternTransform.CanPlace(grid,patternRegion,patternDx,patternDy+1));
        TextButton(b,p.Right,"▶",()=>Move(1,0),PatternTransform.CanPlace(grid,patternRegion,patternDx+1,patternDy));
        TextButton(b,p.Mode,T("pattern-move","移动"),()=>patternCopy=false,active:!patternCopy);
        TextButton(b,p.Copy,T("pattern-copy","复制"),()=>patternCopy=true,active:patternCopy);
        TextButton(b,p.Cancel,T("cancel","取消"),CancelPattern);
        TextButton(b,p.Apply,T("apply","应用"),()=>{document.TransformRegion(patternRegion,patternDx,patternDy,patternCopy);CancelPattern();},patternDx!=0||patternDy!=0,iconKey:"primary");
    }
}
#endif
