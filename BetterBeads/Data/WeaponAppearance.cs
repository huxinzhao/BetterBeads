using System.Text.Json;

namespace BetterBeads.Data;

// Separate from the product snapshot: no combat stats, materials, price or enchantments.
internal sealed class WeaponAppearance
{
    public int SchemaVersion { get; set; } = 1;
    public string BaseItemId { get; set; } = "";
    public string DrawnItemId { get; set; } = "";
    public Blueprint? Pattern { get; set; }

    public bool AppliesTo(string baseId,string? appearance)
        =>BaseItemId==baseId && DrawnItemId==appearance;

    public static WeaponAppearance Create(string baseId,string drawnId,Blueprint? pattern)=>new()
    {
        BaseItemId=baseId,DrawnItemId=drawnId,
        Pattern=pattern is null?null:new Blueprint
        {
            Name=pattern.Name,Use=pattern.Use,TemplateId=pattern.TemplateId,SwordOrientation=pattern.SwordOrientation,
            Views=pattern.Views.ToDictionary(p=>p.Key,p=>p.Value.Copy())
        }
    };

    public static bool TryRead(string raw,out WeaponAppearance? value)
    {
        value=null;
        if(string.IsNullOrWhiteSpace(raw)||raw.Length>256_000)return false;
        try
        {
            var parsed=JsonSerializer.Deserialize<WeaponAppearance>(raw);
            if(parsed is null || parsed.SchemaVersion!=1 || !WeaponId(parsed.BaseItemId) || !WeaponId(parsed.DrawnItemId))return false;
            if(parsed.Pattern is null && (!int.TryParse(parsed.DrawnItemId[3..],out int nativeId)||nativeId<0))return false;
            if(parsed.Pattern is {} pattern && (!DesignStorage.IsStructurallyValid(pattern)
                || pattern.Use!=ProductUse.Sword&&pattern.SwordOrientation!=SwordOrientation.Diagonal
                || !WeaponTemplates.TryGet(pattern.TemplateId,out var template) || !template.Matches(pattern)
                || parsed.DrawnItemId!="(W)"+pattern.TemplateId || !pattern.Views["front"].Cells.Any(c=>c is not null)))return false;
            value=parsed;return true;
        }
        catch(JsonException){return false;}
    }

    private static bool WeaponId(string? value)=>value is {Length:>3 and <180} && value.StartsWith("(W)",StringComparison.Ordinal)
        && value.AsSpan(3).IndexOfAny(' ','\r','\n')<0;
}
