#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private bool workColorsPage;
    private int workColorPage;
    private long workColorVersion=-1;
    private ArtworkColor[] workColors=Array.Empty<ArtworkColor>();
    private uint workFrom,workTo;
    private readonly ArtworkPaletteCache artworkPaletteCache=new();
    private void RefreshWorkColors()
    {
        if(workColorVersion==document.ChangeVersion)return;
        workColors=artworkPaletteCache.Colors(document.ChangeVersion,()=>display.Get(document.ChangeVersion,document.Snapshot));workColorVersion=document.ChangeVersion;
        workColorPage=Math.Clamp(workColorPage,0,Math.Max(0,(workColors.Length-1)/20));
        if(!workColors.Any(c=>c.Rgba==workFrom)&&workColors.Length>0){workFrom=workTo=workColors[0].Rgba;workColorPage=0;}
    }
    private void PaletteMode(bool work)
    {
        if(workColorsPage==work)return;
        workColorsPage=work;paletteDrag="";
        if(work){RefreshWorkColors();(hue,saturation,value)=BeadPalette.ToHsv(workTo);}
        else SelectFavorite(favoriteIndex);
        Suspend();
    }
    private Blueprint ReplacementPreview()
        =>artworkPaletteCache.Preview(document.ChangeVersion,()=>display.Get(document.ChangeVersion,document.Snapshot),workFrom,workTo);
    private void DrawCreativePalette(SpriteBatch b)
    {
        ArtResources.Scope=workColorsPage?"dye/work":"dye/common";hoverText=null;
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var p=CreativeDialogLayout.Calculate(layout.Frame);paletteLayout=p.Picker;
        ArtResources.Panel(b,p.Dialog);
        TextButton(b,p.CommonTab,T("common-colors","常用豆色"),()=>PaletteMode(false),active:!workColorsPage);
        TextButton(b,p.WorkTab,T("artwork-colors","作品用色"),()=>PaletteMode(true),active:workColorsPage);
        RefreshWorkColors();
        uint before=workColorsPage?workFrom:progress.FavoriteColors[favoriteIndex];
        uint after=workColorsPage?workTo:paletteDraft![favoriteIndex];
        int count=workColors.FirstOrDefault(c=>c.Rgba==workFrom)?.Count??0;
        DrawDyeSurface(b,p.Picker);
        int total=workColorsPage?workColors.Length:20;
        for(int i=0;i<20;i++)
        {
            int index=(workColorsPage?workColorPage*20:0)+i;if(index>=total)break;
            uint rgba=workColorsPage?workColors[index].Rgba:paletteDraft![index];
            var r=p.Cells[i];bool selected=workColorsPage?rgba==workFrom:index==favoriteIndex;
            ArtResources.PaletteBead(b,ArtResources.BeadSlot(b,r,selected,r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY)),ColorOf(rgba));
            if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=$"{MardPaletteReference.Current.Display(rgba)} · #{rgba>>8:X6}"+(workColorsPage?$" · {workColors[index].Count} "+T("beads","颗豆"):"");
            controls.Add((r,()=>
            {
                if(workColorsPage)
                {
                    // Clicking the selected source must not discard a color being compared.
                    if(workFrom!=rgba){workFrom=workTo=rgba;(hue,saturation,value)=BeadPalette.ToHsv(rgba);}
                }
                else SelectFavorite(index);
                paletteDrag="";
            }));
        }
        if(total==0)ArtResources.TextLine(b,T("no-artwork-colors","画板上还没有豆子"),new(p.Left.X,p.Left.Y,p.Left.Width,64),ArtResources.MutedInk,true);
        if(workColorsPage&&total>20)
        {
            ArrowButton(b,p.PreviousPage,false,()=>{workColorPage--;paletteDrag="";},workColorPage>0);
            ArrowButton(b,p.NextPage,true,()=>{workColorPage++;paletteDrag="";},(workColorPage+1)*20<total);
            Line(b,$"{workColorPage+1}/{(total+19)/20}",p.PageInfo,ArtResources.MutedInk);
        }
        string brand=MardPaletteReference.Current.ColorCount==221?"MARD 221":T("mard-fallback-short","MARD 20 备用");
        Line(b,brand+(workColorsPage?$" · {count} "+T("beads-short","颗"):""),p.Info,ArtResources.MutedInk);
        void Card(UiRect r,string label,uint rgba,bool hasPreview=false)
        {
            ArtResources.PaletteBead(b,new(r.X,r.Y+8,24,24),ColorOf(rgba));
            int width=r.Width-32-(hasPreview?48:0);
            Line(b,label,new(r.X+32,r.Y,width,20),ArtResources.Ink);
            Line(b,MardPaletteReference.Current.Display(rgba),new(r.X+32,r.Y+20,width,20),ArtResources.Ink);
            if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=$"{brand} · #{rgba>>8:X6}";
        }
        Card(p.BeforeCard,T("before-color","原色"),before);
        Card(p.AfterCard,T("after-color","新色"),after,p.Compact&&workColorsPage&&total>0);
        if(workColorsPage&&total>0)
        {
            ArtResources.DesignPreview(b,ReplacementPreview(),p.Preview,trimTransparent:true);
            controls.Add((p.Preview,()=>{scene.Open(ReplacementPreview());Suspend();}));
            if(p.Preview.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=T("scene-open","点击查看实际比例与占地");
        }
        else if(!p.Compact)
        {
            // Show the selected bead at a useful size instead of a large empty information panel.
            int size=Math.Min(100,Math.Min(p.Preview.Width,p.Preview.Height-56));
            ArtResources.PaletteBead(b,new(p.Preview.X+(p.Preview.Width-size)/2,p.Preview.Y,size,size),ColorOf(after));
            Line(b,T("palette-only","仅修改常用豆色"),new(p.Preview.X,p.Preview.Y+size+12,p.Preview.Width,20),ArtResources.MutedInk);
            Line(b,T("palette-no-repaint","不会重涂作品"),new(p.Preview.X,p.Preview.Y+size+36,p.Preview.Width,20),ArtResources.MutedInk);
        }
        TextButton(b,p.Back,workColorsPage?T("paint-with-color","用作画笔"):T("reset-color-short","还原此色"),()=>
        {
            if(workColorsPage){color=BeadPalette.Id(workFrom);tool="paint";ClosePalette();Suspend();}
            else{paletteDraft![favoriteIndex]=BeadPalette.Defaults[favoriteIndex];SelectFavorite(favoriteIndex);}
        },!workColorsPage||total>0);
        TextButton(b,p.Next,workColorsPage?T("reset-color-short","还原此色"):T("reset-default-short","恢复默认"),()=>
        {
            if(workColorsPage){workTo=workFrom;(hue,saturation,value)=BeadPalette.ToHsv(workTo);}
            else{paletteDraft=BeadPalette.Defaults.ToList();SelectFavorite(favoriteIndex);}
            paletteDrag="";
        },!workColorsPage||total>0);
        TextButton(b,p.Apply,workColorsPage?T("apply-color-short","应用换色"):T("save-colors-short","保存豆色"),()=>
        {
            if(!workColorsPage){progress.FavoriteColors=paletteDraft!.ToList();OnlineSession.MarkPersonalChanged();color=BeadPalette.Id(progress.FavoriteColors[favoriteIndex]);ClosePalette();}
            else if(document.ReplaceExactColor(workFrom,workTo))
            {
                workFrom=workTo;RefreshWorkColors();
                int index=Array.FindIndex(workColors,c=>c.Rgba==workFrom);if(index>=0)workColorPage=index/20;
            }
            Suspend();
        },!workColorsPage||total>0&&workFrom!=workTo,iconKey:"primary");
    }
}
#endif
