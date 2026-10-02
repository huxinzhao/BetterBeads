using BetterBeads.Data;
using StardewValley;

namespace BetterBeads.Runtime;

/// <summary>Read localized labels at use time; never store them in native item caches or snapshots.</summary>
internal static class ProductLabels
{
    public static int Price(ProductSnapshot snapshot)=>SimpleCrafting.SaleValue(snapshot,BasePrice);
    private static int? BasePrice(string id)=>id.StartsWith("(O)",StringComparison.Ordinal)
        &&Game1.objectData is {} data&&data.TryGetValue(id[3..],out var entry)?entry.Price:null;

    public static string Name(Item item)
    {
        if(item.ItemId is SimpleCrafting.Wallpaper16 or SimpleCrafting.Wallpaper32)
            return ContentText.Get("product.wallpaper","Bead Wallpaper");
        if(item.ItemId is SimpleCrafting.Flooring16 or SimpleCrafting.Flooring32)
            return ContentText.Get("product.flooring","Bead Flooring");
        if(FurnitureTemplates.TryGet(item.ItemId,out var furniture))return ContentText.Get(furniture.NameKey,"Bead artwork");
        if(WeaponTemplates.TryGet(item.ItemId,out var weapon))return ContentText.Get(weapon.NameKey,"Bead weapon")
            +(weapon.PixelWidth>16?$" {weapon.PixelWidth}×{weapon.PixelHeight}":"");
        return "";
    }
    public static string Description(ProductSnapshot snapshot)
    {
        string result=ContentText.Get(SimpleCrafting.IsWeapon(snapshot.Design.Use)?"product.weapon-description":"product.description","Handmade bead artwork.");
#if BEADS_LITE
        if(!snapshot.CreatedInCreativeMode&&!SimpleCrafting.IsDecoration(snapshot.Design.Use)&&snapshot.Valuation is not null)
            result+="\n"+ContentText.Get("market.random-note","市场估价随机，不代表图案好坏。");
#endif
        return result;
    }
    public static string SaleLine(ProductSnapshot snapshot)
    {
        if(snapshot.CreatedInCreativeMode)return ContentText.Get("market.creative-price","创造模式作品 · 售价：1金");
        return ContentText.Format("market.final-price",$"售价：{Price(snapshot)}金")
#if BEADS_LITE
            +(snapshot.Valuation?.Collector==true?" · "+ContentText.Get("market.collector-short","收藏家珍品"):"")
#endif
            ;
    }
}
