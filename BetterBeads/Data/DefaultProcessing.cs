namespace BetterBeads.Data;

public static class DefaultProcessing
{
    public static ProcessingCatalog Create()
    {
        var data = new ProcessingCatalog {RulesVersion=GameplayBalance.Version};
        void Material(string id, params ProductUse[] uses) => data.Materials.Add(new() { Id = id, AllowedUses = uses.ToHashSet() });
        void Color(string id, uint rgba, bool basic = false) => data.Colors.Add(new() {
            Id = id, Rgba = rgba, InitiallyUnlocked = basic, NameKey = "color." + id });
        void Source(string item, string material, int amount, string? color) => data.Sources.Add(new() {
            Id = "item." + item, ItemId = "(O)" + item, MaterialId = material, Yield = amount, ColorId = color });
        Material("wood", ProductUse.WoodFurniture);
        Material("hardwood", ProductUse.WoodFurniture);
        Material("stone", ProductUse.Statue);
        Material("wool", ProductUse.Hat, ProductUse.Shirt, ProductUse.Pants);
        Material("decoration", ProductUse.Picture);
        foreach (var id in new[] { "copper", "iron", "gold", "iridium", "emerald", "aquamarine", "ruby", "amethyst", "topaz", "jade", "diamond" })
            Material(id, ProductUse.Sword, ProductUse.Dagger, ProductUse.Hammer);
        DefaultWeapons.ApplyNewDefaults(data);
        GameplayBalance.ApplyDefaults(data);
        DefaultRefinement.AddRefinedMaterials(data);
        Color("black", 0x25242AFF, true); Color("white", 0xFFF7E8FF, true);
        Color("brown", 0xAA734AFF); Color("dark-brown", 0x674833FF); Color("gray", 0x969BA5FF);
        Color("cream", 0xEBD7B3FF); Color("copper", 0xD17F49FF); Color("iron", 0xABBCCBFF);
        Color("gold", 0xF1C447FF); Color("purple", 0x986BD4FF); Color("green", 0x41AD6DFF);
        Color("aqua", 0x70D5E5FF); Color("red", 0xD84E63FF); Color("violet", 0xBC76D0FF);
        Color("orange", 0xEAA344FF); Color("jade", 0x84BA8AFF); Color("ice", 0xC5EBF4FF);
        Color("leaf", 0x809B4DFF); Color("pink", 0xF19EB7FF);
        Source("388", "wood", 1, "brown"); Source("709", "hardwood", 4, "dark-brown");
        Source("390", "stone", 1, "gray"); Source("440", "wool", 24, "cream");
        Source("771", "decoration", 2, "leaf");
        Source("428", "wool", 36, null);
        foreach (var entry in new[] { ("378", "334", "copper", "copper"), ("380", "335", "iron", "iron"),
            ("384", "336", "gold", "gold"), ("386", "337", "iridium", "purple") })
        { Source(entry.Item1, entry.Item3, 2, entry.Item4); Source(entry.Item2, entry.Item3, 12, entry.Item4); }
        foreach (var entry in new[] { ("60", "emerald", "green"), ("62", "aquamarine", "aqua"), ("64", "ruby", "red"),
            ("66", "amethyst", "violet"), ("68", "topaz", "orange"), ("70", "jade", "jade"), ("72", "diamond", "ice") })
            Source(entry.Item1, entry.Item2, 8, entry.Item3);
        // Explicit flower category only: unknown objects never become universal filler.
        data.Sources.Add(new() { Id = "category.flowers", Category = -80, MaterialId = "decoration", Yield = 4, ColorId = "pink" });
        return data;
    }
}
