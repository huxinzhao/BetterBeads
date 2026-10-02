namespace BetterBeads.Data;

public enum ProductGroup { Furniture, Weapon }

public sealed record ProductCategory(ProductGroup Group,ProductUse Use,string NameKey,string PurposeKey,string Template16,string? Template24,string? Template32)
{
    public string TemplateFor(int size)=>size switch
    {
        32 when Template32 is not null=>Template32,
        24 when Template24 is not null=>Template24,
        _=>Template16
    };
    public int[] Sizes=>Template24 is null?new[]{16,32}:new[]{16,24,32};
}

/// <summary>One source of truth for Lite product ordering, paths, dimensions, and template IDs.</summary>
public static class ProductCategories
{
    public static readonly IReadOnlyList<ProductCategory> All=new[]
    {
        new ProductCategory(ProductGroup.Furniture,ProductUse.Picture,"category.picture","category.picture-purpose",SimpleCrafting.Picture16,null,SimpleCrafting.Picture32),
        new ProductCategory(ProductGroup.Furniture,ProductUse.WoodFurniture,"category.ornament","category.ornament-purpose",SimpleCrafting.Ornament,null,SimpleCrafting.LargeOrnament),
        new ProductCategory(ProductGroup.Furniture,ProductUse.Wallpaper,"category.wallpaper","category.wallpaper-purpose",SimpleCrafting.Wallpaper16,null,SimpleCrafting.Wallpaper32),
        new ProductCategory(ProductGroup.Furniture,ProductUse.Flooring,"category.flooring","category.flooring-purpose",SimpleCrafting.Flooring16,null,SimpleCrafting.Flooring32),
        new ProductCategory(ProductGroup.Weapon,ProductUse.Sword,"category.sword","category.sword-purpose",SimpleCrafting.Sword,SimpleCrafting.Sword24,SimpleCrafting.Sword32),
        new ProductCategory(ProductGroup.Weapon,ProductUse.Dagger,"category.dagger","category.dagger-purpose",SimpleCrafting.Dagger,SimpleCrafting.Dagger24,SimpleCrafting.Dagger32),
        new ProductCategory(ProductGroup.Weapon,ProductUse.Hammer,"category.hammer","category.hammer-purpose",SimpleCrafting.Hammer,SimpleCrafting.Hammer24,SimpleCrafting.Hammer32)
    };
    public static ProductCategory? For(ProductUse use)=>All.FirstOrDefault(category=>category.Use==use);
    public static ProductCategory? ForTemplate(string template)=>All.FirstOrDefault(category=>category.Template16==template||category.Template24==template||category.Template32==template);
}
