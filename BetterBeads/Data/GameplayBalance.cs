using System.Text.Json;
namespace BetterBeads.Data;

public static class GameplayBalance
{
    public const string Version="5";
    private static readonly Dictionary<string,(double Old,double New)> hardness=new()
    {
        ["gold"]=(4,6),["emerald"]=(7.5,4.5),["aquamarine"]=(7,4.2),["ruby"]=(9,5.4),
        ["amethyst"]=(6,3.6),["topaz"]=(8,4.8),["jade"]=(6.5,3.9),["diamond"]=(10,6)
    };
    public static void ApplyDefaults(ProcessingCatalog catalog)
    {foreach(var m in catalog.Materials)if(hardness.TryGetValue(m.Id,out var h))m.Hardness=h.New;}
    public static ProcessingCatalog? Upgrade(ProcessingCatalog catalog)
    {
        if(catalog.RulesVersion!="4" || catalog.Validate().Count>0)return null;
        var next=JsonSerializer.Deserialize<ProcessingCatalog>(DesignStorage.Serialize(catalog))!;
        foreach(var m in next.Materials)
        {
            string id=m.RefinedFrom??m.Id;double factor=m.RefinedFrom is null?1:1.25;
            // Each independently customized numerical value survives migration.
            if(hardness.TryGetValue(id,out var h) && m.Hardness==h.Old*factor)m.Hardness=h.New*factor;
        }
        if(!next.Sources.Any(s=>s.ItemId=="(O)428"))next.Sources.Add(new(){Id="item.428",ItemId="(O)428",MaterialId="wool",Yield=36});
        next.RulesVersion=Version;return next;
    }
    public static bool UpgradeClothing(ClothingSettings value)
    {
        bool changed=false;
        if(value.HatWool==80){value.HatWool=24;changed=true;}
        if(value.ShirtWool==120){value.ShirtWool=48;changed=true;}
        if(value.PantsWool==160){value.PantsWool=72;changed=true;}
        return changed;
    }
    public static bool ProtectedSource(string itemId,int category,bool allowed)=>!allowed && (itemId=="(O)709" || category==-80);
}
