namespace BetterBeads.Data;

// All coefficients and bounds must be supplied by a confirmed template; no gameplay defaults here.
public sealed record WeaponStatRule(double Offset,double HardnessFactor,double WeightFactor,double Minimum,double Maximum)
{
    public bool IsValid=>double.IsFinite(Offset) && double.IsFinite(HardnessFactor) && double.IsFinite(WeightFactor)
        && double.IsFinite(Minimum) && double.IsFinite(Maximum) && Minimum<=Maximum;
    public double Evaluate(WeaponMaterialSummary material)=>Offset+HardnessFactor*material.AverageHardness+WeightFactor*material.AverageWeight;
    public double Finish(double value,bool integral)=>Math.Clamp(integral?Math.Round(value,MidpointRounding.AwayFromZero):value,Minimum,Maximum);
}

public sealed record WeaponStatRules(string Version,ProductUse Use,WeaponStatRule MinDamage,WeaponStatRule MaxDamage,
    WeaponStatRule Speed,WeaponStatRule Knockback,WeaponStatRule CritChance,WeaponStatRule CritMultiplier)
{
    public bool IsValid=>!string.IsNullOrWhiteSpace(Version) && WeaponMaterials.IsWeapon(Use)
        && new[]{MinDamage,MaxDamage,Speed,Knockback,CritChance,CritMultiplier}.All(r=>r is not null && r.IsValid)
        && IntegerBounds(MinDamage,1) && IntegerBounds(MaxDamage,1) && IntegerBounds(Speed,int.MinValue)
        && MinDamage.Maximum<=MaxDamage.Maximum
        && Knockback.Minimum>=0 && Knockback.Maximum<=float.MaxValue
        && CritChance.Minimum>=0 && CritChance.Maximum<=1
        && CritMultiplier.Minimum>=0 && CritMultiplier.Maximum<=float.MaxValue;
    private static bool IntegerBounds(WeaponStatRule r,int lower)=>r.Minimum>=lower && r.Maximum<=int.MaxValue
        && Math.Truncate(r.Minimum)==r.Minimum && Math.Truncate(r.Maximum)==r.Maximum;
}

public sealed record WeaponStatValues(int MinDamage,int MaxDamage,int Speed,float Knockback,float CritChance,float CritMultiplier)
{
    public Dictionary<string,double> ToSnapshot()=>new(StringComparer.Ordinal)
    {
        ["minDamage"]=MinDamage,["maxDamage"]=MaxDamage,["speed"]=Speed,["knockback"]=Knockback,
        ["critChance"]=CritChance,["critMultiplier"]=CritMultiplier
    };
    // Read final values directly. This does not need today's catalog and must never recalculate old weapons.
    public static bool TryRead(IReadOnlyDictionary<string,double> values,out WeaponStatValues? result)
    {
        result=null;
        if(!values.TryGetValue("minDamage",out double min) || !values.TryGetValue("maxDamage",out double max)
            || !values.TryGetValue("speed",out double speed) || !values.TryGetValue("knockback",out double knockback)
            || !values.TryGetValue("critChance",out double chance) || !values.TryGetValue("critMultiplier",out double multiplier)
            || new[]{min,max,speed,knockback,chance,multiplier}.Any(v=>!double.IsFinite(v))
            || min<1 || max<min || max>int.MaxValue || speed<int.MinValue || speed>int.MaxValue
            || Math.Truncate(min)!=min || Math.Truncate(max)!=max || Math.Truncate(speed)!=speed
            || knockback<0 || knockback>float.MaxValue || chance<0 || chance>1 || multiplier<0 || multiplier>float.MaxValue)return false;
        result=new((int)min,(int)max,(int)speed,(float)knockback,(float)chance,(float)multiplier);
        return true;
    }
}

public enum WeaponStatFailure { None, InvalidRules, InvalidMaterials, NonFiniteResult, InvalidEffects }
public sealed record WeaponStatResult(WeaponStatFailure Failure,WeaponStatValues? Values,WeaponMaterialSummary? Materials);

public static class WeaponStats
{
    public static WeaponStatResult Calculate(ProductUse use,WeaponStatRules rules,ProcessingCatalog catalog,IReadOnlyDictionary<string,int> bill,SpecialEffectReport? effects=null)
    {
        if(!rules.IsValid || rules.Use!=use)return new(WeaponStatFailure.InvalidRules,null,null);
        var analysis=WeaponMaterials.Analyze(use,catalog,bill);
        if(analysis.Summary is not {} material)return new(WeaponStatFailure.InvalidMaterials,null,null);
        var formulas=new[]{rules.MinDamage,rules.MaxDamage,rules.Speed,rules.Knockback,rules.CritChance,rules.CritMultiplier};
        var raw=formulas.Select(r=>r.Evaluate(material)).ToArray();
        if(effects is not null)
        {
            if(effects.Errors.Count>0)return new(WeaponStatFailure.InvalidEffects,null,material);
            double damage=(1+effects.DamageBonusPercent/100d)*(1-effects.DamagePenaltyPercent/100d);
            raw[0]*=damage;raw[1]*=damage;raw[3]*=1+effects.KnockbackBonusPercent/100d;
        }
        if(raw.Any(v=>!double.IsFinite(v)))return new(WeaponStatFailure.NonFiniteResult,null,material);
        // Permanent effects and instability penalties are applied before the one final clamp.
        var finished=formulas.Select((r,i)=>r.Finish(raw[i],i<3)).ToArray();
        int minimum=(int)finished[0],maximum=Math.Max(minimum,(int)finished[1]);
        return new(WeaponStatFailure.None,new(minimum,maximum,(int)finished[2],
            (float)finished[3],(float)finished[4],(float)finished[5]),material);
    }
}
