#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

/// <summary>A native dialogue with a display-only hold-up pose. The craft has already committed.</summary>
internal sealed class ProductReceivedMenu : DialogueBox
{
    private readonly ProductSnapshot product;
    private readonly Farmer farmer;
    private readonly int originalFrame,originalDirection;
    private readonly ReceivedPresentation presentation=new();
    private bool poseRestored;
    public ProductReceivedMenu(ProductSnapshot snapshot,bool unsaved=false):base(ReceivedPresentation.Message(snapshot,ProductLabels.SaleLine(snapshot))+(unsaved?"\n"+ContentText.Get("simple.draft-unsaved","图纸未保存；本次游戏中可返回拼豆台继续编辑。"):""))
    {
        product=snapshot.Copy();farmer=Game1.player;
        originalFrame=farmer.FarmerSprite.CurrentFrame;originalDirection=farmer.FacingDirection;
        // Frame 57 is the installed game's holdUpItemThenMessage pose. Do not call its item-effect path.
        farmer.faceDirection(2);farmer.FarmerSprite.setCurrentFrame(57);
    }
    public override void update(GameTime time)
    {
        base.update(time);
        var mouse=Mouse.GetState();var pad=GamePad.GetState(PlayerIndex.One);
        presentation.Update(time.ElapsedGameTime.TotalSeconds,
            mouse.LeftButton==ButtonState.Released&&mouse.RightButton==ButtonState.Released
            &&Keyboard.GetState().GetPressedKeys().Length==0
            &&pad.Buttons.A==ButtonState.Released&&pad.Buttons.B==ButtonState.Released);
        if(ReferenceEquals(Game1.activeClickableMenu,this)&&farmer.FacingDirection==2)
            farmer.FarmerSprite.setCurrentFrame(57);
    }
    public override void receiveLeftClick(int x,int y,bool playSound=true){if(presentation.CanContinue)base.receiveLeftClick(x,y,playSound);}
    public override void receiveRightClick(int x,int y,bool playSound=true){if(presentation.CanContinue)base.receiveRightClick(x,y,playSound);}
    public override void receiveKeyPress(Keys key){if(presentation.CanContinue)base.receiveKeyPress(key);}
    public override void receiveGamePadButton(Buttons button){if(presentation.CanContinue)base.receiveGamePadButton(button);}
    public override bool readyToClose()=>presentation.CanContinue&&base.readyToClose();
    protected override void cleanupBeforeExit(){RestorePose();base.cleanupBeforeExit();}
    public override void emergencyShutDown(){RestorePose();base.emergencyShutDown();}
    private void RestorePose()
    {
        if(poseRestored)return;poseRestored=true;
        // Never overwrite a pose another event has taken over, or write movement/freeze flags.
        if(farmer.FacingDirection==2&&farmer.FarmerSprite.CurrentFrame==57)
        {farmer.faceDirection(originalDirection);farmer.FarmerSprite.setCurrentFrame(originalFrame);}
    }
    public override void draw(SpriteBatch b)
    {
        try
        {
            var at=Game1.GlobalToLocal(Game1.viewport,farmer.Position+new Vector2(32,-60));
            float sx=Game1.uiViewport.Width/(float)Math.Max(1,Game1.viewport.Width);
            float sy=Game1.uiViewport.Height/(float)Math.Max(1,Game1.viewport.Height);
            var bounds=ReceivedPresentation.Preview(Game1.uiViewport.Width,Game1.uiViewport.Height,yPositionOnScreen,(int)(at.X*sx),(int)(at.Y*sy));
            int rise=PlayMode.Feedback.ReducedMotion?0:(int)Math.Round((1-presentation.Reveal)*8);
            bounds=bounds with{Y=bounds.Y+rise};
            Star(b,bounds.X-5,bounds.Y+12,4);Star(b,bounds.Right+5,bounds.Bottom-16,3);
            ArtResources.ProductPreview(b,product,bounds);
            // Native border, typewriter text, continuation marker and keyboard/gamepad behavior.
            base.draw(b);
        }
        catch
        {
            RestorePose();exitThisMenu();
            Game1.addHUDMessage(new HUDMessage(ContentText.Get("simple.made-saved","制作成功")));
        }
    }
    private static void Star(SpriteBatch b,int x,int y,int radius)
    {
        for(int row=-radius;row<=radius;row++)
        {
            int half=Math.Max(0,(radius-Math.Abs(row))/2);
            b.Draw(Game1.staminaRect,new Rectangle(x-half,y+row,half*2+1,1),new Color(235,184,70));
        }
        b.Draw(Game1.staminaRect,new Rectangle(x,y-1,1,3),new Color(255,250,211));
    }
}
#endif
