namespace BetterBeads.Data;

public enum SpecialEffectState { Absent, Weak, Ready, Overloaded }
public sealed record SpecialEffectRatio(string Id,long Count,long Total,SpecialEffectState State)
{
    public double Percent=>Total>0?Count*100d/Total:0;
}
public sealed record SpecialEffectReport(IReadOnlyList<SpecialEffectRatio> Ratios,IReadOnlyList<string> Active,
    double DamageBonusPercent,double KnockbackBonusPercent,int DamagePenaltyPercent,double LifeStealRate,IReadOnlyList<string> Errors);

public static class SpecialEffects
{
    public const string RulesVersion="effects-2";
    private static readonly string[] kinds={"ruby","jade","amethyst"};
    public static bool ValidSnapshot(ProductSnapshot snapshot)
    {
        if(snapshot.EffectRulesVersion is null)return snapshot.Effects.Count==0 && snapshot.EffectParameters.Count==0;
        if(snapshot.EffectRulesVersion==RulesVersion)
        {
            var p=snapshot.EffectParameters;
            return snapshot.Effects.Count<=2 && snapshot.Effects.Distinct().Count()==snapshot.Effects.Count
                && snapshot.Effects.All(kinds.Contains) && p.Count==4
                && p.TryGetValue("lifeStealRate",out var life) && life>=0 && life<=0.03 && (life>0)==snapshot.Effects.Contains("jade")
                && p.TryGetValue("damageBonusPercent",out var damage) && damage>=0 && damage<=12 && (damage>0)==snapshot.Effects.Contains("ruby")
                && p.TryGetValue("knockbackBonusPercent",out var knockback) && knockback>=0 && knockback<=20 && (knockback>0)==snapshot.Effects.Contains("amethyst")
                && p.TryGetValue("damagePenaltyPercent",out var penaltyValue) && penaltyValue==0;
        }
        return snapshot.EffectRulesVersion=="effects-1" && snapshot.Effects.Count<=2
            && snapshot.Effects.Distinct(StringComparer.Ordinal).Count()==snapshot.Effects.Count
            && snapshot.Effects.All(id=>kinds.Contains(id)) && snapshot.EffectParameters.Count==2
            && snapshot.EffectParameters.TryGetValue("lifeStealRate",out double rate)
            && rate==(snapshot.Effects.Contains("jade")?0.05:0)
            && snapshot.EffectParameters.TryGetValue("damagePenaltyPercent",out double penalty) && penalty is 0 or 10 or 20 or 30;
    }
    public static SpecialEffectReport Evaluate(ProductUse use,ProcessingCatalog catalog,IReadOnlyDictionary<string,int> bill,
        IReadOnlyList<string> selected)
    {
        var original=EvaluateLegacy(use,catalog,bill,selected);
        if(original.Errors.Count>0)return original;
        var ratios=original.Ratios.Select(r=>r with{State=r.Count==0?SpecialEffectState.Absent:SpecialEffectState.Ready}).ToArray();
        var active=ratios.Where(r=>r.Count>0 && selected.Contains(r.Id)).Select(r=>r.Id).ToArray();
        double Strength(string id)=>active.Contains(id)?Math.Min(1,ratios.Single(r=>r.Id==id).Count*5d/ratios[0].Total):0;
        return new(ratios,active,12*Strength("ruby"),20*Strength("amethyst"),0,0.03*Strength("jade"),Array.Empty<string>());
    }
    public static Dictionary<string,double> Parameters(SpecialEffectReport effects)=>new()
    {{"lifeStealRate",effects.LifeStealRate},{"damagePenaltyPercent",effects.DamagePenaltyPercent},
        {"damageBonusPercent",effects.DamageBonusPercent},{"knockbackBonusPercent",effects.KnockbackBonusPercent}};
    public static string Summary(string id,SpecialEffectReport e)=>id switch
    {"ruby"=>ContentText.Format("copy.SpecialEffects.50078dca31",$"红宝石：伤害 +{e.DamageBonusPercent:0.##}%"),"jade"=>ContentText.Format("copy.SpecialEffects.ba32a2d6c7",$"翡翠：实际伤害吸血 {e.LifeStealRate*100:0.##}%"),_=>ContentText.Format("copy.SpecialEffects.a7bdbe8d5c",$"紫水晶：击退 +{e.KnockbackBonusPercent:0.##}%")};
    public static SpecialEffectReport EvaluateLegacy(ProductUse use,ProcessingCatalog catalog,IReadOnlyDictionary<string,int> bill,
        IReadOnlyList<string> selected)
    {
        var errors=new List<string>();
        if(!WeaponMaterials.IsWeapon(use) || catalog.Validate().Count>0 || !MaterialRules.Allows(catalog,use,bill))
            return new(Array.Empty<SpecialEffectRatio>(),Array.Empty<string>(),0,0,0,0,new[]{"invalid-materials"});
        if(selected.Count>2)errors.Add("too-many-effects");
        if(selected.Distinct(StringComparer.Ordinal).Count()!=selected.Count || selected.Any(id=>!kinds.Contains(id)))errors.Add("invalid-selection");
        long total=bill.Values.Sum(n=>(long)n);
        var counts=kinds.ToDictionary(id=>id,_=>0L,StringComparer.Ordinal);
        foreach(var pair in bill)
        {
            var material=catalog.Materials.Single(m=>m.Id==pair.Key);
            // Refinement identity is validated above; the explicit inherited group counts each actual bead once.
            if(material.EffectGroup is {} group && counts.ContainsKey(group))counts[group]+=pair.Value;
        }
        var ratios=kinds.Select(id=>
        {
            long count=counts[id];
            var state=count==0?SpecialEffectState.Absent:count*100<total*10?SpecialEffectState.Weak:
                count*100>total*25?SpecialEffectState.Overloaded:SpecialEffectState.Ready;
            return new SpecialEffectRatio(id,count,total,state);
        }).ToArray();
        var active=errors.Count>0?Array.Empty<string>():ratios.Where(r=>r.State==SpecialEffectState.Ready && selected.Contains(r.Id)).Select(r=>r.Id).ToArray();
        int penalty=Math.Min(30,ratios.Count(r=>r.State==SpecialEffectState.Overloaded)*10);
        return new(Array.AsReadOnly(ratios),Array.AsReadOnly(active),active.Contains("ruby")?20:0,active.Contains("amethyst")?30:0,
            penalty,active.Contains("jade")?0.05:0,errors.AsReadOnly());
    }
}
