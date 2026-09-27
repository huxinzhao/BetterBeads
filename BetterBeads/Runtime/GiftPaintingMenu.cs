#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

internal sealed class GiftPaintingMenu : IClickableMenu
{
    private readonly NPC recipient;
    private readonly Farmer giver;
    private readonly Item held;
    private readonly ProductSnapshot painting;
    private readonly Action<NPC,Farmer,Item,ProductSnapshot> confirm;
    private Rectangle yes,no;
    private bool released;

    public GiftPaintingMenu(NPC recipient,Farmer giver,Item held,ProductSnapshot painting,
        Action<NPC,Farmer,Item,ProductSnapshot> confirm):base(0,0,0,0,true)
    {this.recipient=recipient;this.giver=giver;this.held=held;this.painting=painting.Copy();this.confirm=confirm;}

    public override void update(GameTime time)
    {base.update(time);var pad=GamePad.GetState(PlayerIndex.One);
        released|=Mouse.GetState().LeftButton==ButtonState.Released&&Keyboard.GetState().GetPressedKeys().Length==0
            &&pad.Buttons.A==ButtonState.Released&&pad.Buttons.B==ButtonState.Released;}

    public override void draw(SpriteBatch b)
    {
        var viewport=Game1.uiViewport;
        int w=Math.Min(560,viewport.Width-24),h=Math.Min(440,viewport.Height-24);
        var panel=new UiRect((viewport.Width-w)/2,(viewport.Height-h)/2,w,h);
        b.Draw(Game1.fadeToBlackRect,new Rectangle(0,0,viewport.Width,viewport.Height),Color.Black*.7f);
        ArtResources.Panel(b,panel);
        string title=ContentText.Format("gift.confirm-title",$"将「{painting.Design.Name}」送给{recipient.displayName}？");
        ArtResources.TextLine(b,title,new(panel.X+20,panel.Y+18,w-40,32),ArtResources.Ink);
        int previewTop=panel.Y+60,previewHeight=Math.Max(80,h-180);
        ArtResources.ProductPreview(b,painting,new(panel.X+(w-previewHeight)/2,previewTop,previewHeight,previewHeight));
        string price=ContentText.Format("gift.confirm-price",$"作品估价：{held.salePrice()}金 · 赠出后无法取回");
        ArtResources.TextLine(b,price,new(panel.X+20,panel.Bottom-104,w-40,28),ArtResources.MutedInk);
        int gap=12,bw=(w-52)/2;
        no=new Rectangle(panel.X+20,panel.Bottom-64,bw,44);
        yes=new Rectangle(no.Right+gap,no.Y,bw,44);
        ArtResources.ButtonText(b,new(no.X,no.Y,no.Width,no.Height),ContentText.Get("gift.cancel","取消"));
        ArtResources.ButtonText(b,new(yes.X,yes.Y,yes.Width,yes.Height),ContentText.Get("gift.confirm","赠送画作"));
        drawMouse(b);
    }
    public override void receiveLeftClick(int x,int y,bool playSound=true)
    {
        if(!released)return;
        if(no.Contains(x,y)){exitThisMenu();return;}
        if(yes.Contains(x,y))Confirm();
    }
    public override void receiveKeyPress(Keys key)
    {if(key==Keys.Escape)exitThisMenu();else if(released&&key==Keys.Enter)Confirm();else base.receiveKeyPress(key);}
    public override void receiveGamePadButton(Buttons button)
    {if(button==Buttons.B)exitThisMenu();else if(released&&button==Buttons.A)Confirm();else base.receiveGamePadButton(button);}
    private void Confirm(){exitThisMenu();confirm(recipient,giver,held,painting);}
}
#endif
