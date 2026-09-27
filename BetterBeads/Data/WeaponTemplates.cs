namespace BetterBeads.Data;

public sealed record WeaponTemplateDefinition(string Id,ProductUse Use,int PixelWidth,int PixelHeight,
    string NameKey,string DescriptionKey,string TextureAsset)
{
    public bool IsValid=>!string.IsNullOrWhiteSpace(Id) && Id.All(c=>(c<128 && char.IsLetterOrDigit(c)) || c is '.' or '_' or '-')
        && WeaponMaterials.IsWeapon(Use) && PixelWidth==PixelHeight && PixelWidth is 16 or 24 or 32
        && !string.IsNullOrWhiteSpace(NameKey) && !string.IsNullOrWhiteSpace(DescriptionKey)
        && !string.IsNullOrWhiteSpace(TextureAsset) && !TextureAsset.Contains('\\') && !TextureAsset.Contains("..",StringComparison.Ordinal);
    public int NativeType=>Use switch{ProductUse.Sword=>3,ProductUse.Dagger=>1,ProductUse.Hammer=>2,_=>throw new InvalidOperationException("Not a weapon template.")};
    public bool Matches(Blueprint design)=>design.TemplateId==Id && design.Use==Use && design.Views.Count==1
        && design.Views.TryGetValue("front",out var grid) && grid.Width==PixelWidth && grid.Height==PixelHeight
        && grid.Cells.Count==PixelWidth*PixelHeight;
}

public static class WeaponTemplates
{
    private static IReadOnlyDictionary<string,WeaponTemplateDefinition> entries=new Dictionary<string,WeaponTemplateDefinition>();
    public static IReadOnlyList<WeaponTemplateDefinition> All=>entries.Values.ToArray();
    public static bool TryGet(string id,out WeaponTemplateDefinition definition)=>entries.TryGetValue(id,out definition!);
    public static void Configure(IEnumerable<WeaponTemplateDefinition> definitions)
    {
        var next=new Dictionary<string,WeaponTemplateDefinition>(StringComparer.Ordinal);
        foreach(var definition in definitions)
        {
            if(!definition.IsValid || !next.TryAdd(definition.Id,definition)
                || FurnitureTemplates.TryGet(definition.Id,out _) || definition.Id==ProductTemplates.Hat)
                throw new ArgumentException("Invalid or conflicting weapon template.");
        }
        if(next.Values.Select(t=>t.TextureAsset).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=next.Count
            || next.Values.Any(t=>FurnitureTemplates.All.Any(f=>string.Equals(f.TextureAsset,t.TextureAsset,StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Weapon textures must have distinct asset names.");
        entries=next;
    }
}
