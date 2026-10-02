namespace BetterBeads.Data;

public static class ProductTemplates
{
    public const string Picture = "xinzh.BetterBeads.Picture16";
    public const string DetailedPicture = "xinzh.BetterBeads.Picture32";
    public static bool IsPicture(string id)=>id is Picture or DetailedPicture;
    public const string Hat = "xinzh.BetterBeads.Hat20";
    public static readonly string[] HatViews = { "front", "right", "left", "back" };

    public static bool Matches(ProductSnapshot snapshot)
    {
        var design = snapshot.Design;
        if(design.Use!=ProductUse.Sword&&design.SwordOrientation!=SwordOrientation.Diagonal)return false;
        if(FurnitureFinish.IsNew(snapshot.FurnitureVariantId))return SoulUpgrades.ValidSnapshot(snapshot)&&SimpleCrafting.Supported(design)
            &&snapshot.FurnitureVariantId==FurnitureFinish.Variant(design)
            &&FurnitureTemplates.TryGet(snapshot.FurnitureVariantId!,out _);
        if(snapshot.FurnitureVariantId is not null && design.TemplateId!=SimpleCrafting.LargeOrnament)return false;
        if(!SoulUpgrades.ValidSnapshot(snapshot))return false;
        if(SimpleCrafting.IsDecoration(design.Use))return SimpleCrafting.Supported(design)
            &&snapshot.FurnitureVariantId is null;
        if(WeaponMaterials.IsWeapon(design.Use))return WeaponTemplates.TryGet(design.TemplateId,out var weapon)
            && weapon.Matches(design) && !string.IsNullOrWhiteSpace(snapshot.WeaponRulesVersion)
            && WeaponStatValues.TryRead(snapshot.FinalStats,out _) && SpecialEffects.ValidSnapshot(snapshot);
        if(design.Use is ProductUse.Shirt or ProductUse.Pants)return ClothingTemplates.Matches(design);
        var isHat = design.TemplateId == Hat && design.Use == ProductUse.Hat;
        if (!isHat)
        {
            if(snapshot.FurnitureVariantId is {} variant)
                return design.TemplateId==SimpleCrafting.LargeOrnament && SimpleCrafting.OrnamentVariant(design)==variant
                    && FurnitureTemplates.TryGet(variant,out var variantFurniture) && variantFurniture.Matches(design);
            return FurnitureTemplates.TryGet(design.TemplateId,out var furniture) && furniture.Matches(design);
        }
        var views = isHat ? HatViews : new[] { "front" };
        int size = isHat ? 20 : 16;
        return design.Views.Count == views.Length && views.All(name =>
            design.Views.TryGetValue(name, out var grid) && grid.Width == size && grid.Height == size
            && grid.Cells.Count == size * size);
    }

    public static string QualifiedId(ProductSnapshot snapshot)
    {
        if(!Matches(snapshot))throw new ArgumentException("Snapshot does not match its template.");
        return (WeaponMaterials.IsWeapon(snapshot.Design.Use)?"(W)":snapshot.Design.Use==ProductUse.Hat?"(H)":snapshot.Design.Use==ProductUse.Shirt?"(S)":snapshot.Design.Use==ProductUse.Pants?"(P)":snapshot.Design.Use==ProductUse.Wallpaper?"(WP)":snapshot.Design.Use==ProductUse.Flooring?"(FL)":"(F)")
            +(snapshot.FurnitureVariantId??snapshot.Design.TemplateId);
    }

    public static BeadGrid CreateAtlas(ProductSnapshot snapshot)
    {
        if (!Matches(snapshot)) throw new ArgumentException("Snapshot does not match its template.");
        if(FurnitureFinish.IsNew(snapshot.FurnitureVariantId))return FurnitureFinish.Atlas(snapshot);
        if(snapshot.Design.Use==ProductUse.Shirt)return ClothingTemplates.ShirtAtlas(snapshot.Design);
        if(snapshot.FurnitureVariantId is {} variant && snapshot.Design.TemplateId==SimpleCrafting.LargeOrnament)
        {
            var source=snapshot.Design.Views["front"];
            var bounds=SimpleCrafting.OccupiedBounds(source);
            int width=variant is SimpleCrafting.OrnamentFromLarge11 or SimpleCrafting.OrnamentFromLarge12?16:32;
            int height=variant is SimpleCrafting.OrnamentFromLarge11 or SimpleCrafting.OrnamentFromLarge21?16:32;
            var result=new BeadGrid{Width=width,Height=height,Cells=Enumerable.Repeat<BeadCell?>(null,width*height).ToList()};
            int offsetX=(width-bounds.Width)/2,offsetY=height-bounds.Height;
            for(int y=0;y<bounds.Height;y++)for(int x=0;x<bounds.Width;x++)
                result.Cells[(offsetY+y)*width+offsetX+x]=source.Cells[(bounds.Y+y)*source.Width+bounds.X+x]?.Copy();
            return result;
        }
        if (snapshot.Design.Use != ProductUse.Hat) return snapshot.Design.Views["front"].Copy();
        return new BeadGrid { Width = 20, Height = 80,
            Cells = HatViews.SelectMany(v => snapshot.Design.Views[v].Cells.Select(c => c?.Copy())).ToList() };
    }

    public static ProductSnapshot DebugSample(bool hat, bool alternate, string name)
    {
        var design = new Blueprint { Name = name, TemplateId = hat ? Hat : Picture,
            Use = hat ? ProductUse.Hat : ProductUse.Picture };
        int size = hat ? 20 : 16;
        foreach (var view in hat ? HatViews : new[] { "front" })
        {
            var grid = new BeadGrid { Width = size, Height = size };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool filled = hat ? y >= 3 && y <= 9 && x >= 4 && x <= 15
                        : alternate ? Math.Abs(x - 7) + Math.Abs(y - 7) <= 6
                        : x >= 3 && x <= 12 && y >= 3 && y <= 12;
                    uint color = alternate ? 0x4BAAC8FFu : 0xEB9161FFu;
                    if (hat && view == "back") color = 0x9C78C4FF;
                    grid.Cells.Add(filled ? new BeadCell { ColorId = "debug", Rgba = color,
                        MaterialId = hat ? null : "decoration" } : null);
                }
            design.Views[view] = grid;
        }
        if (hat) design.WoolMaterials["wool"] = 80;
        return new ProductSnapshot { Design = design, RulesVersion = "debug-1", CreatedInCreativeMode=true,
            ActualMaterials = new() { [hat ? "wool" : "decoration"] = hat ? 80
                : design.Views["front"].Cells.Count(c => c is not null) } };
    }
}
