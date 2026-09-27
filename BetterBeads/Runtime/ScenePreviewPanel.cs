#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

internal sealed class ScenePreviewPanel
{
    private readonly FrameControls controls=new();
    private Blueprint? design;
    private Texture2D? room,person;
    private bool detail,missing;
    private (int Width,int Height) footprint;
    internal bool Active=>design is not null;
    internal UiRect? Hover(int x,int y)=>controls.HitTest(x,y);
    internal void Open(Blueprint source)
    {
        design=source.Copy();footprint=SceneProduct.Footprint(design);detail=false;controls.Clear();missing=false;
        try{room=Game1.content.Load<Texture2D>("Maps/walls_and_floors");if(room.Width<32||room.Height<368)throw new InvalidOperationException();}
        catch{room=null;missing=true;}
        try{person=Game1.content.Load<Texture2D>("Characters/Abigail");if(person.Width<16||person.Height<32)throw new InvalidOperationException();}
        catch{person=null;if(SimpleCrafting.IsWeapon(design.Use))missing=true;}
    }
    internal void Close(){design=null;room=person=null;controls.Clear();}
    internal void Click(int x,int y)=>controls.TryInvoke(x,y);
    private static string T(string key,string fallback)=>ContentText.Get("simple."+key,fallback);
    internal void Draw(SpriteBatch b,UiRect frame,Action closed)
    {
        if(design is null)return;
        ArtResources.Scope="scene";controls.Clear();
        b.Draw(Game1.fadeToBlackRect,ArtResources.Rect(frame),Color.Black*0.7f);
        var p=ScenePreviewLayout.Calculate(frame);ArtResources.Panel(b,p.Dialog);
        ArtResources.TextLine(b,T("scene-title","成品效果预览"),new(p.Dialog.X+16,p.Dialog.Y+16,p.Dialog.Width-88,40),ArtResources.Ink);
        var close=new ClickableTextureComponent(ArtResources.Rect(p.Close),Game1.mouseCursors,new Rectangle(337,494,12,12),3f);
        ArtResources.CloseButton(b,close);controls.Add((p.Close,()=>{Close();closed();}));
        b.Draw(Game1.staminaRect,ArtResources.Rect(p.Stage),new Color(77,65,55));
        var view=detail?new Rectangle(16,design.Use==ProductUse.Picture?0:16,80,64):new Rectangle(0,0,96,80);
        float scale=Math.Min(p.Stage.Width/(float)view.Width,p.Stage.Height/(float)view.Height);
        float ox=p.Stage.X+(p.Stage.Width-view.Width*scale)/2,oy=p.Stage.Y+(p.Stage.Height-view.Height*scale)/2;
        void Tile(Texture2D texture,Rectangle source,Rectangle world,Color tint)
        {
            var visible=Rectangle.Intersect(world,view);if(visible.Width<=0||visible.Height<=0)return;
            var crop=new Rectangle(source.X+(visible.X-world.X)*source.Width/world.Width,source.Y+(visible.Y-world.Y)*source.Height/world.Height,
                visible.Width*source.Width/world.Width,visible.Height*source.Height/world.Height);
            var target=new Rectangle((int)(ox+(visible.X-view.X)*scale),(int)(oy+(visible.Y-view.Y)*scale),
                (int)(ox+(visible.Right-view.X)*scale)-(int)(ox+(visible.X-view.X)*scale),
                (int)(oy+(visible.Bottom-view.Y)*scale)-(int)(oy+(visible.Y-view.Y)*scale));
            b.Draw(texture,target,crop,tint);
        }
        for(int x=0;x<96;x+=16)
        {
            if(room is not null)Tile(room,new(0,0,16,48),new(x,0,16,48),Color.White);
            for(int y=48;y<80;y+=16)if(room is not null)Tile(room,new(0,336,16,16),new(x,y,16,16),Color.White);
        }
        // A world-pixel grid doubles as the fallback; every sixteen pixels is one placement tile.
        for(int x=0;x<=96;x+=16)Tile(Game1.staminaRect,new(0,0,1,1),new(x,0,1,80),new Color(200,179,139)*0.3f);
        for(int y=0;y<=80;y+=16)Tile(Game1.staminaRect,new(0,0,1,1),new(0,y,96,1),new Color(200,179,139)*0.3f);
        var product=ArtResources.SceneTexture(design);
        int density=TextureCache.Density(product.Texture),pw=product.Source.Width/density,ph=product.Source.Height/density;
        bool weapon=SimpleCrafting.IsWeapon(design.Use);
        int px=weapon?56:(96-pw)/2,py=design.Use==ProductUse.Picture?0:72-ph;
        if(weapon&&person is not null)Tile(person,new(0,0,16,32),new(24,40,16,32),Color.White);
        if(!weapon)Tile(Game1.staminaRect,new(0,0,1,1),new(px,py+ph-footprint.Height*16,footprint.Width*16,footprint.Height*16),new Color(239,203,112)*0.16f);
        Tile(product.Texture,product.Source,new(px,py,pw,ph),Color.White);
        var grid=design.Views["front"];
        ArtResources.TextLine(b,$"{grid.Width}×{grid.Height}px · "+(weapon?T("scene-person","人物等比例参照"):T("scene-footprint","占地")+$" {footprint.Width}×{footprint.Height} "+T("tiles","格")),p.Info,ArtResources.Ink,true);
        ArtResources.TextLine(b,missing?T("scene-unavailable","场景素材未加载，使用格线参照"):T("scene-note","标准场景 · 每格16像素 · 不实际摆放"),p.Note,ArtResources.MutedInk,true);
        ArtResources.ButtonText(b,p.Toggle,detail?T("scene-fit","适应场景"):T("scene-detail","放大细节"));controls.Add((p.Toggle,()=>detail=!detail));
    }
}
#endif
