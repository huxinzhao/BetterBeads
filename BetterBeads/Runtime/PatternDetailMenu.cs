using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;
internal sealed class PatternDetailMenu : IClickableMenu
{
    private readonly ReferencePattern pattern;
    private readonly Blueprint design;
    private readonly bool owned;
    private readonly Action back,copy;
    private readonly FrameControls controls=new();
    private int direction;
    private bool beadView;
    public PatternDetailMenu(ReferencePattern pattern,Blueprint design,bool owned,Action back,Action copy)
    {this.pattern=pattern;this.design=design;this.owned=owned;this.back=back;this.copy=copy;}
    public override void gameWindowSizeChanged(Rectangle oldBounds,Rectangle newBounds)=>controls.Clear();
    public override void receiveLeftClick(int x,int y,bool playSound=true){ArtResources.PressAt(x,y);controls.TryInvoke(x,y);}
    public override void receiveKeyPress(Keys key){if(key==Keys.Escape)back();}
    public override void receiveGamePadButton(Buttons button){if(button==Buttons.B)back();}
    public override void draw(SpriteBatch b)
    {
        ArtResources.BeginFrame();controls.Clear();
        int width=Game1.uiViewport.Width,height=Game1.uiViewport.Height;
        b.Draw(Game1.fadeToBlackRect,new Rectangle(0,0,width,height),Color.Black*0.65f);
        var l=PatternDetailLayout.Calculate(width,height);
        ArtResources.Panel(b,l.Frame);ArtResources.TextLine(b,pattern.Name,l.Title,ArtResources.Ink);
        ArtResources.ButtonText(b,l.Mode,beadView?ContentText.Get("copy.PatternDetailMenu.a12f190d18","看成品"):ContentText.Get("copy.PatternDetailMenu.7d27cae3ba","看豆盘"));controls.Add((l.Mode,()=>beadView=!beadView));
        b.Draw(Game1.staminaRect,ArtResources.Rect(l.Preview),new Color(239,238,227));
        string[] views=design.Views.Keys.ToArray();direction=Math.Clamp(direction,0,views.Length-1);
        var grid=design.Views[views[direction]];
        int step=Math.Max(1,Math.Min((l.Preview.Width-16)/grid.Width,(l.Preview.Height-16)/grid.Height));
        int ox=l.Preview.X+(l.Preview.Width-step*grid.Width)/2,oy=l.Preview.Y+(l.Preview.Height-step*grid.Height)/2;
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)
        {
            var r=new UiRect(ox+x*step,oy+y*step,step,step);
            int peg=Math.Max(1,step/8);
            if(beadView)b.Draw(Game1.staminaRect,new Rectangle(r.X+(step-peg)/2,r.Y+(step-peg)/2,peg,peg),new Color(207,211,195));
            if(grid.Cells[y*grid.Width+x] is {} cell)
            {
                var color=new Color((byte)(cell.Rgba>>24),(byte)(cell.Rgba>>16),(byte)(cell.Rgba>>8));
                if(!beadView || step<5)b.Draw(Game1.staminaRect,ArtResources.Rect(r),color);
                else ArtResources.BeadCell(b,r,l.Preview,color,false);
            }
        }
        string[] labels={ContentText.Get("copy.PatternDetailMenu.740f5e8102","正面"),ContentText.Get("copy.PatternDetailMenu.1cf1d4d0b2","右侧"),ContentText.Get("copy.PatternDetailMenu.6a24b14f33","左侧"),ContentText.Get("copy.PatternDetailMenu.2d270b6ce4","背面")};
        int tabWidth=Math.Min(80,l.Directions.Width/views.Length);
        for(int i=0;i<views.Length;i++)
        {
            int tab=i;var r=new UiRect(l.Directions.X+i*tabWidth,l.Directions.Y,tabWidth,40);
            ArtResources.Tab(b,r,labels[Math.Min(i,3)],i==direction);controls.Add((r,()=>direction=tab));
        }
        var spec=ReferencePatterns.Template(pattern.TemplateId)!;
        int count=grid.Cells.Count(c=>c is not null);
        int budget=spec.WoolBudget??Math.Max(count,Math.Max(spec.StructureBudget,spec.MinimumMaterials));
        string[] details={ContentText.Get("copy.PatternDetailMenu.00c615f156","尺寸  ")+grid.Width+"×"+grid.Height+(design.TemplateId==ProductTemplates.DetailedPicture?ContentText.Get("copy.PatternDetailMenu.69b317b50b"," · 占地2×2格"):""),ContentText.Get("copy.PatternDetailMenu.2ae572a36a","方向  ")+string.Join(" / ",views.Select((_,i)=>labels[Math.Min(i,3)])),
            ContentText.Get("copy.PatternDetailMenu.07a5e2fb39","预计用料  ")+(ClothingTemplates.IsClothing(design.Use)?ContentText.Get("copy.PatternDetailMenu.da6205e40d","羊毛豆 "):WeaponMaterials.IsWeapon(design.Use)?ContentText.Get("copy.PatternDetailMenu.4a89c0c09b","金属豆 "):ContentText.Get("copy.PatternDetailMenu.3f404ebe7e","普通拼豆 "))+budget+ContentText.Get("copy.PatternDetailMenu.09901d3de2","颗"),
            ContentText.Get("copy.PatternDetailMenu.086c3e5a40","来源  ")+pattern.Source};
        int lineHeight=l.Compact?22:34;int yy=l.Details.Y;
        foreach(string entry in details)
        {ArtResources.TextLine(b,entry,new UiRect(l.Details.X,yy,l.Details.Width,lineHeight),ArtResources.MutedInk,scale:0.84f);yy+=lineHeight;}
        if(!l.Compact)
        {
            yy+=8;
            ArtResources.TextLine(b,ContentText.Get("copy.PatternDetailMenu.e52fd481c4","作品豆色"),new UiRect(l.Details.X,yy,l.Details.Width,28),ArtResources.Ink);
            yy+=32;
            foreach(var color in grid.Cells.Where(c=>c is not null).Select(c=>c!.Rgba).Distinct().Take(7).Select((rgba,i)=>(rgba,i)))
            {
                var r=new UiRect(l.Details.X+color.i*34,yy,28,28);
                ArtResources.BeadCell(b,r,l.Details,new Color((byte)(color.rgba>>24),(byte)(color.rgba>>16),(byte)(color.rgba>>8)),false);
            }
            yy+=40;
        }
        if(!l.Compact)ArtResources.TextLine(b,owned?ContentText.Get("copy.PatternDetailMenu.cebc46741d","已收录 · 可复制后自由改色"):ContentText.Get("copy.PatternDetailMenu.0f4bdadb99","解锁：")+WorkshopGuidance.RewardName(pattern.Reward),new UiRect(l.Details.X,yy,l.Details.Width,lineHeight),ArtResources.MutedInk,scale:0.84f);
        ArtResources.ButtonText(b,l.Back,ContentText.Get("copy.PatternDetailMenu.572cf45ba4","返回"));controls.Add((l.Back,back));
        ArtResources.ButtonText(b,l.Copy,owned?ContentText.Get("copy.PatternDetailMenu.8d88ff6d6e","复制图纸"):ContentText.Get("copy.PatternDetailMenu.9630b9b14f","尚未获得"),owned,false,"primary");if(owned)controls.Add((l.Copy,copy));
        drawMouse(b);
    }
}
