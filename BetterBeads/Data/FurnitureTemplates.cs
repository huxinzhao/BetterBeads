namespace BetterBeads.Data;

public sealed record FurnitureTemplateDefinition(string Id,ProductUse Use,int PixelWidth,int PixelHeight,
    int FootprintWidth,int FootprintHeight,string NameKey,string TextureAsset,string NativeType="other",int RenderWidth=0,int RenderHeight=0)
{
    public bool IsValid=>!string.IsNullOrWhiteSpace(Id) && Id.All(c=>(c<128 && char.IsLetterOrDigit(c)) || c is '.' or '_' or '-')
        && NativeType is "other" or "chair" or "table" or "painting"
        && Use is ProductUse.Picture or ProductUse.WoodFurniture or ProductUse.Statue
        && PixelWidth>0 && PixelHeight>0 && PixelWidth<=DesignStorage.MaxDimension && PixelHeight<=DesignStorage.MaxDimension
        && PixelWidth%16==0 && PixelHeight%16==0 && FootprintWidth>0 && FootprintHeight>0
        && FootprintWidth<=(RenderWidth>0?RenderWidth:PixelWidth)/16 && FootprintHeight<=(RenderHeight>0?RenderHeight:PixelHeight)/16
        && !string.IsNullOrWhiteSpace(NameKey) && !string.IsNullOrWhiteSpace(TextureAsset)
        && !TextureAsset.Contains('\\') && !TextureAsset.Contains("..",StringComparison.Ordinal);
    public bool Matches(Blueprint design)=>(design.TemplateId==Id || design.TemplateId==SimpleCrafting.LargeOrnament && SimpleCrafting.IsOrnamentVariant(Id)) && design.Use==Use && design.Views.Count==1
        && design.Views.TryGetValue("front",out var grid) && grid.Width==PixelWidth && grid.Height==PixelHeight
        && grid.Cells.Count==PixelWidth*PixelHeight;
    public string FurnitureData(string displayName)
    {
        if(!IsValid || displayName.Contains('/'))throw new ArgumentException("Invalid furniture metadata.");
        return $"BetterBeads {Id}/{NativeType}/{(RenderWidth>0?RenderWidth:PixelWidth)/16} {(RenderHeight>0?RenderHeight:PixelHeight)/16}/{FootprintWidth} {FootprintHeight}/1/0/2/{displayName}/0/{TextureAsset.Replace('/','\\')}/true";
    }
}

public static class FurnitureTemplates
{
    // Legacy debug template is required to keep existing 0.2–0.8 samples readable; it does not enable manufacturing.
    private static readonly FurnitureTemplateDefinition legacy=new(ProductTemplates.Picture,ProductUse.Picture,16,16,1,1,
        "product.picture","Mods/xinzh.BetterBeads/PicturePlaceholder");
    private static IReadOnlyDictionary<string,FurnitureTemplateDefinition> entries=new Dictionary<string,FurnitureTemplateDefinition>{{legacy.Id,legacy}};
    public static IReadOnlyList<FurnitureTemplateDefinition> All=>entries.Values.ToArray();
    public static bool TryGet(string id,out FurnitureTemplateDefinition definition)=>entries.TryGetValue(id,out definition!);
    public static void Configure(IEnumerable<FurnitureTemplateDefinition> definitions)
    {
        var next=new Dictionary<string,FurnitureTemplateDefinition>(StringComparer.Ordinal){{legacy.Id,legacy}};
        foreach(var definition in definitions)
        {
            if(!definition.IsValid || (next.TryGetValue(definition.Id,out var existing) && existing!=definition))
                throw new ArgumentException("Invalid or conflicting furniture template.");
            next[definition.Id]=definition;
        }
        if(next.Values.Select(t=>t.TextureAsset).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=next.Count)
            throw new ArgumentException("Furniture textures must have distinct asset names.");
        entries=next; // Validate all entries before changing the active registry.
    }
}
