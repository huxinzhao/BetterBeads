using System.Text.Json;

namespace BetterBeads.Data;

public sealed record RefinementRecipe(string SourceMaterial,string OutputMaterial,int InputCount,int OutputCount,int CoalCount,int Minutes);
public static class DefaultRefinement
{
    public const string Version="4";
    public static IReadOnlyList<RefinementRecipe> Recipes(ProcessingCatalog catalog)=>catalog.Materials
        .Where(m=>m.RefinedFrom is not null).Select(m=>new RefinementRecipe(m.RefinedFrom!,m.Id,10,5,1,30)).ToArray();

    public static void AddRefinedMaterials(ProcessingCatalog catalog)
    {
        foreach(var original in catalog.Materials.Where(m=>m.RefinedFrom is null && m.Series is MaterialSeries.Metal or MaterialSeries.Gem).ToArray())
        {
            if(original.Hardness<=0 || original.Weight<=0 || !double.IsFinite(original.Hardness*1.25))
                throw new InvalidDataException("Cannot refine an invalid material profile: "+original.Id);
            string id="refined-"+original.Id;
            if(catalog.Materials.Any(m=>m.Id==id))throw new InvalidDataException("Refined material ID already exists: "+id);
            catalog.Materials.Add(new(){Id=id,RefinedFrom=original.Id,Series=original.Series,EffectGroup=original.EffectGroup,
                Hardness=original.Hardness*1.25,Weight=original.Weight,AllowedUses=new(original.AllowedUses)});
        }
    }
    public static ProcessingCatalog? UpgradeLegacy(ProcessingCatalog catalog)
    {
        if(catalog.RulesVersion is not ("2" or "3") || catalog.Validate().Count>0 || catalog.Materials.Any(m=>m.RefinedFrom is not null))return null;
        var upgraded=JsonSerializer.Deserialize<ProcessingCatalog>(DesignStorage.Serialize(catalog))!;
        var defaults=new ProcessingCatalog{Materials=upgraded.Materials.Select(m=>new MaterialDefinition{Id=m.Id}).ToList()};
        DefaultWeapons.ApplyNewDefaults(defaults);
        foreach(var original in upgraded.Materials)
        {
            var profile=defaults.Materials.Single(m=>m.Id==original.Id);
            if(profile.Series==MaterialSeries.Other)continue;
            if(original.Series==MaterialSeries.Other)original.Series=profile.Series;
            if(original.EffectGroup is null)original.EffectGroup=profile.EffectGroup;
            bool shippedGem=profile.Series==MaterialSeries.Gem && (original.Id=="diamond"
                ?original.Hardness==10 && original.Weight==4:original.Hardness==7 && original.Weight==3);
            if(shippedGem){original.Hardness=profile.Hardness;original.Weight=profile.Weight;}
        }
        try{AddRefinedMaterials(upgraded);}catch(InvalidDataException){return null;}
        if(upgraded.Validate().Count>0)return null;
        upgraded.RulesVersion=Version;return upgraded;
    }
}
