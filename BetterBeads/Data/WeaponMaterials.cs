using System.Collections.ObjectModel;

namespace BetterBeads.Data;

public sealed record WeaponMaterialSummary(long Total,double AverageHardness,double AverageWeight,
    IReadOnlyDictionary<string,double> MaterialFractions,IReadOnlyDictionary<string,double> EffectFractions);
public enum WeaponMaterialFailure { None, InvalidUse, InvalidBill, IncompatibleMaterial, UnconfiguredMaterial }
public sealed record WeaponMaterialResult(WeaponMaterialFailure Failure,WeaponMaterialSummary? Summary);

public static class WeaponMaterials
{
    public static bool IsWeapon(ProductUse use)=>use is ProductUse.Sword or ProductUse.Dagger or ProductUse.Hammer;

    // Consume the unified actual bill, never count grid cells again or add supplementary material twice.
    public static WeaponMaterialResult Analyze(ProductUse use,ProcessingCatalog catalog,IReadOnlyDictionary<string,int> bill)
    {
        if(!IsWeapon(use))return new(WeaponMaterialFailure.InvalidUse,null);
        if(bill.Count==0 || bill.Any(p=>string.IsNullOrWhiteSpace(p.Key) || p.Value<=0))
            return new(WeaponMaterialFailure.InvalidBill,null);
        long total=bill.Values.Sum(n=>(long)n);
        double hardness=0,weight=0;
        var fractions=new Dictionary<string,double>(StringComparer.Ordinal);
        var groups=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var pair in bill.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            var matches=catalog.Materials.Where(m=>m.Id==pair.Key).ToArray();
            if(matches.Length!=1 || !matches[0].AllowedUses.Contains(use))
                return new(WeaponMaterialFailure.IncompatibleMaterial,null);
            var material=matches[0];
            // Zero is the existing unconfigured placeholder; it must not become a live weapon stat.
            if(!double.IsFinite(material.Hardness) || !double.IsFinite(material.Weight)
                || material.Hardness<=0 || material.Weight<=0)
                return new(WeaponMaterialFailure.UnconfiguredMaterial,null);
            double fraction=pair.Value/(double)total;
            fractions[pair.Key]=fraction;
            hardness+=fraction*material.Hardness;weight+=fraction*material.Weight;
            if(!string.IsNullOrWhiteSpace(material.EffectGroup))
                groups[material.EffectGroup]=groups.GetValueOrDefault(material.EffectGroup)+pair.Value;
        }
        if(!double.IsFinite(hardness) || !double.IsFinite(weight))return new(WeaponMaterialFailure.UnconfiguredMaterial,null);
        return new(WeaponMaterialFailure.None,new(total,hardness,weight,
            new ReadOnlyDictionary<string,double>(fractions),new ReadOnlyDictionary<string,double>(
                groups.ToDictionary(p=>p.Key,p=>p.Value/(double)total,StringComparer.Ordinal))));
    }
}
