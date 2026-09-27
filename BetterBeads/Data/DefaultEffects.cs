using System.Text.Json;

namespace BetterBeads.Data;

public static class DefaultEffects
{
    // Version 2 predates series/effect metadata. Preserve all configured numerical profiles and processing yields.
    public static ProcessingCatalog? UpgradeLegacy(ProcessingCatalog catalog)
    {
        if(catalog.RulesVersion!="2" || catalog.Validate().Count>0)return null;
        var known=catalog.Materials.Where(m=>m.Id is "copper" or "iron" or "gold" or "iridium" or "emerald" or "aquamarine"
            or "ruby" or "amethyst" or "topaz" or "jade" or "diamond").ToArray();
        if(known.Length!=11 || known.Any(m=>m.RefinedFrom is not null))return null;
        var upgraded=JsonSerializer.Deserialize<ProcessingCatalog>(DesignStorage.Serialize(catalog))!;
        foreach(var material in upgraded.Materials.Where(m=>known.Any(k=>k.Id==m.Id)))
        {
            if(material.Series==MaterialSeries.Other)
                material.Series=material.Id is "copper" or "iron" or "gold" or "iridium"?MaterialSeries.Metal:MaterialSeries.Gem;
            if(material.EffectGroup is null && material.Id is "ruby" or "jade" or "amethyst")material.EffectGroup=material.Id;
        }
        if(upgraded.Validate().Count>0)return null;
        upgraded.RulesVersion="3";return upgraded;
    }
}
