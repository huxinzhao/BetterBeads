using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private List<uint>? paletteDraft;
    private int favoriteIndex;
    private bool dyePage;
#if BEADS_LITE
    private bool backgroundPage;
    private uint backgroundCandidate;
    private void BeginBackgroundPalette()
    {
        BeginPalette();backgroundPage=true;dyePage=true;
        backgroundCandidate=document.Snapshot().BackgroundRgba;
        (hue,saturation,value)=BeadPalette.ToHsv(backgroundCandidate);
    }
#endif
    private double hue,saturation,value;
    private string paletteDrag="";
    private ColorPickerLayout? paletteLayout;
    public bool PaletteOpen=>paletteDraft is not null
#if BEADS_LITE
        &&!scene.Active
#endif
        ;
    public UiRect PaletteCloseButton=>
#if BEADS_LITE
        paletteSearchOpen?PaletteSearchLayout.Calculate(layout.Frame).Close:CreativeDialogLayout.Calculate(layout.Frame).Close;
#else
        ColorPickerLayout.Calculate(layout.Frame).Close;
#endif
    private Texture2D? paletteTexture,hueTexture;
    private double textureHue=double.NaN;
    public void ClosePalette()
    {paletteDraft=null;dyePage=false;paletteDrag="";paletteTexture?.Dispose();hueTexture?.Dispose();paletteTexture=hueTexture=null;textureHue=double.NaN;
#if BEADS_LITE
        ClosePaletteSearch();
        ControllerStopPicker();
        workColorsPage=false;backgroundPage=false;artworkPaletteCache.Clear();workColorVersion=-1;
#endif
    }
    public void ClosePaletteOrSearch()
    {
#if BEADS_LITE
        if(paletteSearchOpen){ClosePaletteSearch();return;}
#endif
        ClosePalette();
    }
    private void BeginPalette()
    {
#if BEADS_LITE
        if(tool=="picker")tool="paint";
#endif
        Suspend();drawer="";paletteDraft=BeadPalette.Normalize(progress.FavoriteColors);paletteLayout=null;
        SelectFavorite(favoriteIndex);
    }
    private void SelectFavorite(int index)
    {favoriteIndex=Math.Clamp(index,0,BeadPalette.Defaults.Length-1);(hue,saturation,value)=BeadPalette.ToHsv(paletteDraft![favoriteIndex]);paletteDrag="";}
    private bool PaletteClick(int x,int y)
    {
        if(paletteLayout is null || !paletteLayout.CanPick(dyePage,x,y))return false;
        if(paletteLayout.SaturationValue.Contains(x,y))paletteDrag="sv";
        else if(paletteLayout.Hue.Contains(x,y))paletteDrag="hue";
        else return false;
        UpdatePaletteColor(x,y);return true;
    }
    private void UpdatePaletteColor(int x,int y)
    {
        if(paletteDraft is null || paletteLayout is null)return;
        var area=paletteDrag=="hue"?paletteLayout.Hue:paletteLayout.SaturationValue;
        if(paletteDrag=="hue")hue=359.999*Math.Clamp((y-area.Y)/(double)Math.Max(1,area.Height-1),0,1);
        else
        {
            saturation=Math.Clamp((x-area.X)/(double)Math.Max(1,area.Width-1),0,1);
            value=1-Math.Clamp((y-area.Y)/(double)Math.Max(1,area.Height-1),0,1);
        }
        uint picked=BeadPalette.FromHsv(hue,saturation,value);
        SetPaletteCandidate(picked);
    }
    private void SetPaletteCandidate(uint picked)
    {
        if(paletteDraft is null)return;
#if BEADS_LITE
        if(backgroundPage){backgroundCandidate=picked;return;}
        if(workColorsPage){workTo=picked;return;}
#endif
        paletteDraft[favoriteIndex]=picked;
    }
    private void DrawPaletteEditor(SpriteBatch b)
    {
#if BEADS_LITE
        DrawCreativePalette(b);
#else
        DrawLegacyPaletteEditor(b);
#endif
    }
#if !BEADS_LITE
    private void DrawLegacyPaletteEditor(SpriteBatch b)
    {
        ArtResources.Scope="dye";
        // The editor is drawn first; modal tooltips must not inherit its hovered button.
        hoverText=null;
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();paletteLayout=ColorPickerLayout.Calculate(layout.Frame);
        var p=paletteLayout;ArtResources.Panel(b,p.Dialog);
        Line(b,text.Get("palette.title"),new(p.Dialog.X+24,p.Dialog.Y+16,p.Compact?144:140,40),ArtResources.Ink);
        if(p.Compact)
        {
            ArtResources.Tab(b,p.ColorTab,text.Get("palette.tray"),!dyePage);
            ArtResources.Tab(b,p.DyeTab,text.Get("palette.dye-page"),dyePage);
            controls.Add((p.ColorTab,()=>{dyePage=false;paletteDrag="";}));
            controls.Add((p.DyeTab,()=>{dyePage=true;paletteDrag="";}));
        }
        else Line(b,text.Get("palette.hint"),new(p.Dialog.X+180,p.Dialog.Y+24,p.Dialog.Width-260,24),ArtResources.MutedInk);
        if(!p.Compact || dyePage)DrawDyeSurface(b,p);
        if(!p.Compact || !dyePage)
        {
            var cells=ColorPickerLayout.FavoriteCells(p.Slots,false);
            for(int i=0;i<BeadPalette.Defaults.Length;i++)
            {
                int index=i;var r=cells[i];bool selected=i==favoriteIndex;
                bool hover=r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY);
                var face=ArtResources.BeadSlot(b,r,selected,hover);
                ArtResources.PaletteBead(b,face,ColorOf(paletteDraft![i]));
                if(hover)hoverText=$"{i+1}/{BeadPalette.Defaults.Length} · #{paletteDraft![i]>>8:X6}";
                controls.Add((r,()=>{SelectFavorite(index);if(p.Compact)dyePage=true;}));
            }
        }
        int beadSize=p.PreviewBeadSize;
        var before=new UiRect(p.Swatch.X,p.Swatch.Y+4,beadSize,beadSize);
        var after=new UiRect(p.Swatch.Right-beadSize,p.Swatch.Y+4,beadSize,beadSize);
        ArtResources.BeadCell(b,before,before,ColorOf(progress.FavoriteColors[favoriteIndex]),false);
        ArtResources.BeadCell(b,after,after,ColorOf(paletteDraft![favoriteIndex]),false);
        Line(b,"→",new(p.Swatch.X+p.Swatch.Width/2-10,p.Swatch.Y+(beadSize-24)/2+4,24,24),ArtResources.MutedInk);
        if(p.Compact)Line(b,$"{favoriteIndex+1}/{BeadPalette.Defaults.Length}",p.SlotInfo,ArtResources.Ink);
        Line(b,(p.Compact?"":$"{favoriteIndex+1}/{BeadPalette.Defaults.Length} · ")+$"#{paletteDraft[favoriteIndex]>>8:X6}",p.Info,ArtResources.Ink);
        if(!p.Compact)Line(b,text.Get("palette.no-repaint"),p.Hint,ArtResources.MutedInk);
        TextButton(b,p.Previous,text.Get("palette.reset-slot"),()=>{paletteDraft[favoriteIndex]=BeadPalette.Defaults[favoriteIndex];SelectFavorite(favoriteIndex);});
        TextButton(b,p.Next,text.Get("palette.reset-all"),()=>{paletteDraft=BeadPalette.Defaults.ToList();SelectFavorite(favoriteIndex);});
        TextButton(b,p.Apply,text.Get("palette.save"),()=>{progress.FavoriteColors=paletteDraft!.ToList();color=BeadPalette.Id(progress.FavoriteColors[favoriteIndex]);ClosePalette();},iconKey:"primary");
    }
#endif
    private void DrawDyeSurface(SpriteBatch b,ColorPickerLayout p)
    {
        // Cache gradients; dragging saturation/value only moves the marker and updates the bead preview.
        if(hueTexture is null)
        {
            hueTexture=new Texture2D(Game1.graphics.GraphicsDevice,1,128);
            hueTexture.SetData(Enumerable.Range(0,128).Select(y=>ColorOf(BeadPalette.FromHsv(y*359.999/127,1,1))).ToArray());
        }
        if(paletteTexture is null || textureHue!=hue)
        {
            paletteTexture??=new Texture2D(Game1.graphics.GraphicsDevice,128,128);
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=ColorOf(BeadPalette.FromHsv(hue,x/127d,1-y/127d));
            paletteTexture.SetData(pixels);textureHue=hue;
        }
        DyeFrame(b,p.SaturationValue);DyeFrame(b,p.Hue);
        b.Draw(paletteTexture,ArtResources.Rect(p.SaturationValue),Color.White);
        b.Draw(hueTexture,ArtResources.Rect(p.Hue),Color.White);
        int sx=p.SaturationValue.X+(int)(saturation*(p.SaturationValue.Width-1)),sy=p.SaturationValue.Y+(int)((1-value)*(p.SaturationValue.Height-1));
        DyeFrame(b,new(sx-3,sy-3,7,7));
        int hy=p.Hue.Y+(int)(hue/360*(p.Hue.Height-1));
        Fill(b,new(p.Hue.X-4,hy-3,p.Hue.Width+8,7),ArtResources.Ink);
        Fill(b,new(p.Hue.X-3,hy-1,p.Hue.Width+6,3),Color.White);
    }
    private static void DyeFrame(SpriteBatch b,UiRect r)
    {
        Fill(b,new(r.X-2,r.Y-2,r.Width+4,r.Height+4),new Color(102,77,57));
        Fill(b,new(r.X-1,r.Y-1,r.Width+2,r.Height+2),new Color(255,247,225));
    }
}
