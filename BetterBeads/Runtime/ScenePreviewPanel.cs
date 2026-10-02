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
    private bool seams,diagnostics;
    private ShapedWeapon? weaponShape;
    private HashSet<int> mainCells=new();
    private int explanationPage;
    internal string FeedbackScope=>diagnostics?"scene/explanation":"scene";
    private (int Width,int Height) footprint;
    internal bool Active=>design is not null;
    internal UiRect? Hover(int x,int y)=>controls.HitTest(x,y);
    internal IReadOnlyList<UiRect> ControllerTargets=>controls.Targets;
    internal void Open(Blueprint source)
    {
        design=source.Copy();footprint=SceneProduct.Footprint(design);detail=seams=diagnostics=false;explanationPage=0;controls.Clear();missing=false;
        weaponShape=SimpleCrafting.IsWeapon(design.Use)?WeaponGeometry.Evaluate(design):null;
        mainCells=weaponShape is not null?WeaponGeometry.MainComponent(design.Views["front"],design.Use,design.SwordOrientation):new();
        try{room=Game1.content.Load<Texture2D>("Maps/walls_and_floors");if(room.Width<32||room.Height<368)throw new InvalidOperationException();}
        catch{room=null;missing=true;}
        try{person=Game1.content.Load<Texture2D>("Characters/Abigail");if(person.Width<16||person.Height<32)throw new InvalidOperationException();}
        catch{person=null;if(SimpleCrafting.IsWeapon(design.Use))missing=true;}
    }
    internal void Close(){design=null;room=person=null;weaponShape=null;mainCells.Clear();controls.Clear();}
    internal void Click(int x,int y)=>controls.TryInvoke(x,y);
    private static string T(string key,string fallback)=>ContentText.Get("simple."+key,fallback);
    internal void Draw(SpriteBatch b,UiRect frame,Action closed)
    {
        if(design is null)return;
        ArtResources.Scope=FeedbackScope;controls.Clear();
        b.Draw(Game1.fadeToBlackRect,ArtResources.Rect(frame),Color.Black*0.7f);
        var p=ScenePreviewLayout.Calculate(frame);ArtResources.Panel(b,p.Dialog);
        ArtResources.TextLine(b,T("scene-title","成品效果预览"),new(p.Dialog.X+16,p.Dialog.Y+16,p.Dialog.Width-88,40),ArtResources.Ink);
        var close=new ClickableTextureComponent(ArtResources.Rect(p.Close),Game1.mouseCursors,new Rectangle(337,494,12,12),3f);
        ArtResources.CloseButton(b,close);controls.Add((p.Close,()=>{Close();closed();}));
        b.Draw(Game1.staminaRect,ArtResources.Rect(p.Stage),new Color(77,65,55));
        if(diagnostics&&weaponShape is not null){DrawWeaponExplanation(b,p);DrawOptions(b,p,true);return;}
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
        bool surface=SimpleCrafting.IsDecoration(design.Use);
        if(surface)
        {
            bool floor=design.Use==ProductUse.Flooring;
            int fromY=floor?48:0,toY=floor?80:48,span=design.Views["front"].Width/16;
            int sourceStep=16*density;
            for(int y=fromY;y<toY;y+=16)for(int x=0;x<96;x+=16)
            {
                var source=new Rectangle(product.Source.X+(x/16%span)*sourceStep,
                    product.Source.Y+((y-fromY)/16%span)*sourceStep,sourceStep,sourceStep);
                Tile(product.Texture,source,new(x,y,16,16),Color.White);
            }
            if(seams)
            {
                int repeat=design.Views["front"].Width;
                for(int x=0;x<=96;x+=repeat)Tile(Game1.staminaRect,new(0,0,1,1),new(x,fromY,1,toY-fromY),new Color(255,212,94)*.85f);
                for(int y=fromY;y<=toY;y+=repeat)Tile(Game1.staminaRect,new(0,0,1,1),new(0,y,96,1),new Color(255,212,94)*.85f);
            }
            var size=design.Views["front"].Width;
            ArtResources.TextLine(b,$"{size}×{size}px · "+T("scene-repeat","按区域左上角重复铺设"),p.Info,ArtResources.Ink,true);
            ArtResources.TextLine(b,seams?T("scene-seam-note","金线为纹样边界；铺设前会高亮覆盖区域。"):
                missing?T("scene-unavailable","场景素材未加载，使用格线参照"):T("scene-note","标准场景 · 每格16像素 · 不实际摆放"),p.Note,ArtResources.MutedInk,true);
            DrawOptions(b,p,false);
            return;
        }
        int px=weapon?56:(96-pw)/2,py=design.Use==ProductUse.Picture?0:72-ph;
        if(weapon&&person is not null)Tile(person,new(0,0,16,32),new(24,40,16,32),Color.White);
        if(!weapon)Tile(Game1.staminaRect,new(0,0,1,1),new(px,py+ph-footprint.Height*16,footprint.Width*16,footprint.Height*16),new Color(239,203,112)*0.16f);
        Tile(product.Texture,product.Source,new(px,py,pw,ph),Color.White);
        var grid=design.Views["front"];
        ArtResources.TextLine(b,$"{grid.Width}×{grid.Height}px · "+(weapon?T("scene-person","人物等比例参照"):T("scene-footprint","占地")+$" {footprint.Width}×{footprint.Height} "+T("tiles","格")),p.Info,ArtResources.Ink,true);
        ArtResources.TextLine(b,missing?T("scene-unavailable","场景素材未加载，使用格线参照"):T("scene-note","标准场景 · 每格16像素 · 不实际摆放"),p.Note,ArtResources.MutedInk,true);
        DrawOptions(b,p,weapon);
    }
    private void DrawOptions(SpriteBatch b,ScenePreviewLayout p,bool weapon)
    {
        bool extra=weapon||design is not null&&SimpleCrafting.IsDecoration(design.Use);
        var left=extra?p.Toggle with{Width=(p.Toggle.Width-8)/2}:p.Toggle;
        ArtResources.ButtonText(b,left,diagnostics?T("scene-standard","返回场景"):detail?T("scene-fit","适应场景"):T("scene-detail","放大细节"));
        controls.Add((left,()=>{if(diagnostics)diagnostics=false;else detail=!detail;}));
        if(!extra)return;
        var right=new UiRect(left.Right+8,left.Y,p.Toggle.Right-left.Right-8,left.Height);
        ArtResources.ButtonText(b,right,weapon?T("shape-explain","判定说明"):T("scene-seams","接缝标记"),selected:weapon?diagnostics:seams);
        controls.Add((right,()=>{if(weapon)diagnostics=!diagnostics;else seams=!seams;}));
    }
    private void DrawWeaponExplanation(SpriteBatch b,ScenePreviewLayout p)
    {
        var layout=WeaponExplanationLayout.Calculate(p);
        ArtResources.Panel(b,layout.Body,"inset");
        var grid=design!.Views["front"];var shape=weaponShape!;
        var picture=layout.Picture;
        float zoom=Math.Min(picture.Width/(float)grid.Width,picture.Height/(float)grid.Height);
        float left=picture.X+(picture.Width-grid.Width*zoom)/2,top=picture.Y+(picture.Height-grid.Height*zoom)/2;
        for(int i=0;i<grid.Cells.Count;i++)if(grid.Cells[i] is {} cell)
        {
            var r=new Rectangle((int)(left+i%grid.Width*zoom),(int)(top+i/grid.Width*zoom),Math.Max(1,(int)zoom),Math.Max(1,(int)zoom));
            var color=new Color((byte)(cell.Rgba>>24),(byte)(cell.Rgba>>16),(byte)(cell.Rgba>>8));
            b.Draw(Game1.staminaRect,r,color*(mainCells.Contains(i)?1f:.25f));
        }
        float rootX=left+(shape.Geometry.RootX+.5f)*zoom,rootY=top+(shape.Geometry.RootY+.5f)*zoom;
        b.Draw(Game1.staminaRect,new Rectangle((int)rootX-4,(int)rootY-1,9,2),Color.Gold);
        b.Draw(Game1.staminaRect,new Rectangle((int)rootX-1,(int)rootY-4,2,9),Color.Gold);
        ArtResources.Paragraph(b,T("shape-markers","亮色为判定主体；金色十字为分析握持端。"),layout.Legend,ArtResources.MutedInk);
        var lines=new[]{
            ContentText.Format("simple.shape-count",$"总豆数 {shape.Geometry.Count} · 主体 {shape.Geometry.MainCount} · 孤立 {shape.Geometry.Count-shape.Geometry.MainCount}"),
            ContentText.Format("simple.shape-length",$"主体长度约 {shape.Geometry.Length:0.#}px"),
            ContentText.Format("simple.shape-result",$"伤害 {shape.Stats.MinDamage}～{shape.Stats.MaxDamage} · 攻速 {shape.Stats.Speed} · 范围 ×{shape.Reach:0.##}"),
            shape.Geometry.Inertia>WeaponGeometry.Reference(design.Use).Inertia*1.25?
                T("shape-heavy-tip","重量较偏向远端，可能使挥舞更慢。"):
                T("shape-weight-note","豆数、主体长度与重量分布共同影响攻速。"),
            T("shape-range-note","孤立豆计入重量，但不增加主体攻击距离。")};
        string paragraph=string.Join("\n",lines);
        var textPages=ArtResources.ParagraphPages(paragraph,layout.Text.Width,Math.Max(1,layout.Text.Height/24));
        int pages=textPages.Length;
        explanationPage=Math.Clamp(explanationPage,0,Math.Max(0,pages-1));
        for(int i=0;i<textPages[explanationPage].Length;i++)
            ArtResources.TextLine(b,textPages[explanationPage][i],new(layout.Text.X,layout.Text.Y+i*24,layout.Text.Width,24),ArtResources.Ink);
        ArtResources.ButtonText(b,layout.Previous,"◀",explanationPage>0);
        ArtResources.ButtonText(b,layout.Next,"▶",explanationPage+1<pages);
        ArtResources.TextLine(b,$"{explanationPage+1} / {pages}",layout.Page,ArtResources.MutedInk,true);
        if(explanationPage>0)controls.Add((layout.Previous,()=>explanationPage--));
        if(explanationPage+1<pages)controls.Add((layout.Next,()=>explanationPage++));
    }
}
#endif
