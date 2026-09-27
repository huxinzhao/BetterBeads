using System.Reflection;
using System.Reflection.Emit;
using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace BetterBeads.Runtime;

internal static class WeaponPatches
{
    private static ProductItems products=null!;
    private static ITranslationHelper text=null!;
    public static void Apply(Harmony harmony,ProductItems items,ITranslationHelper translations)
    {
        products=items;text=translations;
        WeaponAppearancePatches.Apply(harmony,items);
        WeaponLifeSteal.Apply(harmony,items);
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"getDescription"),postfix:Patch(nameof(Describe)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"ReloadData"),postfix:Patch(nameof(Restore)));
        foreach(string method in new[]{"drawTooltip","setFarmerAnimating","animateSpecialMove"})
            harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),method),prefix:Patch(nameof(Restore)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"DoDamage"),prefix:Patch(nameof(BeforeDamage)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"getAreaOfEffect"),postfix:Patch(nameof(ScaleArea)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"triggerClubFunction"),transpiler:Patch(nameof(HammerSpecial)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"CanForge"),prefix:Patch(nameof(CanForge)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"Forge"),prefix:Patch(nameof(Forge)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"CanAddEnchantment"),prefix:Patch(nameof(DisallowUnimplementedUpgrade)));
    }
    private static HarmonyMethod Patch(string name)=>new(typeof(WeaponPatches),name);
    private static void Describe(MeleeWeapon __instance,ref string __result)
    {
        if(WeaponAppearancePatches.PatternName(__instance) is {} patternName)
            __result+="\n"+Game1.parseText(ContentText.Format("forge.appearance.description",$"拼豆外观：{patternName}"),Game1.smallFont,320);
        if(products.Read(__instance) is not {} snapshot)return;
#if BEADS_LITE
        __result+="\n"+Game1.parseText(ProductLabels.SaleLine(snapshot),Game1.smallFont,320);
#endif
        var lines=snapshot.EffectRulesVersion==SpecialEffects.RulesVersion
            ?snapshot.Effects.Select(id=>id switch{"ruby"=>ContentText.Format("copy.WeaponPatches.50078dca31",$"红宝石：伤害 +{snapshot.EffectParameters.GetValueOrDefault("damageBonusPercent"):0.##}%"),
                "jade"=>ContentText.Format("copy.WeaponPatches.ba32a2d6c7",$"翡翠：实际伤害吸血 {snapshot.EffectParameters.GetValueOrDefault("lifeStealRate")*100:0.##}%"),
                _=>ContentText.Format("copy.WeaponPatches.a7bdbe8d5c",$"紫水晶：击退 +{snapshot.EffectParameters.GetValueOrDefault("knockbackBonusPercent"):0.##}%")}).ToList()
            :snapshot.Effects.Select(id=>text.Get("effect.description."+id).ToString()).ToList();
        if(snapshot.GalaxySoul is {} soul)lines.Add(text.Get("soul.level",new{level=soul.Level,bonus=soul.Level*10}).ToString());
        if(snapshot.EffectParameters.GetValueOrDefault("damagePenaltyPercent") is >0 and var penalty)
            lines.Add(text.Get("effect.penalty",new{penalty}).ToString());
        if(lines.Count>0)__result+="\n"+Game1.parseText(string.Join("\n",lines),Game1.smallFont,320);
    }
    private static bool IsCustom(MeleeWeapon item)=>item.QualifiedItemId=="(W)"+item.ItemId && WeaponTemplates.TryGet(item.ItemId,out _);
    private static void Restore(MeleeWeapon __instance)
    {
        if(IsCustom(__instance))WeaponInstances.Apply(__instance,products.Read(__instance));
    }
    private static bool BeforeDamage(MeleeWeapon __instance)
        =>!IsCustom(__instance) || WeaponInstances.Apply(__instance,products.Read(__instance));
    private static void ScaleArea(MeleeWeapon __instance,int facingDirection,Rectangle wielderBoundingBox,ref Rectangle __result)
    {
        if(!IsCustom(__instance)||products.Read(__instance) is not {} snapshot)return;
        double factor=snapshot.FinalStats.GetValueOrDefault("reachScale",1);
        if(snapshot.WeaponRulesVersion==SimpleCrafting.Version)
        {
            if(!double.IsFinite(factor)||factor<.5||factor>2)return;
            var forward=WeaponReach.ExtendForward(__result.X,__result.Y,__result.Width,__result.Height,
                wielderBoundingBox.Left,wielderBoundingBox.Top,wielderBoundingBox.Right,wielderBoundingBox.Bottom,facingDirection,factor);
            __result=new Rectangle(forward.X,forward.Y,forward.Width,forward.Height);
            return;
        }
        if(snapshot.WeaponRulesVersion!="simple-1"||factor is not (1.5 or 2)
            ||snapshot.Design.Views["front"].Width/16d!=factor)return;
        var area=WeaponReach.Scale(__result.X,__result.Y,__result.Width,__result.Height,
            wielderBoundingBox.Center.X,wielderBoundingBox.Center.Y,factor);
        __result=new Rectangle(area.X,area.Y,area.Width,area.Height);
    }
    private static IEnumerable<CodeInstruction> HammerSpecial(IEnumerable<CodeInstruction> instructions)
    {
        var codes=instructions.ToList();
        int index=-1;
        for(int i=2;i<codes.Count;i++)
            if(codes[i].opcode==OpCodes.Newobj && codes[i].operand is ConstructorInfo ctor
                && ctor.DeclaringType==typeof(Rectangle) && codes[i-1].operand is int h && h==384
                && codes[i-2].operand is int w && w==384){index=i;break;}
        if(index<0)return codes; // A changed game method keeps its native area instead of preventing mod startup.
        codes.InsertRange(index+1,new[]{new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.Call(typeof(WeaponPatches),nameof(ScaleHammerSpecial))});
        return codes;
    }
    private static Rectangle ScaleHammerSpecial(Rectangle area,MeleeWeapon weapon)
    {
        if(!IsCustom(weapon)||products.Read(weapon) is not {} snapshot
            ||snapshot.WeaponRulesVersion!=SimpleCrafting.Version)return area;
        double factor=snapshot.FinalStats.GetValueOrDefault("reachScale",1);
        if(!double.IsFinite(factor)||factor<.5||factor>2)return area;
        var scaled=WeaponReach.ScaleCentered(area.X,area.Y,area.Width,area.Height,factor);
        return new Rectangle(scaled.X,scaled.Y,scaled.Width,scaled.Height);
    }
    private static bool DisallowUnimplementedUpgrade(MeleeWeapon __instance,ref bool __result)
    {
        if(!IsCustom(__instance))return true;
        __result=false;return false;
    }
    private static bool IsSoul(Item item)=>item is StardewValley.Object obj && obj.GetType()==typeof(StardewValley.Object)
        && obj.QualifiedItemId=="(O)896" && obj.Stack>0 && obj.Quality==0 && InventoryService.IsPlainObject(obj);
    private static bool CanForge(MeleeWeapon __instance,Item item,ref bool __result)
    {
        if(WeaponAppearancePatches.Handles(__instance,item))
        {__result=WeaponAppearancePatches.ValidPair(__instance,(MeleeWeapon)item);return false;}
        if(!IsCustom(__instance))return true;
        __result=FeatureAccess.Has("souls") && !Context.IsMultiplayer && IsSoul(item) && products.Read(__instance) is {} snapshot && SoulUpgrades.TryUpgrade(snapshot,out _);
        return false;
    }
    private static bool Forge(MeleeWeapon __instance,Item item,ref bool __result)
    {
        if(WeaponAppearancePatches.Handles(__instance,item))
        {__result=WeaponAppearancePatches.Forge(__instance,(MeleeWeapon)item);return false;}
        if(!IsCustom(__instance))return true;
        __result=false;
        if(!FeatureAccess.Has("souls") || Context.IsMultiplayer || !IsSoul(item) || products.Read(__instance) is not {} snapshot
            || !SoulUpgrades.TryUpgrade(snapshot,out var upgraded))return false;
        // ForgeMenu previews use getOne() clones. Native ForgeMenu owns shard/soul costs; no second deduction here.
        string raw=DesignStorage.Serialize(upgraded);
        if(!WeaponInstances.Apply(__instance,upgraded))return false;
        __instance.modData[ProductItems.SnapshotKey]=raw;
        __result=true;return false;
    }
}
