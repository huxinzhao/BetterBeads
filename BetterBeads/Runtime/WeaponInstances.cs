using BetterBeads.Data;
using StardewValley.Tools;

namespace BetterBeads.Runtime;

internal static class WeaponInstances
{
    public static bool Apply(MeleeWeapon item,ProductSnapshot? snapshot)
    {
        if(snapshot is null || !WeaponTemplates.TryGet(item.ItemId,out var template)
            || item.QualifiedItemId!="(W)"+template.Id || !ProductTemplates.Matches(snapshot)
            || snapshot.Design.TemplateId!=template.Id || !WeaponStatValues.TryRead(snapshot.FinalStats,out var stats))return false;
        // Assign final saved values. Never add modifiers or consult today's material rules on load/equip.
        item.type.Value=template.NativeType;
        item.minDamage.Value=stats!.MinDamage;item.maxDamage.Value=stats.MaxDamage;item.speed.Value=stats.Speed;
        item.knockback.Value=stats.Knockback;item.critChance.Value=stats.CritChance;item.critMultiplier.Value=stats.CritMultiplier;
        item.addedAreaOfEffect.Value=0;item.addedDefense.Value=0;item.addedPrecision.Value=0;
        return true;
    }
}
