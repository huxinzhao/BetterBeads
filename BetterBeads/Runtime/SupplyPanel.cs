using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private BeadPreparationPlan? preparationPlan;
    private void BeginPreparation()
    {
        Suspend();drawer="";
        var preview=manufacturing?.PreviewPreparation(material);
        preparationPlan=preview?.Plan;
        if(preparationPlan is null)notice=preview?.Message??"manufacturing.unavailable";
    }
    private void DrawPreparation(SpriteBatch b)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var box=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,box);var plan=preparationPlan!;var raw=plan.Before[plan.SourceSlot]!;
        Line(b,text.Get("supply.prepare-title"),new(box.X+12,box.Y+12,box.Width-24,28),ArtResources.Ink);
        Line(b,text.Get("supply.consume",new{name=ItemRegistry.GetDataOrErrorItem(raw.ItemId).DisplayName,count=plan.Consumed,quality=QualityName(raw.Quality)}),new(box.X+12,box.Y+58,box.Width-24,28),ArtResources.Ink);
        Line(b,MaterialName(plan.Material)+" × "+plan.Produced,new(box.X+12,box.Y+100,box.Width-24,28),ArtResources.Ink);
        var message=Game1.parseText(text.Get("supply.prepare-help"),Game1.smallFont,box.Width-24);
        b.DrawString(Game1.smallFont,message,new(box.X+12,box.Y+142),ArtResources.Ink);
        int half=(box.Width-36)/2;
        Button(b,new(box.X+12,box.Bottom-48,half,36),"editor.cancel",()=>preparationPlan=null);
        Button(b,new(box.X+24+half,box.Bottom-48,half,36),"supply.prepare-confirm",()=>{preparationPlan=null;notice=manufacturing!.SubmitPreparation(plan);});
    }
}
