using System.Text.Json;

namespace BetterBeads.Data;

public static class DefaultWeapons
{
    public static IReadOnlyList<WeaponTemplateDefinition> Templates()=>new[]{ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer}
#if BEADS_LITE
        .SelectMany(use=>new[]{16,24,32}.Select(size=>(use,size)))
#else
        .Select(use=>(use,size:16))
#endif
        .Select(v=>new WeaponTemplateDefinition("xinzh.BetterBeads."+v.use+v.size,v.use,v.size,v.size,
            "product."+v.use,"product.weapon-description","Mods/xinzh.BetterBeads/"+v.use+"Placeholder"+(v.size==16?"":v.size))).ToArray();

    public static IReadOnlyList<ManufacturingRecipe> Recipes()=>Templates().Select(t=>new ManufacturingRecipe(
        new(t.Id,t.Use,t.PixelWidth,t.PixelHeight,new[]{"front"},"front",MinimumBeads:1,ManufacturingAvailable:true,
            MinimumMaterials:t.Use==ProductUse.Sword?16:t.Use==ProductUse.Dagger?12:24),"(W)"+t.Id,0,Rules(t.Use),SpecialEffectsEnabled:true)).ToArray();

    private static WeaponStatRules Rules(ProductUse use)
    {
        bool sword=use==ProductUse.Sword,dagger=use==ProductUse.Dagger;
        return new("weapons-2",use,new(0,sword?5:dagger?3:7,0,1,120),new(0,sword?7:dagger?4:10,0,1,120),
            new(sword?2:dagger?3:1,0,sword?-1d/3:dagger?-1d/4:-1d/2,sword?-4:dagger?-2:-8,sword?4:dagger?5:2),
            new(sword?0.6:dagger?0.2:1,0,sword?0.04:dagger?0.02:0.08,0,sword?2:dagger?1:3),
            new(0.02,0,0,0,0.25),new(3,0,0,1,5));
    }

    private static readonly IReadOnlyDictionary<string,(double Hardness,double Weight)> profiles=new Dictionary<string,(double,double)>
    {
        ["copper"]=(3,5),["iron"]=(5,6),["gold"]=(4,9),["iridium"]=(9,8),
        ["emerald"]=(7.5,3.2),["aquamarine"]=(7,2.8),["ruby"]=(9,4),["amethyst"]=(6,3),["topaz"]=(8,3.5),["jade"]=(6.5,4),["diamond"]=(10,3.5)
    };
    public static void ApplyNewDefaults(ProcessingCatalog catalog)
    {
        foreach(var material in catalog.Materials)if(profiles.TryGetValue(material.Id,out var profile))
        {
            material.Hardness=profile.Hardness;material.Weight=profile.Weight;
            material.Series=material.Id is "copper" or "iron" or "gold" or "iridium"?MaterialSeries.Metal:MaterialSeries.Gem;
            material.EffectGroup=material.Id is "ruby" or "jade" or "amethyst"?material.Id:null;
        }
    }

    // Recognize only the shipped pre-weapon placeholders; never overwrite a customized nonzero profile.
    public static ProcessingCatalog? UpgradeLegacy(ProcessingCatalog catalog)
    {
        if(catalog.RulesVersion!="1" || catalog.Validate().Count>0)return null;
        var originals=catalog.Materials.Where(m=>profiles.ContainsKey(m.Id)).ToArray();
        if(originals.Length!=profiles.Count || originals.Any(m=>m.Hardness!=0 || m.Weight!=0))return null;
        var upgraded=JsonSerializer.Deserialize<ProcessingCatalog>(DesignStorage.Serialize(catalog))!;
        ApplyNewDefaults(upgraded);upgraded.RulesVersion="2";return upgraded;
    }
}
