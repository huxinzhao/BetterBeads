namespace BetterBeads.Data;

public sealed class PaletteColor
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public uint Rgba { get; set; }
    public bool InitiallyUnlocked { get; set; }
}

public sealed class ProcessingSource
{
    public string Id { get; set; } = "";
    public string? ItemId { get; set; }
    public int? Category { get; set; }
    public string MaterialId { get; set; } = "";
    public string? ColorId { get; set; }
    public int Yield { get; set; }
}

public sealed class ProcessingCatalog
{
    public string RulesVersion { get; set; } = "1";
    public List<MaterialDefinition> Materials { get; set; } = new();
    public List<PaletteColor> Colors { get; set; } = new();
    public List<ProcessingSource> Sources { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(RulesVersion)) errors.Add("rules.version");
        if (Materials is null || Colors is null || Sources is null) return new[] { "catalog.null" };
        if (Materials.Count == 0 || Colors.Count == 0) errors.Add("catalog.empty");
        if (Materials.Any(m => m is null) || Colors.Any(c => c is null) || Sources.Any(s => s is null))
            return new[] { "catalog.null-entry" };
        CheckIds(Materials.Select(m => m.Id), "material", errors);
        CheckIds(Colors.Select(c => c.Id), "color", errors);
        CheckIds(Sources.Select(s => s.Id), "source", errors);
        var materials = Materials.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var colors = Colors.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var material in Materials)
            if (material.AllowedUses is null || material.AllowedUses.Count == 0
                || material.AllowedUses.Any(use => !Enum.IsDefined(typeof(ProductUse), use))
                || !double.IsFinite(material.Weight) || material.Weight < 0
                || !double.IsFinite(material.Hardness) || material.Hardness < 0)
                errors.Add("material.invalid:" + material.Id);
        foreach(var material in Materials)
        {
            if(!Enum.IsDefined(typeof(MaterialSeries),material.Series))errors.Add("material.series:"+material.Id);
            if(material.RefinedFrom is not null)
            {
                var original=Materials.FirstOrDefault(m=>m.Id==material.RefinedFrom);
                if(original is null || original.Id==material.Id || original.RefinedFrom is not null
                    || material.Series==MaterialSeries.Other || original.Series!=material.Series
                    || material.AllowedUses is null || original.AllowedUses is null || !material.AllowedUses.SetEquals(original.AllowedUses)
                    || material.EffectGroup!=original.EffectGroup || material.Hardness<=original.Hardness
                    || Materials.Count(m=>m.RefinedFrom==material.RefinedFrom)>1)errors.Add("material.refinement:"+material.Id);
            }
        }
        foreach (var color in Colors)
        {
            if ((color.Rgba & 255) != 255 || string.IsNullOrWhiteSpace(color.NameKey)) errors.Add("color.invalid:" + color.Id);
        }
        var selectors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in Sources)
        {
            bool hasItem = !string.IsNullOrWhiteSpace(source.ItemId);
            if (hasItem == source.Category.HasValue || (hasItem && !source.ItemId!.StartsWith("(O)", StringComparison.Ordinal)))
                errors.Add("source.selector:" + source.Id);
            string selector = hasItem ? source.ItemId! : "category:" + source.Category;
            if (!selectors.Add(selector)) errors.Add("source.duplicate-selector:" + source.Id);
            if (source.Yield <= 0 || !materials.Contains(source.MaterialId)
                || (source.ColorId is not null && !colors.Contains(source.ColorId))) errors.Add("source.reference:" + source.Id);
        }
        return errors;
    }

    private static void CheckIds(IEnumerable<string> ids, string kind, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-')) || !seen.Add(id))
                errors.Add(kind + ".id:" + id);
    }

    // Explicit item rules take precedence over category rules. Quality isn't part of color identity.
    public ProcessingSource? Match(string qualifiedId, int category) =>
        Sources.FirstOrDefault(s => s.ItemId == qualifiedId)
        ?? Sources.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.ItemId) && s.Category == category);
}

public static class MaterialRules
{
    public static bool IsDecoration(ProductUse use) => use is ProductUse.Picture or ProductUse.WoodFurniture or ProductUse.Statue;
    public static string Canonical(MaterialDefinition material)=>material.Series==MaterialSeries.Other && material.Id!="wool"?"decoration":material.Id;
    public static bool CanUse(MaterialDefinition material, ProductUse use) => IsDecoration(use)?Canonical(material)=="decoration"
        :ClothingTemplates.IsClothing(use)?material.Id=="wool":material.Series!=MaterialSeries.Other && material.AllowedUses.Contains(use);
    // A single incompatible entry rejects the complete bill, including supplementary material.
    public static bool Allows(ProcessingCatalog catalog, ProductUse use, IReadOnlyDictionary<string, int> bill) =>
        bill.Count > 0 && bill.All(pair => pair.Value > 0
            && catalog.Materials.Any(m => m.Id == pair.Key && CanUse(m,use)));
}
