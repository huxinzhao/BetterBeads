using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace BetterBeads.Runtime;

internal static class ClothingPatches
{
    private static ProductItems products=null!;
    private static TextureCache textures=null!;
    public static void Apply(Harmony harmony,ProductItems items,TextureCache cache)
    {
        products=items;textures=cache;
        harmony.Patch(AccessTools.Method(typeof(Farmer),"GetDisplayShirt"),postfix:new(typeof(ClothingPatches),nameof(Shirt)));
        harmony.Patch(AccessTools.Method(typeof(Farmer),"GetDisplayPants"),postfix:new(typeof(ClothingPatches),nameof(Pants)));
    }
    private static void Shirt(Farmer __instance,ref Texture2D __0,ref int __1)
    {
        var render=products.ReadVisual(__instance.shirtItem.Value);
        if(render?.Snapshot.Design.Use!=ProductUse.Shirt)return;
        __0=textures.Get(Game1.graphics.GraphicsDevice,render,"atlas");__1=0;
    }
    private static void Pants(Farmer __instance,ref Texture2D __0,ref int __1)
    {
        var render=products.ReadVisual(__instance.pantsItem.Value);
        if(render?.Snapshot.Design.Use!=ProductUse.Pants)return;
        __0=textures.Get(Game1.graphics.GraphicsDevice,render,"atlas");__1=0;
    }
}
