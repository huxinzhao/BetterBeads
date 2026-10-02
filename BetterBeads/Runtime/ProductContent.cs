using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Weapons;
using StardewValley.GameData;

namespace BetterBeads.Runtime;

internal sealed class ProductContent
{
    private const string HatTexture = "Mods/xinzh.BetterBeads/HatPlaceholder";
    private readonly ITranslationHelper text;
    public ProductContent(ITranslationHelper text) => this.text = text;

    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
#if BEADS_LITE
        if(DecorationPlacement.TryAsset(e))return;
#endif
        if(e.NameWithoutLocale.IsEquivalentTo("Data/AdditionalWallpaperFlooring"))
        {
            e.Edit(asset=>
            {
                var data=asset.GetData<List<ModWallpaperOrFlooring>>();
                foreach(string id in new[]{SimpleCrafting.Wallpaper16,SimpleCrafting.Wallpaper32,SimpleCrafting.Flooring16,SimpleCrafting.Flooring32})
                    if(!data.Any(entry=>entry.Id==id))data.Add(new ModWallpaperOrFlooring
                    {Id=id,Texture="Mods/xinzh.BetterBeads/DecorationPlaceholder",IsFlooring=id is SimpleCrafting.Flooring16 or SimpleCrafting.Flooring32,Count=1});
            });return;
        }
        if(e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/DecorationPlaceholder"))
        {
            e.LoadFrom(()=>{var t=new Texture2D(Game1.graphics.GraphicsDevice,256,48);var pixels=Enumerable.Repeat(new Color(234,223,198),256*48).ToArray();t.SetData(pixels);return t;},AssetLoadPriority.Low);
            return;
        }
        if(WeaponTemplates.All.Any(w=>w.PixelWidth>16&&e.NameWithoutLocale.IsEquivalentTo(w.TextureAsset)))
        {e.LoadFrom(()=>{var t=new Texture2D(Game1.graphics.GraphicsDevice,16,16);t.SetData(new Color[256]);return t;},AssetLoadPriority.Low);return;}
        if(e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/WallPicture16") || e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/WallPicture32")
            || FurnitureTemplates.All.Any(f=>(SimpleCrafting.IsOrnamentVariant(f.Id)||FurnitureFinish.IsNew(f.Id))&&e.NameWithoutLocale.IsEquivalentTo(f.TextureAsset)))
        {e.LoadFrom(()=>
        {
            int width,height;
            if(e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/WallPicture16"))width=height=32;
            else if(e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/WallPicture32"))width=height=48;
            else
            {
                var furniture=FurnitureTemplates.All.First(f=>(SimpleCrafting.IsOrnamentVariant(f.Id)||FurnitureFinish.IsNew(f.Id))&&e.NameWithoutLocale.IsEquivalentTo(f.TextureAsset));
                width=furniture.RenderWidth>0?furniture.RenderWidth:furniture.PixelWidth;
                height=furniture.RenderHeight>0?furniture.RenderHeight:furniture.PixelHeight;
            }
            var t=new Texture2D(Game1.graphics.GraphicsDevice,width,height);t.SetData(new Color[width*height]);return t;
        },AssetLoadPriority.Low);return;}
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))
            e.Edit(asset =>
            {
                var data=asset.AsDictionary<string,string>().Data;
                foreach(var definition in FurnitureTemplates.All)data[definition.Id]=definition.FurnitureData(text.Get(definition.NameKey).ToString());
            });
        else if(e.NameWithoutLocale.IsEquivalentTo("Data/Weapons"))
            e.Edit(asset=>
            {
                var data=asset.AsDictionary<string,WeaponData>().Data;
                foreach(var definition in WeaponTemplates.All)data[definition.Id]=new WeaponData
                {
                    Name="BetterBeads "+definition.Id,DisplayName=text.Get(definition.NameKey).ToString()+(definition.PixelWidth>16?$" {definition.PixelWidth}×{definition.PixelHeight}":""),
                    Description=text.Get(definition.DescriptionKey).ToString(),Type=definition.NativeType,
                    Texture=definition.TextureAsset,SpriteIndex=0,
                    // Neutral metadata only; valid independent snapshots supply the actual combat values.
                    MinDamage=0,MaxDamage=0,Speed=0,Knockback=0,CritChance=0,CritMultiplier=0,AreaOfEffect=0,Defense=0,Precision=0
                };
            });
        else if(e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/ChairBase") || e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/TableBase") || e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/ChairBaseFront") || e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/Picture32Base"))
            e.LoadFrom(()=>{int w=e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/TableBase") || e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/Picture32Base")?32:16;var t=new Texture2D(Game1.graphics.GraphicsDevice,w,32);t.SetData(new Color[w*32]);return t;},AssetLoadPriority.Low);
        else if(e.NameWithoutLocale.IsEquivalentTo("Data/Shirts"))
            e.Edit(a=>a.AsDictionary<string,StardewValley.GameData.Shirts.ShirtData>().Data[ClothingTemplates.Shirt]=new(){Name="BetterBeads Shirt",DisplayName=text.Get("product.shirt"),Description=text.Get("product.description"),Price=0,Texture="Mods/xinzh.BetterBeads/ShirtBase",SpriteIndex=0,CanBeDyed=false,HasSleeves=false});
        else if(e.NameWithoutLocale.IsEquivalentTo("Data/Pants"))
            e.Edit(a=>a.AsDictionary<string,StardewValley.GameData.Pants.PantsData>().Data[ClothingTemplates.Pants]=new(){Name="BetterBeads Pants",DisplayName=text.Get("product.pants"),Description=text.Get("product.description"),Price=0,Texture="Characters/Farmer/pants",SpriteIndex=0,DefaultColor="255 255 255",CanBeDyed=false});
        else if(e.NameWithoutLocale.IsEquivalentTo("Mods/xinzh.BetterBeads/ShirtBase"))
            e.LoadFrom(()=>{var t=new Texture2D(Game1.graphics.GraphicsDevice,256,32);t.SetData(new Color[256*32]);return t;},AssetLoadPriority.Low);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Hats"))
            e.Edit(asset => asset.AsDictionary<string, string>().Data[ProductTemplates.Hat] =
                $"BetterBeads Hat/{text.Get("product.description")}/true/false//{text.Get("product.hat")}/0/{HatTexture.Replace('/', '\\')}");
    }
}
