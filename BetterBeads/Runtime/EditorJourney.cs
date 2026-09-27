using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private string TemplateName(string id)=>text.Get(FurnitureTemplates.TryGet(id,out var f)?f.NameKey:
        WeaponTemplates.TryGet(id,out var w)?w.NameKey:ClothingTemplates.Find(id) is not null?ClothingTemplates.NameKey(id):"template.unknown");
    public bool SaveAs()
    {
        Suspend();controls.Clear();
        if(!document.SaveAs(new BlueprintRepository(progress),out var copy)){notice="editor.save-failed";return false;}
        drawer="";notice="editor.saved-as";Saved?.Invoke(copy!.Id);return true;
    }
    private void NextView()
    {
        var views=document.Snapshot().Views.Keys.ToArray();document.SetView(views[(Array.IndexOf(views,document.View)+1)%views.Length]);
        Center();located=null;
    }
    private void DrawTemplates(SpriteBatch b)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var box=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,box);Line(b,text.Get("editor.choose-product"),new(box.X+12,box.Y+12,box.Width-24,28),ArtResources.Ink);
        var choices=manufacturing?.SingleViewChoices??Array.Empty<TemplateSpec>();
        int columns=box.Width>=650?3:2,rows=Math.Max(1,(box.Height-120)/76),perPage=columns*rows;
        templatePage=Math.Clamp(templatePage,0,Math.Max(0,(choices.Count-1)/perPage));int w=(box.Width-24-(columns-1)*8)/columns;
        for(int i=0;i<perPage && templatePage*perPage+i<choices.Count;i++)
        {
            int index=templatePage*perPage+i;var entry=choices[index];var rect=new UiRect(box.X+12+i%columns*(w+8),box.Y+50+i/columns*76,w,68);
            var face=ArtResources.Button(b,rect,true,entry.Id==document.Snapshot().TemplateId);
            Line(b,TemplateName(entry.Id),new(face.X+12,face.Y+6,w-24,24),ArtResources.Ink);
            Line(b,$"{entry.Width}×{entry.Height}"+(entry.WoolBudget is {} budget?" · "+text.Get("material.wool")+" "+budget:""),new(face.X+12,face.Y+34,w-24,22),ArtResources.MutedInk);
            controls.Add((rect,()=>{templatePicker=false;BeginConversion();conversionIndex=index;RefreshConversion();}));
        }
        int third=(box.Width-48)/3;
        Button(b,new(box.X+12,box.Bottom-52,third,40),"processing.previous",()=>templatePage--,templatePage>0);
        Button(b,new(box.X+24+third,box.Bottom-52,third,40),"processing.next",()=>templatePage++,(templatePage+1)*perPage<choices.Count);
        Button(b,new(box.X+36+third*2,box.Bottom-52,third,40),"editor.cancel",()=>templatePicker=false);
    }
    private void PreviewMaterialAssignment()
    {
        if(document.IsClothing)return;
        candidate=DraftRepairs.AssignMaterial(document.Snapshot(),catalog,material);
        candidateNote=text.Get("editor.assign-note",new{material=MaterialName(material)});checking=false;drawer="";
    }
    private void DrawHelp(SpriteBatch b)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var box=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,box);
        var lines=text.Get("editor.journey").ToString().Split('\n').SelectMany(line=>Game1.parseText(line,Game1.smallFont,box.Width-24).Split('\n')).ToArray();
        int pitch=Game1.smallFont.LineSpacing+8,rows=Math.Max(1,(box.Height-76)/pitch);
        helpPage=Math.Clamp(helpPage,0,Math.Max(0,(lines.Length-1)/rows));
        for(int i=0;i<rows && helpPage*rows+i<lines.Length;i++)
            b.DrawString(Game1.smallFont,lines[helpPage*rows+i],new(box.X+12,box.Y+12+i*pitch),ArtResources.Ink);
        int w=(box.Width-48)/3;
        Button(b,new(box.X+12,box.Bottom-48,w,36),"processing.previous",()=>helpPage--,helpPage>0);
        Button(b,new(box.X+24+w,box.Bottom-48,w,36),"processing.next",()=>helpPage++,(helpPage+1)*rows<lines.Length);
        Button(b,new(box.X+36+w*2,box.Bottom-48,w,36),"editor.cancel",()=>help=false);
    }
}
