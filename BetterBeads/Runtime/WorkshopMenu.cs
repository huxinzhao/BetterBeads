using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;
internal sealed record WorkshopChoice(string Label,Action Choose,bool Enabled=true,Blueprint? Preview=null);

/// <summary>A paged, fixed-type-size menu shared by commissions and reference patterns.</summary>
internal sealed class WorkshopMenu : IClickableMenu
{
    private readonly string title,description;
    private readonly Func<IReadOnlyList<WorkshopChoice>> choices;
    private readonly IClickableMenu? parent;
    private readonly Action? back;
    private readonly FrameControls controls=new();
    private int page;
    public WorkshopMenu(string title,string description,Func<IReadOnlyList<WorkshopChoice>> choices,IClickableMenu? parent=null,Action? back=null)
    {this.title=title;this.description=description;this.choices=choices;this.parent=parent;this.back=back;}
    private void Back(){controls.Clear();if(back is not null)back();else Game1.activeClickableMenu=parent;}
    public override void gameWindowSizeChanged(Rectangle oldBounds,Rectangle newBounds)=>controls.Clear();
    public override void receiveLeftClick(int x,int y,bool playSound=true){ArtResources.PressAt(x,y);controls.TryInvoke(x,y);}
    public override void receiveKeyPress(Keys key){if(key==Keys.Escape)Back();}
    public override void receiveGamePadButton(Buttons button){if(button==Buttons.B)Back();}
    public override void receiveScrollWheelAction(int direction){page=Math.Max(0,page-Math.Sign(direction));controls.Clear();}
    public override void draw(SpriteBatch b)
    {
        ArtResources.BeginFrame();controls.Clear();
        b.Draw(Game1.fadeToBlackRect,new Rectangle(0,0,Game1.uiViewport.Width,Game1.uiViewport.Height),Color.Black*0.65f);
        int width=Math.Min(720,Game1.uiViewport.Width-32);
        float scale=18f/Game1.smallFont.MeasureString(ContentText.Get("copy.WorkshopMenu.16e7a38c73","国Ag")).Y;
        string[] lines=Game1.parseText(description,Game1.smallFont,(int)((width-48)/scale)).Split('\n');
        var rows=choices();
        var l=WorkshopMenuLayout.Calculate(Game1.uiViewport.Width,Game1.uiViewport.Height,lines.Length,rows.Count);
        ArtResources.Panel(b,l.Frame);ArtResources.TextLine(b,title,l.Title,ArtResources.Ink);
        var pages=WorkshopPages.Calculate(page,lines.Length,rows.Count,l.Count);page=pages.Page;
        for(int i=0;i<pages.DescriptionCount && pages.DescriptionStart+i<lines.Length;i++)
            ArtResources.TextLine(b,lines[pages.DescriptionStart+i],new(l.Description.X,l.Description.Y+i*24,l.Description.Width,24),ArtResources.MutedInk,scale:0.84f);
        if(lines.Length>5)ArtResources.TextLine(b,ContentText.Format("copy.WorkshopMenu.ec25e967e0",$"说明 {pages.DescriptionStart/4+1}/{(lines.Length+3)/4} · 使用下方翻页阅读"),new(l.Description.X,l.Description.Y+96,l.Description.Width,24),ArtResources.MutedInk,scale:0.84f);
        for(int i=0;i<l.Count && pages.ChoiceStart+i<rows.Count;i++)
        {
            var entry=rows[pages.ChoiceStart+i];var r=new UiRect(l.Rows.X,l.Rows.Y+i*52,l.Rows.Width,44);
            if(entry.Preview?.Views.Values.FirstOrDefault() is {} grid)
            {
                ArtResources.Button(b,r,entry.Enabled);float zoom=Math.Min(30f/grid.Width,30f/grid.Height);
                for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)if(grid.Cells[y*grid.Width+x] is {} cell)
                    b.Draw(Game1.staminaRect,new Rectangle(r.X+8+(int)(x*zoom),r.Y+6+(int)(y*zoom),Math.Max(1,(int)zoom),Math.Max(1,(int)zoom)),
                        new Color((byte)(cell.Rgba>>24),(byte)(cell.Rgba>>16),(byte)(cell.Rgba>>8)));
                ArtResources.TextLine(b,entry.Label,new(r.X+48,r.Y+8,r.Width-60,28),ArtResources.Ink,scale:0.84f);
            }
            else ArtResources.ButtonText(b,r,entry.Label,entry.Enabled);
            if(entry.Enabled)controls.Add((r,entry.Choose));
            if(r.Contains(Game1.getMouseX(true),Game1.getMouseY(true)) && Game1.smallFont.MeasureString(entry.Label).X*scale>r.Width-48)
                IClickableMenu.drawHoverText(b,entry.Label,Game1.smallFont);
        }
        void Button(UiRect r,string label,Action action,bool enabled=true){ArtResources.ButtonText(b,r,label,enabled);if(enabled)controls.Add((r,action));}
        Button(l.Previous,ContentText.Get("copy.WorkshopMenu.c9b9ae7a61","上一页"),()=>{page--;controls.Clear();},page>0);Button(l.Next,ContentText.Get("copy.WorkshopMenu.8a8542f696","下一页"),()=>{page++;controls.Clear();},page+1<pages.Count);Button(l.Back,ContentText.Get("copy.WorkshopMenu.572cf45ba4","返回"),Back);
        drawMouse(b);
    }
}
