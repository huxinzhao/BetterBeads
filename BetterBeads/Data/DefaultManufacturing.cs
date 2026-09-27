namespace BetterBeads.Data;

// Each template keeps its own native pixel size; occupied cells are billed as individual beads.
public static class DefaultManufacturing
{
    public const string WoodOrnament = "xinzh.BetterBeads.WoodOrnament16";
    public const string Chair="xinzh.BetterBeads.Chair16";
    public const string Table="xinzh.BetterBeads.Table32";
    public const string StoneStatue = "xinzh.BetterBeads.StoneStatue16";

    public static IReadOnlyList<FurnitureTemplateDefinition> Furniture() => new FurnitureTemplateDefinition[]
    {
        // Keep the existing ID and texture path so earlier picture snapshots remain readable.
        new(ProductTemplates.Picture,ProductUse.Picture,16,16,1,1,
            "product.picture","Mods/xinzh.BetterBeads/PicturePlaceholder"),
        new(ProductTemplates.DetailedPicture,ProductUse.Picture,32,32,2,2,
            "product.picture-detailed","Mods/xinzh.BetterBeads/Picture32Base"),
        new(WoodOrnament,ProductUse.WoodFurniture,16,16,1,1,
            "product.wood-ornament","Mods/xinzh.BetterBeads/WoodOrnamentPlaceholder"),
        new(StoneStatue,ProductUse.Statue,16,16,1,1,
            "product.stone-statue","Mods/xinzh.BetterBeads/StoneStatuePlaceholder"),
        new(Chair,ProductUse.WoodFurniture,16,32,1,1,"product.chair","Mods/xinzh.BetterBeads/ChairBase","chair"),
        new(Table,ProductUse.WoodFurniture,32,32,2,2,"product.table","Mods/xinzh.BetterBeads/TableBase","table")
    };

    public static IReadOnlyList<ManufacturingRecipe> Recipes() => Furniture().Select(t => new ManufacturingRecipe(
        new(t.Id,t.Use,t.PixelWidth,t.PixelHeight,new[]{"front"},"front",MinimumBeads:1,ManufacturingAvailable:true,StructureBudget:t.Id==Chair?48:t.Id==Table?96:ProductTemplates.IsPicture(t.Id)?0:16),
        "(F)"+t.Id,ExtraCost:0)).ToArray();
}
