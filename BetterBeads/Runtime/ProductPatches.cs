using System.Reflection;
using System.Reflection.Emit;
using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.Tools;

namespace BetterBeads.Runtime;

internal static class ProductPatches
{
    private static ProductItems items = null!;
    private static TextureCache textures = null!;
    [ThreadStatic] private static Item? drawing;
    [ThreadStatic] private static bool drawingMenu;
    private readonly record struct DrawContext(Item? Item,bool Menu);
    [ThreadStatic] private static bool drawingWeaponUse;
    [ThreadStatic] private static Texture2D? fullWeaponTexture;
    [ThreadStatic] private static int fullWeaponDesignSize;
    [ThreadStatic] private static bool fullWeaponVertical;

    public static void Apply(string id, ProductItems products, TextureCache cache,StardewModdingAPI.ITranslationHelper text)
    {
        items = products;
        textures = cache;
        var harmony = new Harmony(id);
        foreach (var type in new[] { typeof(Furniture), typeof(Hat),typeof(Clothing),typeof(MeleeWeapon) })
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.Name is "drawInMenu" or "draw" or "drawWhenHeld" or "drawDuringUse" or "drawAtNonTileSpot"))
                harmony.Patch(method, prefix: Patch(nameof(BeginItem)), finalizer: Patch(nameof(EndDraw)),
                    transpiler: type == typeof(Furniture) ? Patch(nameof(FurnitureFront)) : null);
            harmony.Patch(AccessTools.Method(type, "maximumStackSize"), postfix: Patch(nameof(StackSize)));
        }
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Object), "drawPlacementBounds", new[]{typeof(SpriteBatch),typeof(GameLocation)}),
            prefix:Patch(nameof(BeginItem)),finalizer:Patch(nameof(EndDraw)),transpiler:Patch(nameof(FurnitureFront)));
        harmony.Patch(AccessTools.Method(typeof(Furniture), "IsHeldOverHead",Type.EmptyTypes),postfix:Patch(nameof(HeldOverHead)));
        harmony.Patch(AccessTools.Method(typeof(Furniture), "drawWhenHeld",new[]{typeof(SpriteBatch),typeof(Vector2),typeof(Farmer)}),prefix:Patch(nameof(DrawHeldFurniture)));
        harmony.Patch(AccessTools.Method(typeof(Furniture), "drawInMenu",new[]{typeof(SpriteBatch),typeof(Vector2),typeof(float),typeof(float),typeof(float),typeof(StackDrawType),typeof(Color),typeof(bool)}),prefix:Patch(nameof(DrawFurnitureIcon)));
        harmony.Patch(AccessTools.Method(typeof(FarmerRenderer), "drawHairAndAccesories"),
            prefix: Patch(nameof(BeginFarmer)), finalizer: Patch(nameof(EndDraw)));
        harmony.Patch(AccessTools.Method(typeof(ParsedItemData), nameof(ParsedItemData.GetTexture)), prefix: Patch(nameof(GetTexture)));
        harmony.Patch(AccessTools.Method(typeof(Item), nameof(Item.canStackWith)), postfix: Patch(nameof(CanStack)));
        harmony.Patch(AccessTools.Method(typeof(Item), nameof(Item.getOne)), postfix: Patch(nameof(CopySnapshot)));
        harmony.Patch(AccessTools.Method(typeof(Furniture), nameof(Furniture.salePrice)), prefix: Patch(nameof(Price)));
        harmony.Patch(AccessTools.Method(typeof(Item), "salePrice"), prefix: Patch(nameof(Price)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon), "salePrice"), prefix: Patch(nameof(Price)));
#if BEADS_LITE
        foreach(var type in new[]{typeof(StardewValley.Object),typeof(Tool)})
            harmony.Patch(AccessTools.PropertyGetter(type,"DisplayName"),prefix:Patch(nameof(DisplayName)));
        foreach(var type in new[]{typeof(Furniture),typeof(Tool)})
            harmony.Patch(AccessTools.PropertyGetter(type,"description"),prefix:Patch(nameof(BaseDescription)));
        if(AccessTools.Method(typeof(Furniture),"getDescription") is {} furnitureDescription)
            harmony.Patch(furnitureDescription,postfix:Patch(nameof(FurnitureDescription)));
#endif
#if BEADS_LITE
        harmony.Patch(AccessTools.Method(typeof(Item), "sellToStorePrice"), prefix: Patch(nameof(Price)));
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Object), "sellToStorePrice"), prefix: Patch(nameof(Price)));
        harmony.Patch(AccessTools.Method(typeof(Item), "canBeShipped"), postfix: Patch(nameof(CanShip)));
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Object), "canBeShipped"), postfix: Patch(nameof(CanShip)));
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Menus.ShopMenu), "highlightItemToSell"), postfix: Patch(nameof(ShopEligible)));
#endif
        var weaponDraw=typeof(MeleeWeapon).GetMethods(BindingFlags.Static|BindingFlags.Public).Single(m=>m.Name=="drawDuringUse");
        harmony.Patch(weaponDraw,prefix:Patch(nameof(BeginWeaponUse)),finalizer:Patch(nameof(EndWeaponUse)),transpiler:Patch(nameof(WeaponDrawCalls)));
        WeaponPatches.Apply(harmony,products,text);
        ClothingPatches.Apply(harmony,products,cache);
    }

    private static HarmonyMethod Patch(string name) => new(typeof(ProductPatches), name);
    private static bool Carryable(ProductRenderData? render)=>render?.Snapshot.Design is {} d
        &&(d.Use==ProductUse.Picture||d.Use==ProductUse.WoodFurniture&&d.TemplateId is SimpleCrafting.Ornament or SimpleCrafting.LargeOrnament);
    private static void HeldOverHead(StardewValley.Object __instance,ref bool __result)
    {if(Carryable(items.ReadVisual(__instance)))__result=true;}
    private static bool DrawHeldFurniture(Furniture __instance,SpriteBatch __0,Vector2 __1,Farmer __2)
    {
        var render=items.ReadVisual(__instance);if(!Carryable(render))return true;
        bool painting=render!.Snapshot.Design.Use==ProductUse.Picture;
        var texture=textures.Get(Game1.graphics.GraphicsDevice,render,painting?"atlas":"front");
        var bounds=render.VisibleBounds;
        int density=TextureCache.Density(texture);
        var source=painting?new Rectangle(0,0,texture.Width/density,texture.Height/density):new Rectangle(bounds.X,bounds.Y,bounds.Width,bounds.Height);
        if(source.Width<=0||source.Height<=0)return false;
        float scale=Math.Min(4f,96f/Math.Max(source.Width,source.Height));
        // Native position is the upper-left of a 64px held item; keep its bottom-center hand anchor.
        DrawFurnitureSprite(__0,texture,__1+new Vector2(32,64+(__instance.bigCraftable.Value?64:0)),source,Color.White,0,new Vector2(source.Width/2f,source.Height),scale,
            SpriteEffects.None,Math.Max(0,(__2.StandingPixel.Y+3)/10000f));
        return false;
    }
    private static bool DrawFurnitureIcon(Furniture __instance,SpriteBatch __0,Vector2 __1,float __2,float __3,float __4,StackDrawType __5,Color __6,bool __7)
    {
        var render=items.ReadVisual(__instance);
        if(render?.Snapshot.Design is not {} design||design.Use!=ProductUse.WoodFurniture
            ||design.TemplateId is not (SimpleCrafting.Ornament or SimpleCrafting.LargeOrnament))return true;
        var bounds=render.VisibleBounds;if(bounds.Width<=0||bounds.Height<=0)return true;
        __instance.AdjustMenuDrawForRecipes(ref __3,ref __2);
        var texture=textures.Get(Game1.graphics.GraphicsDevice,render,"furniture-icon");
        DrawFurnitureSprite(__0,texture,__1+new Vector2(32,32),new Rectangle(bounds.X,bounds.Y,bounds.Width,bounds.Height),__6*__3,0,
            new Vector2(bounds.Width/2f,bounds.Height/2f),56f/Math.Max(bounds.Width,bounds.Height)*__2,SpriteEffects.None,__4);
        __instance.DrawMenuIcons(__0,__1,__2,__3,__4,__5,__6);
        return false;
    }
    private static void BeginItem(Item __instance,MethodBase __originalMethod, out DrawContext __state)
    {
        __state = new(drawing,drawingMenu);
        drawing = __instance;
        drawingMenu=__originalMethod.Name=="drawInMenu";
    }
    private static void BeginFarmer(Farmer who, out DrawContext __state)
    {
        __state = new(drawing,drawingMenu);
        drawing = who.hat.Value;
        drawingMenu=false;
    }
    private static void BeginWeaponUse(Farmer f,string weaponItemId,out Item? __state)
    {
        __state=drawing;
        drawing=drawing as MeleeWeapon ?? f.CurrentTool as MeleeWeapon;
        drawingWeaponUse=true;fullWeaponTexture=null;fullWeaponDesignSize=0;fullWeaponVertical=false;
        if(drawing is MeleeWeapon weapon)
        {
            var design=WeaponAppearancePatches.ReadVisual(weapon,weaponItemId)?.Snapshot.Design
                ?? (weapon.QualifiedItemId==weaponItemId?items.Read(weapon)?.Design:null);
            fullWeaponVertical=design?.Use==ProductUse.Sword&&design.SwordOrientation==SwordOrientation.Vertical;
            fullWeaponDesignSize=design?.Views["front"].Width??0;
        }
    }
    private static void EndWeaponUse(Item? __state){drawing=__state;drawingWeaponUse=false;fullWeaponTexture=null;fullWeaponDesignSize=0;fullWeaponVertical=false;}
    // Harmony finalizer also runs when original drawing throws, preventing context leaks.
    private static void EndDraw(DrawContext __state) {drawing=__state.Item;drawingMenu=__state.Menu;}

    private static bool GetTexture(ParsedItemData __instance, ref Texture2D __result)
    {
        if(drawing is MeleeWeapon weapon && WeaponAppearancePatches.ReadVisual(weapon,__instance.QualifiedItemId) is {} appearance)
        {
            var design=appearance.Snapshot.Design;
            bool vertical=design.Use==ProductUse.Sword&&design.SwordOrientation==SwordOrientation.Vertical;
            bool large=design.Views["front"].Width>16;
            __result=textures.Get(Game1.graphics.GraphicsDevice,appearance,large&&!drawingWeaponUse?"weapon-icon":"front");
            if((large||vertical)&&drawingWeaponUse){fullWeaponTexture=__result;fullWeaponDesignSize=design.Views["front"].Width;fullWeaponVertical=vertical;}
            return false;
        }
        if (drawing?.QualifiedItemId != __instance.QualifiedItemId) return true;
        var render = items.ReadVisual(drawing);
        if (render is null) return true;
        var productDesign=render.Snapshot.Design;
        bool verticalSword=productDesign.Use==ProductUse.Sword&&productDesign.SwordOrientation==SwordOrientation.Vertical;
        bool largeWeapon=(productDesign.Use is ProductUse.Sword or ProductUse.Dagger or ProductUse.Hammer)
            &&productDesign.Views["front"].Width>16;
        __result = textures.Get(Game1.graphics.GraphicsDevice, render,largeWeapon&&!drawingWeaponUse?"weapon-icon":drawingMenu&&FurnitureFinish.IsNew(render.Snapshot.FurnitureVariantId)?"furniture-menu":"atlas");
        if((largeWeapon||verticalSword)&&drawingWeaponUse){fullWeaponTexture=__result;fullWeaponDesignSize=productDesign.Views["front"].Width;fullWeaponVertical=verticalSword;}
        return false;
    }
    private static IEnumerable<CodeInstruction> WeaponDrawCalls(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var instruction in instructions)
        {
            if(instruction.operand is MethodInfo method&&method.DeclaringType==typeof(SpriteBatch)&&method.Name==nameof(SpriteBatch.Draw)
                &&method.GetParameters() is {Length:9} parameters&&parameters[0].ParameterType==typeof(Texture2D)
                &&parameters[1].ParameterType==typeof(Vector2))
            {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ProductPatches),nameof(DrawWeaponSprite));}
            yield return instruction;
        }
    }
    private static void DrawWeaponSprite(SpriteBatch batch,Texture2D texture,Vector2 position,Rectangle? source,Color color,
        float rotation,Vector2 origin,float scale,SpriteEffects effects,float layer)
    {
        var nativeOrigin=origin;
        if(drawingWeaponUse&&ReferenceEquals(texture,fullWeaponTexture)&&source is {Width:16,Height:16})
        {
            int designSize=fullWeaponDesignSize;
            if(designSize>16)
            {
                source=new Rectangle(0,0,texture.Width,texture.Height);
                origin*=designSize/16f;
            }
        }
        // This method replaces only the native weapon-swing Draw calls. The texture returned by
        // ParsedItemData may differ from the cached instance, so angle correction uses the
        // weapon's saved orientation captured by BeginWeaponUse, not texture identity.
        if(drawingWeaponUse&&fullWeaponVertical)
        {
            var swing=WeaponShape.VerticalSwing(nativeOrigin.X,nativeOrigin.Y,rotation,scale,
                fullWeaponDesignSize>0?fullWeaponDesignSize:source?.Width??16,
                (effects&SpriteEffects.FlipHorizontally)!=0,(effects&SpriteEffects.FlipVertically)!=0);
            position+=new Vector2(swing.OffsetX,swing.OffsetY);
            origin=new Vector2(swing.OriginX,swing.OriginY);
            rotation=swing.Rotation;
        }
        batch.Draw(texture,position,source,color,rotation,origin,scale,effects,layer);
    }
    private static IEnumerable<CodeInstruction> FurnitureFront(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var instruction in instructions)
        {
            if(instruction.operand is MethodInfo draw&&draw.DeclaringType==typeof(SpriteBatch)&&draw.Name==nameof(SpriteBatch.Draw)
                &&draw.GetParameters() is {Length:9} args&&args[1].ParameterType==typeof(Vector2)&&args[6].ParameterType==typeof(float))
            {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ProductPatches),nameof(DrawFurnitureSprite));}
            if(instruction.operand is MethodInfo method && method.Name=="Load" && method.DeclaringType==typeof(LocalizedContentManager) && method.GetParameters().Length==1 && method.GetParameters()[0].ParameterType==typeof(string) && method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[]{typeof(Texture2D)}))
            {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ProductPatches),nameof(FrontTexture));}
            yield return instruction;
        }
    }
    private static void DrawFurnitureSprite(SpriteBatch batch,Texture2D texture,Vector2 position,Rectangle? source,Color color,
        float rotation,Vector2 origin,float scale,SpriteEffects effects,float layer)
    {
        int density=TextureCache.Density(texture);
        if(density>1)
        {
            if(source is {} r)source=new Rectangle(r.X*density,r.Y*density,r.Width*density,r.Height*density);
            origin*=density;scale/=density;
        }
        batch.Draw(texture,position,source,color,rotation,origin,scale,effects,layer);
    }
    private static Texture2D FrontTexture(LocalizedContentManager content,string name)
    {
        var render=items.ReadVisual(drawing);
        if(render?.Snapshot.Design.TemplateId==DefaultManufacturing.Chair && name.Replace('\\','/')=="Mods/xinzh.BetterBeads/ChairBaseFront")
            return textures.Get(Game1.graphics.GraphicsDevice,render,"chair-front");
        return content.Load<Texture2D>(name);
    }
    private static void StackSize(Item __instance, ref int __result)
    {
        if (ProductItems.IsProduct(__instance)) __result = 1;
    }
    private static void CanStack(Item __instance, ISalable other, ref bool __result)
    {
        if (ProductItems.IsProduct(__instance) || ProductItems.IsProduct(other as Item)) __result = false;
    }
    private static void CopySnapshot(Item __instance, Item __result)
    {
        WeaponAppearancePatches.Copy(__instance,__result);
        if (ProductItems.IsProduct(__instance) && __instance.modData.TryGetValue(ProductItems.SnapshotKey, out var raw))
        {
            __result.modData[ProductItems.SnapshotKey] = raw; // Strings are immutable; parsed copies are per item.
            if(__result is MeleeWeapon weapon)WeaponInstances.Apply(weapon,items.Read(weapon));
        }
    }
    private static bool Price(Item __instance, ref int __result)
    {
        if (!ProductItems.IsProduct(__instance))return true;
#if BEADS_LITE
        __result=items.Read(__instance) is {} snapshot?ProductLabels.Price(snapshot):0;
#else
        __result=0;
#endif
        return false;
    }
#if BEADS_LITE
    private static bool DisplayName(Item __instance,ref string __result)
    {
        if(items.Read(__instance) is null)return true;
        string name=ProductLabels.Name(__instance);if(name.Length==0)return true;
        __result=name;return false;
    }
    private static bool BaseDescription(Item __instance,ref string __result)
    {
        if(items.Read(__instance) is not {} snapshot)return true;
        __result=ProductLabels.Description(snapshot);return false;
    }
    private static void FurnitureDescription(Furniture __instance,ref string __result)
    {
        if(items.Read(__instance) is not {} snapshot)return;
        __result+="\n"+Game1.parseText(ProductLabels.SaleLine(snapshot),Game1.smallFont,320);
    }
    private static void CanShip(Item __instance,ref bool __result)
    {
        if(ProductItems.IsProduct(__instance))__result=items.Read(__instance) is {} snapshot&&SimpleCrafting.CanSell(snapshot);
    }
    private static void ShopEligible(Item __0,ref bool __result)
    {
        // Only veto invalid products. Native category/tag rules still select each shop's stock.
        if(ProductItems.IsProduct(__0)&&!(items.Read(__0) is {} snapshot&&SimpleCrafting.CanSell(snapshot)))__result=false;
    }
#endif
    public static void Clear() { drawing = null; WeaponAppearancePatches.Clear(); }
}
