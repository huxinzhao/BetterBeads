using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

/// <summary>All custom art uses public content keys, so Content Patcher can edit it.
/// Textures belong to SMAPI's content manager; never dispose a shared texture here.</summary>
internal static class ArtResources
{
    public const string Prefix="Mods/xinzh.BetterBeads/";
    private static IModHelper helper=null!;
    private static IMonitor monitor=null!;
    private static readonly HashSet<string> warned=new();
    private static ArtDefinition? cachedDefinition;
    private static Texture2D? beadSurface;
    private static uint[]? framePixels;
    public static int FrameRevision {get;private set;}
    private static readonly Dictionary<(string Scope,Rectangle Rect,string Id),(ButtonMotion Motion,double Seen)> buttonMotion=new();
    private static readonly UiFeedbackState feedback=new();
    private static bool feedbackManaged;
    private static (string Scope,Rectangle Rect,string Id)? pressedTarget;
    private static readonly List<((string Scope,Rectangle Rect,string Id) Key,bool Enabled)> targets=new();
    private static (string Scope,Rectangle Rect,string Id)? activeTarget;
    public static string Scope {get;set;}="";
    public static bool InteractiveScope=>Scope==activeScope;
    private static string activeScope="";
    private static string controlId="";
    private static string? hint;
    private static readonly HoverDelay tooltip=new();
    public static void PrepareFeedback(string scope,UiRect? target)
    {
        activeScope=scope;Scope=scope;activeTarget=Resolve(target);hint=null;
        if(activeTarget?.Scope!=scope)activeTarget=null;
        var sound=feedback.Observe(activeTarget is {} k?new UiRect(k.Rect.X,k.Rect.Y,k.Rect.Width,k.Rect.Height):null,WorkbenchUi.MouseX,WorkbenchUi.MouseY,animationTime,activeTarget is {} id?id.Scope+"/"+id.Id:null);
        feedbackManaged=true;targets.Clear();
        if(sound==HoverFeedback.Enter)UiSounds.Hover(true);else if(sound==HoverFeedback.Leave)UiSounds.Hover(false);
    }
    private static (string Scope,Rectangle Rect,string Id)? Resolve(UiRect? target)
    {
        if(target is not {} r)return null;
        for(int i=targets.Count-1;i>=0;i--)if(targets[i].Enabled&&targets[i].Key.Rect==Rect(r))return targets[i].Key;
        return null;
    }
    public static void Hint(string value){if(Scope==activeScope)hint=value;}
    public static void DrawHint(SpriteBatch b)
    {
        if(tooltip.Ready(hint is null?null:activeScope+"/"+hint,animationTime)&&hint is not null)IClickableMenu.drawHoverText(b,hint,Game1.smallFont);
    }
    public static bool Truncated(string label,int width)=>UiTextCache.Measure(Game1.smallFont,label).X*TextHeight(true)/UiTextCache.Measure(Game1.smallFont,ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y>width;
    private static readonly DeferredCache<(Blueprint Design,int Revision,bool Framed,bool Trim),(Texture2D Texture,Rectangle Source)> previews=new(64);
    private static readonly DeferredCache<(ProductSnapshot Product,int Revision),(Texture2D Texture,Rectangle Source)> productPreviews=new(64);
    public static void ReleasePreviewRetired(){previews.ReleaseRetired(p=>p.Texture.Dispose());productPreviews.ReleaseRetired(p=>p.Texture.Dispose());}
    private static double animationTime,animationDelta,nextMotionCleanup;
    private static readonly List<(string Scope,Rectangle Rect,string Id)> motionScratch=new();
    public static void BeginFrame()
    {
        if(!feedbackManaged)targets.Clear();
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        animationDelta=animationTime==0?0:Math.Clamp(now-animationTime,0,0.1);animationTime=now;
        if(Game1.input.GetMouseState().LeftButton!=ButtonState.Pressed)pressedTarget=null;
        if(buttonMotion.Count>512)buttonMotion.Clear();
        if(now>=nextMotionCleanup)
        {
            motionScratch.Clear();foreach(var entry in buttonMotion)if(now-entry.Value.Seen>2)motionScratch.Add(entry.Key);
            foreach(var key in motionScratch)buttonMotion.Remove(key);
            motionScratch.Clear();nextMotionCleanup=now+0.5;
        }
    }
    public static void ResetMotion(){buttonMotion.Clear();motionScratch.Clear();nextMotionCleanup=0;animationTime=0;feedback.Reset();feedbackManaged=false;pressedTarget=null;activeTarget=null;targets.Clear();tooltip.Reset();hint=null;}
    public static void Press(UiRect? target)
    {
        var key=Resolve(target);if(key is not {} k)return;
        pressedTarget=k;var entry=buttonMotion.GetValueOrDefault(k);
        buttonMotion[k]=(entry.Motion with{Press=1,Spring=1,Velocity=0},entry.Seen);
        UiSounds.Click();
    }
    public static void PressAt(int x,int y)
    {
        motionScratch.Clear();foreach(var key in buttonMotion.Keys)if(key.Rect.Contains(x,y))motionScratch.Add(key);
        foreach(var rect in motionScratch)
        {
            var entry=buttonMotion[rect];buttonMotion[rect]=(entry.Motion with{Press=1,Spring=1,Velocity=0},entry.Seen);
        }
    }
    private static ButtonMotion Motion(Rectangle r,bool enabled)
    {
        var key=(Scope,r,controlId);targets.Add((key,enabled));
        bool hover=enabled&&r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY)&&(!feedbackManaged||activeTarget==key);
        bool down=hover&&Game1.input.GetMouseState().LeftButton==ButtonState.Pressed&&(!feedbackManaged||pressedTarget==key);
        var previous=buttonMotion.GetValueOrDefault(key);
        var motion=previous.Motion.Step(hover,down,enabled,previous.Seen==animationTime?0:animationDelta);
        buttonMotion[key]=(motion,animationTime);return motion;
    }
    public static Color ButtonInk(UiRect r,bool enabled=true)
    {
        float hover=0;
        for(int i=targets.Count-1;i>=0;i--)if(targets[i].Key.Scope==Scope&&targets[i].Key.Rect==Rect(r)){hover=buttonMotion.GetValueOrDefault(targets[i].Key).Motion.Hover;break;}
        return enabled?Color.Lerp(Ink,new Color(136,62,25),Math.Clamp(hover,0,1)):MutedInk;
    }
    public static void CloseButton(SpriteBatch b,ClickableTextureComponent button)
    {
        var r=button.bounds;var motion=Motion(r,true);var face=motion.Face(new(r.X,r.Y,r.Width,r.Height+4),reducedMotion:PlayMode.Feedback.ReducedMotion);
        b.Draw(button.texture,new Rectangle(face.X+1,face.Y+3,face.Width,face.Height),button.sourceRect,Color.Black*0.2f);
        b.Draw(button.texture,Rect(face),button.sourceRect,Color.Lerp(Color.White,new Color(255,229,184),motion.Hover));
    }
    private static readonly Dictionary<string,string> files=new()
    {
        ["PaintingFrame"]="PaintingFrame.png",
        ["Pipette"]="Pipette.png",
        ["Workbench"]="Workbench.png",["SourceBeads"]="Beads.png",["Ui"]="Ui.png",
        ["Icons"]="Icons.png",["Status"]="Status.png",["Board"]="Board.png",
        ["PicturePlaceholder"]="Products/Picture.png",["WoodOrnamentPlaceholder"]="Products/WoodOrnament.png",
        ["StoneStatuePlaceholder"]="Products/StoneStatue.png",["SwordPlaceholder"]="Products/Sword.png",
        ["DaggerPlaceholder"]="Products/Dagger.png",["HammerPlaceholder"]="Products/Hammer.png",
        ["HatPlaceholder"]="Products/Hat.png"
    };
    public static void Register(IModHelper modHelper,IMonitor modMonitor)
    {
        helper=modHelper;monitor=modMonitor;
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>{UiTextCache.Clear();parsedColors.Clear();beadSurface?.Dispose();beadSurface=null;previews.Clear();productPreviews.Clear();ReleasePreviewRetired();};
        helper.Events.Display.Rendered+=(_,_)=>ReleasePreviewRetired();
        helper.Events.Content.AssetRequested+=(_,e)=>
        {
            if(e.NameWithoutLocale.IsEquivalentTo(Prefix+"Art"))
                e.LoadFromModFile<ArtDefinition>("assets/art.json",AssetLoadPriority.Low);
            foreach(var pair in files)if(e.NameWithoutLocale.IsEquivalentTo(Prefix+pair.Key))
                e.LoadFromModFile<Texture2D>("assets/"+pair.Value,AssetLoadPriority.Low);
        };
        helper.Events.Content.AssetsInvalidated+=(_,e)=>
        {
            UiTextCache.Clear();parsedColors.Clear();
            if(e.NamesWithoutLocale.Any(n=>n.IsEquivalentTo(Prefix+"Art"))){cachedDefinition=null;FrameRevision++;}
            if(e.NamesWithoutLocale.Any(n=>n.IsEquivalentTo(Prefix+"PaintingFrame"))){framePixels=null;FrameRevision++;}
            if(e.NamesWithoutLocale.Any(n=>n.IsEquivalentTo(Prefix+"Art") || n.IsEquivalentTo(Prefix+"SourceBeads")))
                helper.GameContent.InvalidateCache(Prefix+"Beads");
        };
        helper.ConsoleCommands.Add("betterbeads_reload_art","Reload visual assets and assets/art.json; no save data is changed.",(_,_)=>
        {
            warned.Clear();
            cachedDefinition=null;
            helper.GameContent.InvalidateCache(Prefix+"Art");
            foreach(string key in files.Keys)helper.GameContent.InvalidateCache(Prefix+key);
            helper.GameContent.InvalidateCache(Prefix+"Beads");
            monitor.Log("Better Beads art invalidated; it will reload on the next draw.",LogLevel.Info);
        });
    }
    private static void Warn(string key,Exception? ex=null)
    {if(warned.Add(key))monitor.Log($"Art resource '{key}' is missing or invalid; using a visual fallback. {ex?.Message}",LogLevel.Warn);}
    public static ArtDefinition Definition
    {
        get
        {
            if(cachedDefinition is not null)return cachedDefinition;
            try{var d=helper.GameContent.Load<ArtDefinition>(Prefix+"Art");if(d.IsValid())return cachedDefinition=d;}
            catch(Exception ex){Warn("Art",ex);}
            Warn("Art");return cachedDefinition=new();
        }
    }
    private static Texture2D? Get(string name)
    {
        try{return helper.GameContent.Load<Texture2D>(Prefix+name);}
        catch(Exception ex){Warn(name,ex);return null;}
    }
    public static uint[] PaintingFrame()
    {
        if(framePixels is not null)return framePixels;
        var t=Get("PaintingFrame");if(t is null || t.Width!=8 || t.Height!=8)return framePixels=PaintingArt.DefaultFrame;
        var c=new Color[64];t.GetData(c);return framePixels=c.Select(p=>((uint)p.R<<24)|((uint)p.G<<16)|((uint)p.B<<8)|p.A).ToArray();
    }
    public static void DesignPreview(SpriteBatch b,Blueprint design,UiRect area,bool framed=true,bool trimTransparent=false)
    {
        // Callers provide detached, immutable display snapshots. A new snapshot or frame revision is a new key.
        var entry=previews.Get((design,FrameRevision,framed,trimTransparent),createPreview);
        float z=Math.Min(area.Width/(float)entry.Source.Width,area.Height/(float)entry.Source.Height);
        int w=Math.Max(1,(int)(entry.Source.Width*z)),h=Math.Max(1,(int)(entry.Source.Height*z));
        b.Draw(entry.Texture,new Rectangle(area.X+(area.Width-w)/2,area.Y+(area.Height-h)/2,w,h),entry.Source,Color.White);
    }
    private static readonly Func<(Blueprint Design,int Revision,bool Framed,bool Trim),(Texture2D Texture,Rectangle Source)> createPreview=CreatePreview;
    internal static (Texture2D Texture,Rectangle Source) SceneTexture(Blueprint design)=>previews.Get((design,FrameRevision,true,false),createPreview);
    internal static (Texture2D Texture,Rectangle Source) SceneTexture(ProductSnapshot product)=>productPreviews.Get((product,FrameRevision),
        key=>BuildPreview(FurnitureFinish.Compose(key.Product,PaintingFrame()),FurnitureFinish.IsNew(key.Product.FurnitureVariantId),false));
    internal static void ProductPreview(SpriteBatch b,ProductSnapshot product,UiRect area)
    {
        if(product.Design.Use is not (ProductUse.Picture or ProductUse.WoodFurniture))
        {DesignPreview(b,product.Design,area,trimTransparent:true);return;}
        var entry=SceneTexture(product);float scale=Math.Min(area.Width/(float)entry.Source.Width,area.Height/(float)entry.Source.Height);
        int w=(int)(entry.Source.Width*scale),h=(int)(entry.Source.Height*scale);
        b.Draw(entry.Texture,new Rectangle(area.X+(area.Width-w)/2,area.Y+(area.Height-h)/2,w,h),entry.Source,Color.White);
    }
    private static (Texture2D Texture,Rectangle Source) CreatePreview((Blueprint Design,int Revision,bool Framed,bool Trim) key)
    {
        var design=key.Design;var grid=design.Views["front"];
#if BEADS_LITE
        if(SimpleCrafting.IsDecoration(design.Use))return BuildDecorationPreview(design);
#endif
        bool fused=false;
#if BEADS_LITE
        if(key.Framed&&SimpleCrafting.Supported(design)){grid=SceneProduct.Compose(design,PaintingFrame());fused=FurnitureFinish.IsNew(FurnitureFinish.Variant(design));}
        else if(key.Framed&&design.Use==ProductUse.Picture)grid=PaintingArt.Compose(grid,PaintingFrame());
#else
        if(key.Framed&&design.Use==ProductUse.Picture)grid=PaintingArt.Compose(grid,PaintingFrame());
#endif
        return BuildPreview(grid,fused,key.Trim);
    }
#if BEADS_LITE
    private static (Texture2D Texture,Rectangle Source) BuildDecorationPreview(Blueprint design)
    {
        var grid=design.Views["front"];
        var finish=BeadFinish.Bake(grid,Definition,false);
        uint background=design.BackgroundRgba;
        var pixels=finish.Pixels.Select(c=>
        {
            if((c&255)==0)c=background;
            return new Color((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);
        }).ToArray();
        var texture=new Texture2D(Game1.graphics.GraphicsDevice,finish.Width,finish.Height);
        try{texture.SetData(pixels);}catch{texture.Dispose();throw;}
        TextureCache.MarkDense(texture);
        return(texture,new Rectangle(0,0,finish.Width,finish.Height));
    }
#endif
    private static (Texture2D Texture,Rectangle Source) BuildPreview(BeadGrid grid,bool fused,bool trim)
    {
        int minX=grid.Width,minY=grid.Height,maxX=-1,maxY=-1;
        var pixels=new Color[grid.Width*grid.Height];
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)if(grid.Cells[y*grid.Width+x] is {} c)
        {
            pixels[y*grid.Width+x]=new Color((byte)(c.Rgba>>24),(byte)(c.Rgba>>16),(byte)(c.Rgba>>8),(byte)c.Rgba);
            minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
        }
        if(!trim||maxX<minX){minX=0;minY=0;maxX=grid.Width-1;maxY=grid.Height-1;}
        int density=fused?BeadFinish.Density:1;
        if(fused)pixels=BeadFinish.Bake(grid,Definition,trim).Pixels.Select(c=>new Color((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c)).ToArray();
        var texture=new Texture2D(Game1.graphics.GraphicsDevice,grid.Width*density,grid.Height*density);
        try{texture.SetData(pixels);}catch{texture.Dispose();throw;}
        if(fused)TextureCache.MarkDense(texture);
        return(texture,new Rectangle(minX*density,minY*density,(maxX-minX+1)*density,(maxY-minY+1)*density));
    }
    public static void InventoryPreviewSlot(SpriteBatch b,UiRect slot)
    {
        if(Game1.menuTexture is {} menu)
            b.Draw(menu,Rect(slot),Game1.getSourceRectForStandardTileSheet(menu,10),Color.White);
        else b.Draw(Game1.staminaRect,Rect(slot),new Color(245,215,163));
        // The inventory tile's center can be nearly transparent. Keep its slot edge visible
        // around pale paintings without turning this display into a clickable button.
        b.Draw(Game1.staminaRect,new Rectangle(slot.X,slot.Y,slot.Width,3),new Color(113,73,42));
        b.Draw(Game1.staminaRect,new Rectangle(slot.X,slot.Bottom-3,slot.Width,3),new Color(113,73,42));
        b.Draw(Game1.staminaRect,new Rectangle(slot.X,slot.Y,3,slot.Height),new Color(113,73,42));
        b.Draw(Game1.staminaRect,new Rectangle(slot.Right-3,slot.Y,3,slot.Height),new Color(113,73,42));
        b.Draw(Game1.staminaRect,new Rectangle(slot.X+4,slot.Y+4,slot.Width-8,1),new Color(255,237,191));
    }
    private static readonly BoundedMemo<string,Color> parsedColors=new(64);
    private static readonly Func<string,Color> parseColor=hex=>new Color(byte.Parse(hex.AsSpan(0,2),System.Globalization.NumberStyles.HexNumber),byte.Parse(hex.AsSpan(2,2),System.Globalization.NumberStyles.HexNumber),byte.Parse(hex.AsSpan(4,2),System.Globalization.NumberStyles.HexNumber));
    public static Color Parse(string color)=>parsedColors.Get(color,parseColor);
    public static Color Ink=>Parse(Definition.Ink);
    public static Color MutedInk=>Parse(Definition.MutedInk);
    public static Color Canvas=>Parse(Definition.Canvas);
    public static Color CheckerLight=>Parse(Definition.CheckerLight);
    public static Color CheckerDark=>Parse(Definition.CheckerDark);
    public static Rectangle Rect(UiRect r)=>new(r.X,r.Y,r.Width,r.Height);
    public static void DialogueBox(SpriteBatch b,UiRect r)
    {
        if(Game1.menuTexture is null){Panel(b,r);return;}
        IClickableMenu.drawTextureBox(b,Game1.menuTexture,new Rectangle(0,256,60,60),r.X,r.Y,r.Width,r.Height,Color.White,0.3f,false);
    }
    public static void Panel(SpriteBatch b,UiRect r,string style="panel")=>Panel(b,Rect(r),style);
    public static void Panel(SpriteBatch b,Rectangle r,string style="panel")
    {
        if(r.Width<=0 || r.Height<=0)return;
        if(style=="panel")b.Draw(Game1.staminaRect,new Rectangle(r.X+5,r.Y+7,r.Width,r.Height),Color.Black*0.16f);
        if(style is "inset" or "accent")
        {
            b.Draw(Game1.staminaRect,r,new Color(236,186,119));
            b.Draw(Game1.staminaRect,new Rectangle(r.X,r.Y,r.Width,1),new Color(147,93,45)*0.45f);
            b.Draw(Game1.staminaRect,new Rectangle(r.X,r.Bottom-1,r.Width,1),new Color(255,225,174)*0.7f);
            return;
        }
        if(style.StartsWith("button-") && style!="button-primary")
        {
            var borderColor=new Color(156,102,51);var fill=style=="button-disabled"?new Color(213,183,140):new Color(249,203,141);
            b.Draw(Game1.staminaRect,r,borderColor);
            b.Draw(Game1.staminaRect,new Rectangle(r.X+2,r.Y+2,Math.Max(1,r.Width-4),Math.Max(1,r.Height-4)),fill);
            
            return;
        }
        if(Definition.UseNativeUi && Game1.menuTexture is not null && (style is "panel" or "button-primary"))
        {
            bool button=style.StartsWith("button-");
            float scale=button?(r.Height<30?0.2f:0.3f):style=="panel"?(r.Width<500?0.6f:0.8f):0.4f;
            var tint=style=="button-disabled"?new Color(192,178,153):Color.White;
            IClickableMenu.drawTextureBox(b,Game1.menuTexture,new Rectangle(0,256,60,60),r.X,r.Y,r.Width,r.Height,tint,scale,false);
            int rim=(int)(20*scale);
            if(style is "inset" or "accent")
            {
                b.Draw(Game1.staminaRect,new Rectangle(r.X+rim,r.Y+rim,Math.Max(1,r.Width-2*rim),Math.Max(1,r.Height-2*rim)),new Color(239,207,157));
                b.Draw(Game1.staminaRect,new Rectangle(r.X+rim,r.Y+rim,Math.Max(1,r.Width-2*rim),2),new Color(152,101,55)*0.55f);
            }
            if(style=="button-pressed")
                b.Draw(Game1.staminaRect,new Rectangle(r.X+rim,r.Bottom-rim-3,Math.Max(1,r.Width-rim*2),3),new Color(75,111,64));
            return;
        }
        if(style=="button-primary")style="button-hover";
        var tex=Get(style=="board"?"Board":"Ui");var d=Definition;int index=0;
        if(tex is null || (style!="board" && !d.Ui.TryGetValue(style,out index)) || tex.Width%24!=0 || !ArtDefinition.Fits(index,tex.Width/24,24,tex.Width,tex.Height))
        {b.Draw(Game1.staminaRect,r,new Color(247,232,200));return;}
        int sx=index%(tex.Width/24)*24,sy=index/(tex.Width/24)*24;
        // Corners never stretch; edges repeat rather than blur their pixel clusters.
        int edge=Math.Min(8,Math.Min(r.Width/2,r.Height/2));
        int[] dx={r.X,r.X+edge,r.Right-edge},dy={r.Y,r.Y+edge,r.Bottom-edge};
        int[] dw={edge,r.Width-2*edge,edge},dh={edge,r.Height-2*edge,edge};
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)
        {
            if(dw[x]<=0 || dh[y]<=0)continue;
            // The solid center may stretch; borders/corners retain their source resolution.
            if(x==1 && y==1){b.Draw(tex,new Rectangle(dx[x],dy[y],dw[x],dh[y]),new Rectangle(sx+8,sy+8,8,8),Color.White);continue;}
            int stepX=x==1?8:dw[x],stepY=y==1?8:dh[y];
            for(int yy=0;yy<dh[y];yy+=stepY)for(int xx=0;xx<dw[x];xx+=stepX)
            {
                int w=Math.Min(stepX,dw[x]-xx),h=Math.Min(stepY,dh[y]-yy);
                b.Draw(tex,new Rectangle(dx[x]+xx,dy[y]+yy,w,h),new Rectangle(sx+x*8,sy+y*8,w,h),Color.White);
            }
        }
    }
    public static UiRect Button(SpriteBatch b,UiRect r,bool enabled=true,bool selected=false,bool primary=false,string identity="")=>Button(b,Rect(r),enabled,selected,primary,identity);
    public static UiRect Button(SpriteBatch b,Rectangle r,bool enabled=true,bool selected=false,bool primary=false,string identity="")
    {
        controlId=identity;var motion=Motion(r,enabled);controlId="";
        var face=motion.Face(new(r.X,r.Y,r.Width,r.Height),selected&&enabled,(int)TextHeight(true)+16,PlayMode.Feedback.ReducedMotion);
        if(r.Width>6 && r.Height>10)
        {
            b.Draw(Game1.staminaRect,new Rectangle(r.X+3,r.Y+5,r.Width-3,r.Height-5),Color.Black*(enabled?0.18f:0.08f));
            int sink=(int)Math.Round(2*motion.Press);
            b.Draw(Game1.staminaRect,new Rectangle(face.X+1,r.Y+4+sink,face.Width-2,Math.Max(1,r.Height-5-sink)),enabled?new Color(112,83,58):new Color(159,144,120));
        }
        Panel(b,face,!enabled?"button-disabled":primary?"button-primary":selected?"button-pressed":"button-normal");
        if(selected && enabled)
        {
            b.Draw(Game1.staminaRect,Rect(face),new Color(91,54,28));
            b.Draw(Game1.staminaRect,new Rectangle(face.X+2,face.Y+2,face.Width-4,face.Height-4),new Color(224,165,67));
            b.Draw(Game1.staminaRect,new Rectangle(face.X+3,face.Y+3,face.Width-6,3),new Color(130,80,35));
            CheckMark(b,face.X+3,face.Y+face.Height-9);
        }
        if(motion.Hover>0.01f && enabled)
            b.Draw(Game1.staminaRect,new Rectangle(face.X+3,face.Y+3,Math.Max(1,face.Width-6),Math.Max(1,face.Height-6)),new Color(255,250,224)*(motion.Hover*0.18f));
        if(enabled&&motion.Hover>0.01f)
        {
            var rim=Color.Lerp(selected?new Color(91,54,28):new Color(156,102,51),new Color(114,57,28),motion.Hover);
            b.Draw(Game1.staminaRect,new Rectangle(face.X,face.Y,face.Width,2),rim);
            b.Draw(Game1.staminaRect,new Rectangle(face.X,face.Bottom-2,face.Width,2),rim);
            b.Draw(Game1.staminaRect,new Rectangle(face.X,face.Y,2,face.Height),rim);
            b.Draw(Game1.staminaRect,new Rectangle(face.Right-2,face.Y,2,face.Height),rim);
        }
        if(face.Width>12 && face.Height>8)
        {
            b.Draw(Game1.staminaRect,new Rectangle(face.X+5,face.Y+2,face.Width-10,1),Color.White*(selected?0.3f:0.6f));
            b.Draw(Game1.staminaRect,new Rectangle(face.X+4,face.Bottom-2,face.Width-8,1),new Color(91,69,48)*0.35f);
        }
        return face;
    }
    public static float TextHeight(bool small)=>WorkbenchUi.Active?20f:Game1.uiViewport.Width>=900 && Game1.uiViewport.Height>=640
        ? (small?22f:24f) : (small?18f:20f);
    public static void TextLine(SpriteBatch b,string label,UiRect r,Color color,bool centered=false,float scale=0.92f)
    {
        if(r.Width<=0 || r.Height<=0)return;
        var font=Game1.smallFont;
        scale=TextHeight(scale<0.9f)/UiTextCache.Measure(font,ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y;
        if(UiTextCache.Measure(font,label).X*scale>r.Width)
        {
            if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))Hint(label);
            label=UiTextCache.Ellipsize(font,label,r.Width,scale);
        }
        var size=UiTextCache.Measure(font,label)*scale;
        b.DrawString(font,label,new Vector2((int)(r.X+(centered?(r.Width-size.X)/2:0)),(int)(r.Y+(r.Height-size.Y)/2)),color,0,Vector2.Zero,scale,SpriteEffects.None,0);
    }
    internal static string[] ParagraphLines(string text,int width)
    {
        float scale=TextHeight(true)/UiTextCache.Measure(Game1.smallFont,ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y;
        return UiTextCache.Wrap(Game1.smallFont,text,width,scale);
    }
    internal static string[][] ParagraphPages(string text,int width,int rows)
    {
        float scale=TextHeight(true)/UiTextCache.Measure(Game1.smallFont,ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y;
        return UiTextCache.Pages(Game1.smallFont,text,width,scale,rows);
    }
    internal static void Paragraph(SpriteBatch b,string text,UiRect area,Color color)
    {
        var lines=ParagraphLines(text,area.Width);int count=Math.Min(lines.Length,area.Height/24);
        for(int i=0;i<count;i++)TextLine(b,lines[i],new(area.X,area.Y+i*24,area.Width,24),color);
        if(count<lines.Length&&area.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))Hint(text);
    }
    public static void ButtonText(SpriteBatch b,UiRect r,string label,bool enabled=true,bool selected=false,string iconKey="")
    {
        if(r.Height<24)
        {
            if(enabled && r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))
                b.Draw(Game1.staminaRect,Rect(r),Color.White*0.12f);
            TextLine(b,label,new(r.X+12,r.Y+8,r.Width-24,r.Height-16),enabled?Ink:MutedInk,true,0.84f);return;
        }
        var face=Button(b,r,enabled,selected,iconKey=="primary",label);
        if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY)&&Truncated(label,r.Width-24))Hint(label);
        float scale=TextHeight(true)/UiTextCache.Measure(Game1.smallFont,ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y;
        int padding=12,verticalPadding=8;
        // Keep the entire icon + label group centered; suppress optional icons before squeezing text.
        int textWidth=(int)Math.Ceiling(UiTextCache.Measure(Game1.smallFont,label).X*scale);
        bool icon=Definition.ShowIcons && iconKey.Length>0 && iconKey!="primary" && face.Height>=24 && textWidth+24<=r.Width-padding*2;
        int groupWidth=textWidth+(icon?24:0),left=face.X+Math.Max(padding,(face.Width-groupWidth)/2);
        if(icon)
        {
            string id=iconKey.StartsWith("editor.")?iconKey[7..]:iconKey;
            id=id switch{"make" or "manufacture" or "iron-confirm"=>"iron","overlay" or "materials"=>"material",_=>id};
            icon=Icon(b,id,new Rectangle(left,face.Y+(face.Height-16)/2,16,16));
        }
        var content=icon?new UiRect(left+24,face.Y+verticalPadding,Math.Max(1,face.Right-padding-left-24),face.Height-verticalPadding*2)
            :new UiRect(face.X+padding,face.Y+verticalPadding,Math.Max(1,face.Width-padding*2),face.Height-verticalPadding*2);
        TextLine(b,label,content,ButtonInk(r,enabled),!icon,0.84f);
    }
    public static void Tab(SpriteBatch b,UiRect r,string label,bool selected)
    {
        var face=Button(b,r,selected:selected,identity:label);
        TextLine(b,label,new(face.X+8,face.Y+8,face.Width-16,face.Height-16),ButtonInk(r),true,0.84f);
    }
    private static void CheckMark(SpriteBatch b,int x,int y)
    {
        for(int i=0;i<3;i++)b.Draw(Game1.staminaRect,new Rectangle(x+i,y+i,2,2),new Color(74,43,24));
        for(int i=0;i<5;i++)b.Draw(Game1.staminaRect,new Rectangle(x+2+i,y+2-i,2,2),new Color(74,43,24));
    }
    public static UiRect BeadSlot(SpriteBatch b,UiRect r,bool selected,bool hovered=false)
    {
        var motion=Motion(Rect(r),true);
        r=motion.Face(r with{Height=r.Height+4},reducedMotion:PlayMode.Feedback.ReducedMotion);
        hovered=motion.Hover>0.1f;
        b.Draw(Game1.staminaRect,Rect(r),selected?new Color(83,48,25):new Color(181,128,64));
        b.Draw(Game1.staminaRect,new Rectangle(r.X+2,r.Y+2,r.Width-4,r.Height-4),selected?new Color(255,227,146):hovered?new Color(255,229,185):new Color(239,196,136));
        b.Draw(Game1.staminaRect,new Rectangle(r.X+4,r.Y+4,r.Width-8,r.Height-8),new Color(220,180,126));
        if(selected){b.Draw(Game1.staminaRect,new Rectangle(r.Right-7,r.Y,7,7),new Color(255,234,165));CheckMark(b,r.Right-7,r.Y+2);}
        return r;
    }
    public static void PaletteBead(SpriteBatch b,UiRect slot,Color tint)
    {
        // Keep this treatment in color choices; canvas beads retain their simpler surface.
        var body=new UiRect(slot.X+6,slot.Y+6,slot.Width-12,slot.Height-12);
        float brightness=(0.2126f*tint.R+0.7152f*tint.G+0.0722f*tint.B)/255f;
        var rim=Color.Lerp(tint,Color.Black,0.25f+0.15f*(1f-brightness));
        var shadow=Color.Lerp(tint,Color.Black,0.55f)*0.22f;
        BeadCell(b,new UiRect(body.X+1,body.Y+2,body.Width+1,body.Height+1),slot,shadow,false);
        BeadCell(b,new UiRect(body.X-1,body.Y-1,body.Width+2,body.Height+2),slot,rim,false);
        BeadCell(b,body,slot,tint,false);
    }
    private static bool Sprite(SpriteBatch b,string sheet,Dictionary<string,int> map,string id,int cell,Rectangle target)
    {
        var tex=Get(sheet);
        if(tex is null || tex.Width%cell!=0 || !map.TryGetValue(id,out int i) || !ArtDefinition.Fits(i,tex.Width/cell,cell,tex.Width,tex.Height))return false;
        b.Draw(tex,target,new Rectangle(i%(tex.Width/cell)*cell,i/(tex.Width/cell)*cell,cell,cell),Color.White);return true;
    }
    public static bool Icon(SpriteBatch b,string id,Rectangle target)=>Definition.ShowIcons && Sprite(b,"Icons",Definition.Icons,id,16,target);
    public static bool PipetteIcon(SpriteBatch b,UiRect buttonFace)
    {
        var texture=Get("Pipette");
        if(texture is null||texture.Width!=24||texture.Height!=24)return false;
        b.Draw(texture,new Rectangle(buttonFace.X+(buttonFace.Width-28)/2,buttonFace.Y+(buttonFace.Height-28)/2,28,28),Color.White);
        return true;
    }
    public static bool Status(SpriteBatch b,string id,Rectangle target)=>Definition.ShowIcons && Sprite(b,"Status",Definition.Status,id,12,target);
    public static bool BeadCell(SpriteBatch b,UiRect r,UiRect clip,Color tint,bool respectDisplaySetting=true)
    {
        if(r.Width<5 || r.Height<5 || respectDisplaySetting && !Definition.ShowBeadHoles)return false;
        int x=Math.Max(r.X,clip.X),y=Math.Max(r.Y,clip.Y),right=Math.Min(r.Right,clip.Right),bottom=Math.Min(r.Bottom,clip.Bottom);
        if(x>=right || y>=bottom)return true;
        if(beadSurface is null || beadSurface.IsDisposed)
        {
            var pixels=new Color[32*32];
            for(int py=0;py<32;py++)for(int px=0;px<32;px++)
            {
                double dx=px-15.5,dy=py-15.5,d=Math.Sqrt(dx*dx+dy*dy);
                if(d>14.5 || d<5)continue;
                float shade=d<6.5?0.52f:d>12.5?0.7f:(float)Math.Clamp(0.88-(dx+dy)/100,0.7,1);
                pixels[py*32+px]=Color.White*shade;
                pixels[py*32+px].A=255;
            }
            beadSurface=new Texture2D(Game1.graphics.GraphicsDevice,32,32);beadSurface.SetData(pixels);
        }
        int sx=(x-r.X)*32/r.Width,sy=(y-r.Y)*32/r.Height;
        int sw=Math.Max(1,(int)Math.Ceiling((right-r.X)*32d/r.Width)-sx),sh=Math.Max(1,(int)Math.Ceiling((bottom-r.Y)*32d/r.Height)-sy);
        b.Draw(beadSurface,new Rectangle(x,y,right-x,bottom-y),new Rectangle(sx,sy,Math.Min(sw,32-sx),Math.Min(sh,32-sy)),tint);
        return true;
    }
    public static int ButtonIcon(SpriteBatch b,UiRect r,string key,string label)
    {
        if(!Definition.ShowIcons || r.Height<28 || UiTextCache.Measure(Game1.smallFont,label).X>r.Width-38)return 6;
        string id=key.StartsWith("editor.")?key[7..]:key;
        id=id switch{"make" or "manufacture" or "iron-confirm"=>"iron","overlay" or "materials"=>"material","all-colors"=>"creative",_=>id};
        if(key.StartsWith("material."))
        {
            var d=Definition;var tex=Get("SourceBeads");int i=tex is null?-1:d.BeadIndex(key[9..],tex.Width,tex.Height);
            if(tex is null || i<0)return 6;
            b.Draw(tex,new Rectangle(r.X+7,r.Y+(r.Height-16)/2,16,16),new Rectangle(i%d.BeadColumns*16,i/d.BeadColumns*16,16,16),Color.White);return 29;
        }
        return Icon(b,id,new Rectangle(r.X+7,r.Y+(r.Height-16)/2,16,16))?29:6;
    }
    public static Texture2D Beads(ProcessingCatalog catalog)
    {
        var source=Get("SourceBeads");var d=Definition;
        int width=Math.Max(1,catalog.Materials.Count)*16;var output=new Color[width*16];
        Color[]? pixels=null;
        if(source is not null){pixels=new Color[source.Width*source.Height];source.GetData(pixels);}
        for(int i=0;i<catalog.Materials.Count;i++)
        {
            int index=source is null?-1:d.BeadIndex(catalog.Materials[i].Id,source.Width,source.Height);
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                output[y*width+i*16+x]=index>=0 && pixels is not null
                    ?pixels![(index/d.BeadColumns*16+y)*source!.Width+index%d.BeadColumns*16+x]
                    :(x-7)*(x-7)+(y-7)*(y-7)<=30 && !(x>=5 && x<=9 && y>=5 && y<=9)?new Color(130,115,140):Color.Transparent;
        }
        var texture=new Texture2D(Game1.graphics.GraphicsDevice,width,16);texture.SetData(output);return texture;
    }
}
