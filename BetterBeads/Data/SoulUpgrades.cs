namespace BetterBeads.Data;

public sealed class GalaxySoulState
{
    public string RulesVersion { get; set; }="souls-1";
    public int Level { get; set; }
    public int BaseMinDamage { get; set; }
    public int BaseMaxDamage { get; set; }
    public GalaxySoulState Copy()=>new(){RulesVersion=RulesVersion,Level=Level,BaseMinDamage=BaseMinDamage,BaseMaxDamage=BaseMaxDamage};
}
public static class SoulUpgrades
{
    public const int MaximumLevel=3;
    private static int Damage(int baseline,int level)=>(int)Math.Clamp(Math.Round(baseline*(100+level*10)/100d,MidpointRounding.AwayFromZero),1,120);
    public static bool ValidSnapshot(ProductSnapshot snapshot)
    {
        if(snapshot.GalaxySoul is not {} soul)return true;
        return (snapshot.WeaponRulesVersion is "weapons-1" or "weapons-2") && WeaponMaterials.IsWeapon(snapshot.Design.Use)
            && soul.RulesVersion=="souls-1" && soul.Level is >=1 and <=MaximumLevel
            && soul.BaseMinDamage>=1 && soul.BaseMaxDamage>=soul.BaseMinDamage && soul.BaseMaxDamage<=120
            && snapshot.FinalStats.GetValueOrDefault("minDamage")==Damage(soul.BaseMinDamage,soul.Level)
            && snapshot.FinalStats.GetValueOrDefault("maxDamage")==Damage(soul.BaseMaxDamage,soul.Level);
    }
    public static bool TryUpgrade(ProductSnapshot snapshot,out ProductSnapshot? upgraded)
    {
        upgraded=null;
        if(!ProductTemplates.Matches(snapshot) || snapshot.WeaponRulesVersion is not ("weapons-1" or "weapons-2")
            || !WeaponStatValues.TryRead(snapshot.FinalStats,out var stats) || stats!.MaxDamage>120
            || (snapshot.GalaxySoul?.Level??0)>=MaximumLevel)return false;
        upgraded=snapshot.Copy();
        upgraded.GalaxySoul??=new(){BaseMinDamage=stats.MinDamage,BaseMaxDamage=stats.MaxDamage};
        upgraded.GalaxySoul.Level++;
        upgraded.FinalStats["minDamage"]=Damage(upgraded.GalaxySoul.BaseMinDamage,upgraded.GalaxySoul.Level);
        upgraded.FinalStats["maxDamage"]=Damage(upgraded.GalaxySoul.BaseMaxDamage,upgraded.GalaxySoul.Level);
        return true;
    }
}
