using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Objects;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private Farmer? fitting;
    private int fittingDirection=2;
    private bool fittingFailed;
    private void BeginFitting()
    {
        CloseFitting();drawer="";fittingFailed=false;
        try
        {
            var design=document.Snapshot();
            if(!ClothingTemplates.Matches(design))return;
            fitting=Game1.player.CreateFakeEventFarmer();
            var item=new ProductItems().Create(new(){Design=design,RulesVersion="preview"});
            if(item is Hat hat)fitting.hat.Value=hat;
            else if(design.Use==ProductUse.Shirt)fitting.shirtItem.Value=(Clothing)item;
            else fitting.pantsItem.Value=(Clothing)item;
            fitting.UpdateClothing();fitting.FarmerRenderer.MarkSpriteDirty();fittingDirection=2;
        }
        catch(Exception ex) { referenceReader.ReportPreviewError(ex);CloseFitting();notice="editor.fitting-failed"; }
    }
    public void CloseFitting()
    {
        if(fitting is null)return;
        // This renderer belongs only to the disposable preview actor.
        var actor=fitting;fitting=null;
        try
        {
            if(HarmonyLib.AccessTools.Field(typeof(FarmerRenderer),"baseTexture").GetValue(actor.FarmerRenderer) is Texture2D texture && !texture.IsDisposed)texture.Dispose();
        }
        finally { actor.FarmerRenderer.unload(); }
    }
    private void DrawFitting(SpriteBatch b)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var box=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,box);Line(b,text.Get("editor.fitting-note"),new(box.X+12,box.Y+12,box.Width-24,28),ArtResources.Ink);
        int frame=fittingDirection==2?0:fittingDirection==0?12:6;
        bool before=FarmerRenderer.isDrawingForUI;
        try
        {
            FarmerRenderer.isDrawingForUI=true;
            if(fittingFailed){Line(b,text.Get("editor.fitting-failed"),new(box.X+12,box.Y+52,box.Width-24,28),ArtResources.Ink);}
            else {
            fitting!.faceDirection(fittingDirection);
            var animation=new FarmerSprite.AnimationFrame(frame,100){flip=fittingDirection==3};
            fitting.FarmerRenderer.draw(b,animation,frame,new Rectangle(frame%6*16,frame/6*32,16,32),
                new Vector2(box.X+box.Width/2-32,box.Y+box.Height/2-64),Vector2.Zero,0.9f,fittingDirection,Color.White,0,1,fitting);
            }
        }
        catch(Exception ex) { if(!fittingFailed)referenceReader.ReportPreviewError(ex);fittingFailed=true;Line(b,text.Get("editor.fitting-failed"),new(box.X+12,box.Y+52,box.Width-24,28),ArtResources.Ink); }
        finally { FarmerRenderer.isDrawingForUI=before; }
        int half=(box.Width-36)/2;
        Button(b,new(box.X+12,box.Bottom-48,half,36),"editor.next-direction",()=>fittingDirection=(fittingDirection+1)%4);
        Button(b,new(box.X+24+half,box.Bottom-48,half,36),"editor.cancel",CloseFitting);
    }
}
