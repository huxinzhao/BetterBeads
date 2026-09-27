using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed class ImportPanel
{
    private readonly Blueprint baseline;
    private readonly string view;
    private readonly ProcessingCatalog catalog;
    private readonly SaveProgress progress;
    private readonly ReferenceReader reader;
    private readonly ITranslationHelper text;
    private readonly Action<Blueprint?> finish;
    private readonly List<(UiRect Rect,Action Action)> controls=new();
    private ReferencePixels? source;
    private Blueprint? sourceDesign;
    private ImportPreview? preview;
    private uint[]? convertedPixels;
    private ImportOptions options=new(ImportSizing.Original,PreserveColors:true);
    private int page;
    private bool settings,original;
    private bool? previewCreative;
    private string error="";
    public ImportPanel(Blueprint baseline,string view,ProcessingCatalog catalog,SaveProgress progress,
        ReferenceReader reader,ITranslationHelper text,Action<Blueprint?> finish)
    {this.baseline=baseline.Copy();this.view=view;this.catalog=catalog;this.progress=progress;this.reader=reader;this.text=text;this.finish=finish;}
    public void InvalidateLayout()=>controls.Clear();
    public void Click(int x,int y)
    {foreach(var control in controls.AsEnumerable().Reverse())if(control.Rect.Contains(x,y)){controls.Clear();control.Action();return;}}
    public void Back(){if(settings)settings=false;else finish(null);controls.Clear();}
    private void Rebuild()
    {
        preview=null;convertedPixels=null;error="";previewCreative=PlayMode.Creative;
        if(source is null)return;
        try
        {
            preview=PixelImport.Create(baseline,view,source,options,catalog,PlayMode.Unlocked(catalog,progress));
            convertedPixels=preview.Candidate.Views[view].Cells.Select(c=>c?.Rgba??0u).ToArray();
        }
        catch(ArgumentException){error="import.adjust-size";}
    }
    public void Draw(SpriteBatch b,UiRect frame)
    {
        if(source is not null && previewCreative!=PlayMode.Creative)Rebuild();
        controls.Clear();
        b.Draw(Game1.staminaRect,new Rectangle(frame.X,frame.Y,frame.Width,frame.Height),Color.Black*0.8f);
        var layout=ImportLayout.Calculate(frame);var box=layout.Dialog;
        ArtResources.Panel(b,box);
        int x=box.X+12,w=box.Width-24;
        Label(b,source is null?text.Get("import.title").ToString():text.Get(original?"import.original":"import.converted")
            +$" · {source.Width}×{source.Height} → {baseline.Views[view].Width}×{baseline.Views[view].Height}",x,box.Y+10,w);
        if(source is null)
        {
            var items=Game1.player.Items.Take(Game1.player.MaxItems).Where(i=>i is not null).ToArray();
            int count=layout.SelectionRows;page=Math.Clamp(page,0,Math.Max(0,(items.Length-1)/count));
            for(int i=0;i<count && page*count+i<items.Length;i++)
            {
                var item=items[page*count+i];
                Button(b,new(x,box.Y+44+i*42,w,36),item.DisplayName,()=>
                {
                    if(reader.TryCapture(item,view,out source,out error)){sourceDesign=reader.CaptureDesign(item);options=new(ImportSizing.Original,PreserveColors:true);Rebuild();}
                });
            }
            int third=(w-12)/3;
            Button(b,new(x,box.Bottom-48,third,40),text.Get("processing.previous").ToString(),()=>page--,page>0);
            Button(b,new(x+third+6,box.Bottom-48,third,40),text.Get("processing.next").ToString(),()=>page++,(page+1)*count<items.Length);
            Button(b,new(x+2*(third+6),box.Bottom-48,third,40),text.Get("editor.cancel").ToString(),()=>finish(null));
            if(items.Length==0)Label(b,text.Get("import.empty").ToString(),x,box.Y+48,w);
        }
        else if(settings)
        {
            int half=(w-8)/2,y=box.Y+44;
            Button(b,new(x,y,w,36),text.Get("import.size."+options.Sizing).ToString(),()=>
            {options=options with{Sizing=(ImportSizing)(((int)options.Sizing+1)%3)};Rebuild();});y+=42;
            Button(b,new(x,y,w,36),text.Get(options.PreserveColors?"import.exact-colors":"import.base-colors").ToString(),()=>
            {options=options with{PreserveColors=!options.PreserveColors};Rebuild();});y+=42;
            Button(b,new(x,y,half,36),text.Get("import.alpha").ToString()+$" {options.AlphaThreshold} −",()=>
            {options=options with{AlphaThreshold=(byte)Math.Max(0,options.AlphaThreshold-1)};Rebuild();});
            Button(b,new(x+half+8,y,half,36),text.Get("import.alpha").ToString()+" +",()=>
            {options=options with{AlphaThreshold=(byte)Math.Min(255,options.AlphaThreshold+1)};Rebuild();});y+=42;
            var allowed=catalog.Materials.Where(m=>MaterialRules.CanUse(m,baseline.Use)).Select(m=>m.Id).ToList();
            bool clothing=!WeaponMaterials.IsWeapon(baseline.Use);
            Button(b,new(x,y,w,36),options.MaterialId is null?text.Get("issue.unassigned-material").ToString():text.Get("material."+options.MaterialId).ToString(),()=>
            {
                int next=options.MaterialId is null?0:allowed.IndexOf(options.MaterialId)+1;
                options=options with{MaterialId=next>=allowed.Count?null:allowed[next]};Rebuild();
            },!clothing && allowed.Count>0);y+=42;
            Button(b,new(x,y,half,36),$"X {options.CropX} + ↻",()=>
            {options=options with{CropX=(options.CropX+1)%source.Width};Rebuild();},options.Sizing==ImportSizing.Crop);
            Button(b,new(x+half+8,y,half,36),$"Y {options.CropY} + ↻",()=>
            {options=options with{CropY=(options.CropY+1)%source.Height};Rebuild();},options.Sizing==ImportSizing.Crop);
            Button(b,new(x,box.Bottom-48,w,40),text.Get("import.preview").ToString(),()=>settings=false);
        }
        else
        {
            var pixels=original?source.Pixels:convertedPixels;
            int pw=original?source.Width:baseline.Views[view].Width,ph=original?source.Height:baseline.Views[view].Height;
            var image=layout.Image;
            if(sourceDesign is not null)image=new(image.X,image.Y+42,image.Width,Math.Max(1,image.Height-42));
            b.Draw(Game1.staminaRect,new Rectangle(image.X,image.Y,image.Width,image.Height),ArtResources.Canvas);
            if(pixels is not null)
            {
                float scale=Math.Min(image.Width/(float)pw,image.Height/(float)ph);
                int dx=image.X+(int)((image.Width-pw*scale)/2),dy=image.Y+(int)((image.Height-ph*scale)/2);
                for(int yy=0;yy<ph;yy++)for(int xx=0;xx<pw;xx++)
                {
                    uint rgba=pixels[yy*pw+xx];if((byte)rgba==0)continue;
                    var color=new Color((byte)(rgba>>24),(byte)(rgba>>16),(byte)(rgba>>8)) *((byte)rgba/255f);
                    int px=dx+(int)(xx*scale),py=dy+(int)(yy*scale),size=Math.Max(1,(int)Math.Ceiling(scale));
                    b.Draw(Game1.staminaRect,new Rectangle(px,py,Math.Min(size,image.Right-px),Math.Min(size,image.Bottom-py)),color);
                }
            }
            int half=(w-8)/2;
            if(sourceDesign is not null)Button(b,new(x,box.Y+44,w,36),text.Get("import.restore-design").ToString(),()=>
            {var restored=sourceDesign.Copy();restored.Id=baseline.Id;restored.Revision=baseline.Revision;finish(restored);});
            Button(b,new(x,box.Bottom-96,half,40),text.Get(original?"import.converted":"import.original").ToString(),()=>original=!original);
            Button(b,new(x+half+8,box.Bottom-96,half,40),text.Get("import.options").ToString(),()=>settings=true);
            Button(b,new(x,box.Bottom-48,half,40),text.Get("editor.cancel").ToString(),()=>finish(null));
            Button(b,new(x+half+8,box.Bottom-48,half,40),text.Get("editor.apply").ToString(),()=>finish(preview!.Candidate.Copy()),preview is not null);
            if(preview is not null)Label(b,text.Get("import.summary",new{count=preview.EffectiveCells,locked=preview.LockedColors.Count}).ToString(),x,box.Bottom-132,w);
        }
        if(error.Length>0)Label(b,text.Get(error).ToString(),x,box.Bottom-(settings?76:128),w);
    }
    private void Button(SpriteBatch b,UiRect r,string label,Action action,bool enabled=true)
    {ArtResources.ButtonText(b,r,label,enabled);if(enabled)controls.Add((r,action));}
    private static void Label(SpriteBatch b,string label,int x,int y,int width)
    {while(label.Length>0 && Game1.smallFont.MeasureString(label).X>width)label=label[..^1];b.DrawString(Game1.smallFont,label,new Vector2(x,y),ArtResources.Ink);}
}
