#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private bool padCanvas,padStroke,padErase,padWaitRelease,padCellInitialized;
    private int padCellX,padCellY;
    private double padMoveAt;
    private string padPicker="";
    internal bool ControllerCanvasActive=>padCanvas;
    internal bool ControllerPickerEditing=>padPicker.Length>0;
    internal bool ControllerTryPicker(UiRect target)
    {
        if(paletteDraft is null||paletteLayout is null)return false;
        if(target==paletteLayout.SaturationValue)padPicker="sv";
        else if(target==paletteLayout.Hue)padPicker="hue";
        else return false;
        return true;
    }
    internal void ControllerStopPicker()=>padPicker="";
    internal void ControllerAdjustPicker(int dx,int dy)
    {
        if(padPicker=="hue")hue=(hue+dy*3+360)%360;
        else if(padPicker=="sv")
        {saturation=Math.Clamp(saturation+dx*.02,0,1);value=Math.Clamp(value-dy*.02,0,1);}
        SetPaletteCandidate(BeadPalette.FromHsv(hue,saturation,value));
    }
    internal void ControllerEnterCanvas()
    {
        if(HasOverlay)return;
        var (width,height)=document.ViewSize;
        padCellX=Math.Clamp(padCellX,0,width-1);padCellY=Math.Clamp(padCellY,0,height-1);
        if(!padCellInitialized){padCellX=width/2;padCellY=height/2;padCellInitialized=true;}
        padCanvas=true;padWaitRelease=true;Suspend();
    }
    internal void ControllerLeaveCanvas(){EndPadStroke();padCanvas=false;if(patternSelecting)CancelPattern();else Suspend();}
    private void EndPadStroke(){if(padStroke)document.EndStroke();padStroke=false;}
    internal void ControllerTick(GamePadState state)
    {
        if(!padCanvas)return;
        if(HasOverlay){ControllerLeaveCanvas();return;}
        bool a=state.IsButtonDown(Buttons.A),x=state.IsButtonDown(Buttons.X);
        if(!a&&!x)padWaitRelease=false;
        bool erase=x,held=a||x;
        if(padStroke&&(!held||padErase!=erase))EndPadStroke();
        int dx=state.IsButtonDown(Buttons.DPadLeft)||state.ThumbSticks.Left.X<-.55f?-1:
            state.IsButtonDown(Buttons.DPadRight)||state.ThumbSticks.Left.X>.55f?1:0;
        int dy=state.IsButtonDown(Buttons.DPadUp)||state.ThumbSticks.Left.Y>.55f?-1:
            state.IsButtonDown(Buttons.DPadDown)||state.ThumbSticks.Left.Y<-.55f?1:0;
        bool moved=false;
        if((dx!=0||dy!=0)&&Now>=padMoveAt)
        {
            var (width,height)=document.ViewSize;
            int nx=Math.Clamp(padCellX+dx,0,width-1),ny=Math.Clamp(padCellY+dy,0,height-1);
            moved=nx!=padCellX||ny!=padCellY;padCellX=nx;padCellY=ny;
            if(moved)KeepPadCellVisible();
            padMoveAt=Now+.12;
        }
        if(held&&!padWaitRelease)
        {
            if(!padStroke)
            {
                padErase=erase;
                padStroke=UseToolAt(padCellX,padCellY,erase);
                if(!padStroke)padWaitRelease=true;
            }
            else if(moved){long before=document.ChangeVersion;document.ContinueStroke(padCellX,padCellY);
                if(!erase&&tool!="erase"&&document.ChangeVersion!=before)UiSounds.BeadPlaced();}
        }
        int px=(int)Math.Round(state.ThumbSticks.Right.X*7),py=-(int)Math.Round(state.ThumbSticks.Right.Y*7);
        if(px!=0||py!=0)transform.Pan(px,py);
    }
    internal void ControllerZoom(int direction)
    {
        var cell=transform.CellRect(padCellX,padCellY);
        transform.ZoomAt(Math.Clamp(cell.X+cell.Width/2,layout.Canvas.X,layout.Canvas.Right-1),
            Math.Clamp(cell.Y+cell.Height/2,layout.Canvas.Y,layout.Canvas.Bottom-1),direction);
        KeepPadCellVisible();
    }
    private void KeepPadCellVisible()
    {
        var r=transform.CellRect(padCellX,padCellY);
        int dx=r.X<layout.Canvas.X?layout.Canvas.X-r.X:r.Right>layout.Canvas.Right?layout.Canvas.Right-r.Right:0;
        int dy=r.Y<layout.Canvas.Y?layout.Canvas.Y-r.Y:r.Bottom>layout.Canvas.Bottom?layout.Canvas.Bottom-r.Bottom:0;
        if(dx!=0||dy!=0)transform.Pan(dx,dy);
    }
    internal void ControllerUndo(){EndPadStroke();UndoDesign();}
    internal void ControllerRedo(){EndPadStroke();RedoDesign();}
    internal void ControllerColors(){if(!HasOverlay){ControllerLeaveCanvas();BeginPalette();}}
    internal void DrawControllerCursor(SpriteBatch b)
    {
        if(!padCanvas||HasOverlay)return;
        var r=transform.CellRect(padCellX,padCellY);
        int left=Math.Max(r.X,layout.Canvas.X),top=Math.Max(r.Y,layout.Canvas.Y);
        int right=Math.Min(r.Right,layout.Canvas.Right),bottom=Math.Min(r.Bottom,layout.Canvas.Bottom);
        if(right<=left||bottom<=top)return;
        var ink=new Color(255,228,99);
        Fill(b,new(left,top,right-left,2),ink);Fill(b,new(left,bottom-2,right-left,2),ink);
        Fill(b,new(left,top,2,bottom-top),ink);Fill(b,new(right-2,top,2,bottom-top),ink);
    }
}
#endif
