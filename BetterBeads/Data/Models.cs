namespace BetterBeads.Data;

// DTOs contain no game objects or textures. IDs, not translated names, define identity.
public enum ProductUse { Picture, WoodFurniture, Statue, Sword, Dagger, Hammer, Hat, Shirt, Pants, Wallpaper, Flooring }
public enum SwordOrientation { Diagonal, Vertical }
public enum MaterialSeries { Other, Metal, Gem }

public sealed class MaterialDefinition
{
    public string Id { get; set; } = "";
    public HashSet<ProductUse> AllowedUses { get; set; } = new();
    public double Weight { get; set; }
    public double Hardness { get; set; }
    public string? EffectGroup { get; set; }
    public MaterialSeries Series { get; set; }
    public string? RefinedFrom { get; set; }
}

public sealed class BeadCell
{
    public string ColorId { get; set; } = "";
    public uint Rgba { get; set; }
    public string? MaterialId { get; set; }
    public BeadCell Copy() => new() { ColorId = ColorId, Rgba = Rgba, MaterialId = MaterialId };
}

public sealed class BeadGrid
{
    public int Width { get; set; }
    public int Height { get; set; }
    // Null is empty. Black and other opaque colors are never treated as empty.
    public List<BeadCell?> Cells { get; set; } = new();
    public BeadGrid Copy() => new() { Width = Width, Height = Height, Cells = Cells.Select(c => c?.Copy()).ToList() };
}

public sealed class ReferencePixels
{
    public string? SourceItemId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public uint[] Pixels { get; set; } = Array.Empty<uint>();
    public ReferencePixels Copy() => new() { SourceItemId = SourceItemId, Width = Width, Height = Height, Pixels = (uint[])Pixels.Clone() };
}

public sealed class Blueprint
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public int Revision { get; set; }
    public ProductUse Use { get; set; }
    public string TemplateId { get; set; } = "";
    public SwordOrientation SwordOrientation { get; set; }
    // Opaque RGBA; older blueprints use the same neutral canvas by default.
    public uint BackgroundRgba { get; set; } = 0xEADFC6FF;
    public Dictionary<string, BeadGrid> Views { get; set; } = new();
    public Dictionary<string, int> WoolMaterials { get; set; } = new();
    public Dictionary<string, int> SupplementaryMaterials { get; set; } = new();
    public List<string> SelectedEffects { get; set; } = new();
    public ReferencePixels? Reference { get; set; }

    public Blueprint Copy(bool newIdentity = false) => new()
    {
        Id = newIdentity ? Guid.NewGuid().ToString("N") : Id,
        Name = Name, Revision = newIdentity ? 0 : Revision, Use = Use, TemplateId = TemplateId, SwordOrientation = SwordOrientation, BackgroundRgba = BackgroundRgba,
        Views = Views.ToDictionary(p => p.Key, p => p.Value.Copy()),
        WoolMaterials = new(WoolMaterials), SupplementaryMaterials = new(SupplementaryMaterials), SelectedEffects = new(SelectedEffects), Reference = Reference?.Copy()
    };
}

public sealed class ProductSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string RulesVersion { get; set; } = "1";
    public string? FurnitureVariantId { get; set; }
    public string? WeaponRulesVersion { get; set; }
    public Blueprint Design { get; set; } = new();
    public Dictionary<string, int> ActualMaterials { get; set; } = new();
    public Dictionary<string, double> FinalStats { get; set; } = new();
    public List<string> Effects { get; set; } = new();
    public string? EffectRulesVersion { get; set; }
    public Dictionary<string,double> EffectParameters { get; set; } = new();
    public GalaxySoulState? GalaxySoul { get; set; }
    public bool CreatedInCreativeMode { get; set; }
#if BEADS_LITE
    public ArtworkValuation? Valuation { get; set; }
#endif
    public ProductSnapshot Copy() => new()
    {
        SchemaVersion = SchemaVersion, InstanceId = InstanceId, RulesVersion = RulesVersion, FurnitureVariantId=FurnitureVariantId, WeaponRulesVersion = WeaponRulesVersion,
        Design = Design.Copy(), ActualMaterials = new(ActualMaterials),
        FinalStats = new(FinalStats), Effects = new(Effects), EffectRulesVersion=EffectRulesVersion, EffectParameters=new(EffectParameters),GalaxySoul=GalaxySoul?.Copy(),CreatedInCreativeMode=CreatedInCreativeMode
#if BEADS_LITE
        ,Valuation=Valuation?.Copy()
#endif
    };
}

public sealed class SaveProgress
{
    public WorkshopProgress? Workshop { get; set; }
    public List<uint> FavoriteColors { get; set; } = new();
    public int SchemaVersion { get; set; } = 1;
    public HashSet<string> UnlockedColors { get; set; } = new();
    public HashSet<string> ProcessedSources { get; set; } = new();
    // Each record remains independent JSON, so an unreadable design doesn't destroy its neighbors.
    public Dictionary<string, string> BlueprintRecords { get; set; } = new();
#if BEADS_LITE
    // Built-in templates are game assets; deleting one only hides it for this save.
    public HashSet<string> HiddenLiteTemplates { get; set; } = new();
    public HashSet<string> FavoriteBlueprintIds { get; set; } = new();
    public List<string> RecentBlueprintIds { get; set; } = new();
    public long ArtworkValuationSequence { get; set; }
    public List<CollectorLetter> CollectorLetters { get; set; } = new();
    public Dictionary<string,GiftGalleryRecord> GiftGalleryRecords { get; set; } = new();
#endif
}
