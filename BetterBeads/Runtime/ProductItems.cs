using System.Runtime.CompilerServices;
using BetterBeads.Data;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed class ProductItems
{
    public const string SnapshotKey = "xinzh.BetterBeads/snapshot";
    private ConditionalWeakTable<Item, Cached> parsed = new();
    private sealed class Cached
    {
        public string Raw = "";
        public string ItemId = "";
        public ProductSnapshot? Snapshot;
        public ProductRenderData? Render;
    }

    public static bool IsProduct(Item? item) => item is not null &&
        ((item.QualifiedItemId == "(F)"+item.ItemId && FurnitureTemplates.TryGet(item.ItemId,out _))
            || item.QualifiedItemId == "(H)" + ProductTemplates.Hat
            || item.QualifiedItemId == "(S)" + ClothingTemplates.Shirt || item.QualifiedItemId == "(P)" + ClothingTemplates.Pants
            || (item.QualifiedItemId == "(W)"+item.ItemId && WeaponTemplates.TryGet(item.ItemId,out _)));

    private Cached? ReadEntry(Item? item)
    {
        if (!IsProduct(item) || !item!.modData.TryGetValue(SnapshotKey, out var raw)) return null;
        var entry = parsed.GetValue(item, _ => new Cached());
        if (entry.Raw == raw && entry.ItemId == item.QualifiedItemId) return entry;
        entry.Raw = raw;
        entry.ItemId = item.QualifiedItemId;
        entry.Snapshot = DesignStorage.TryReadSnapshot(raw, out var snapshot) && ProductTemplates.Matches(snapshot!)
            && (snapshot!.FurnitureVariantId??snapshot.Design.TemplateId)==item.ItemId ? snapshot : null;
        entry.Render=null;
        return entry;
    }

    public ProductSnapshot? Read(Item? item)=>ReadEntry(item)?.Snapshot;
    public ProductRenderData? ReadVisual(Item? item)
    {
        var entry=ReadEntry(item);
        return entry?.Snapshot is {} snapshot?entry.Render??=new ProductRenderData(snapshot):null;
    }

    public Item Create(ProductSnapshot snapshot)
    {
        if (!ProductTemplates.Matches(snapshot)) throw new ArgumentException("Invalid product template.");
        var item = ItemRegistry.Create(ProductTemplates.QualifiedId(snapshot));
        item.modData[SnapshotKey] = DesignStorage.Serialize(snapshot.Copy());
        item.Stack = 1;
        if(item is StardewValley.Tools.MeleeWeapon weapon && !WeaponInstances.Apply(weapon,snapshot))
            throw new InvalidOperationException("Weapon snapshot could not be applied.");
        return item;
    }

    public void Clear() => parsed = new();
}
