using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace BetterBeads.Runtime;

internal static class WeaponAppearancePatches
{
    internal const string Key="xinzh.BetterBeads/weaponAppearance";
    private static ProductItems products=null!;
    private static ConditionalWeakTable<MeleeWeapon,Cached> cache=new();
    private static readonly LastTextLayout<SpriteFont> descriptionLayout=new((value,font,width)=>Game1.parseText(value,font,width));
    private sealed class Cached
    {
        public string Raw="";
        public WeaponAppearance? Appearance;
        public ProductRenderData? Render;
    }
    internal static void Apply(Harmony harmony,ProductItems items)
    {
        products=items;
        harmony.Patch(AccessTools.Method(typeof(ForgeMenu),nameof(ForgeMenu.update)),transpiler:Patch(nameof(Unforge)));
        harmony.Patch(AccessTools.Method(typeof(ForgeMenu),"_UpdateDescriptionText"),postfix:Patch(nameof(Description)));
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),nameof(MeleeWeapon.Forge)),postfix:Patch(nameof(AfterForge)));
    }
    private static HarmonyMethod Patch(string name)=>new(typeof(WeaponAppearancePatches),name);
    internal static bool IsBead(MeleeWeapon weapon)=>WeaponTemplates.TryGet(weapon.ItemId,out _);
    internal static bool Handles(MeleeWeapon left,Item? right)=>right is MeleeWeapon other
        && (Managed(left)||Managed(other));
    private static bool Managed(MeleeWeapon weapon)
    {
        if(IsBead(weapon))return true;
        var entry=ReadRecord(weapon);
        // A known record invalidated by a native reset must not take over ordinary native forging.
        return entry is not null && (entry.Appearance is null || Active(weapon,entry));
    }
    private static bool Supported(MeleeWeapon weapon)=>!weapon.isScythe() && weapon.type.Value is 0 or 1 or 2 or 3
        && (IsBead(weapon)||int.TryParse(weapon.ItemId,out int id)&&id>=0);

    private static Cached? ReadRecord(MeleeWeapon weapon)
    {
        if(!weapon.modData.TryGetValue(Key,out string raw))return null;
        var entry=cache.GetValue(weapon,_=>new Cached());
        if(entry.Raw!=raw)
        {
            entry.Raw=raw;
            entry.Appearance=WeaponAppearance.TryRead(raw,out var appearance)?appearance:null;
            entry.Render=null;
        }
        return entry;
    }
    private static bool Active(MeleeWeapon weapon,Cached entry)
        =>entry.Appearance?.AppliesTo(weapon.QualifiedItemId,weapon.appearance.Value)==true;
    private static Cached? Read(MeleeWeapon weapon)
        =>ReadRecord(weapon) is {} entry && Active(weapon,entry)?entry:null;

    // Only this entrance exposes appearance data to drawing; gameplay always reads ProductItems.
    internal static ProductRenderData? ReadVisual(MeleeWeapon weapon,string drawnId)
    {
        var entry=Read(weapon);
        if(entry?.Appearance is not {Pattern:{} pattern} appearance || appearance.DrawnItemId!=drawnId)return null;
        return entry.Render??=new ProductRenderData(new ProductSnapshot{Design=pattern});
    }
    internal static string? PatternName(MeleeWeapon weapon)=>Read(weapon)?.Appearance?.Pattern?.Name;

    private static bool Source(MeleeWeapon weapon,out string drawnId,out Blueprint? pattern)
    {
        pattern=null;drawnId=weapon.GetDrawnItemId();
        if(Read(weapon)?.Appearance is {} appearance)
        {pattern=appearance.Pattern;return true;}
        // Malformed records remain blocked. Valid-but-inactive records can use the current native
        // appearance (or their original bead snapshot), never the superseded pattern.
        if(ReadRecord(weapon) is {Appearance:null})return false;
        if(!string.IsNullOrEmpty(weapon.appearance.Value))
            return drawnId.StartsWith("(W)",StringComparison.Ordinal)&&int.TryParse(drawnId[3..],out _);
        if(!IsBead(weapon))return true;
        pattern=products.Read(weapon)?.Design;
        return pattern is not null && pattern.Views["front"].Cells.Any(c=>c is not null);
    }
    internal static bool ValidPair(MeleeWeapon left,MeleeWeapon right)=>ResolvePair(left,right,out _,out _);
    private static bool ResolvePair(MeleeWeapon left,MeleeWeapon right,out string drawnId,out Blueprint? pattern)
    {
        drawnId="";pattern=null;
        return !ReferenceEquals(left,right) && Supported(left)&&Supported(right)
            && left.type.Value==right.type.Value
            && (!IsBead(left)||products.Read(left) is not null)
            && Source(right,out drawnId,out pattern);
    }

    internal static bool Forge(MeleeWeapon left,MeleeWeapon right)
    {
        if(!ResolvePair(left,right,out var id,out var pattern))return false;
        // Prepare the complete detached record before modifying anything. Native ForgeMenu pays once.
        string raw=DesignStorage.Serialize(WeaponAppearance.Create(left.QualifiedItemId,id,pattern));
        string? oldAppearance=left.appearance.Value;
        bool hadRecord=left.modData.TryGetValue(Key,out string oldRaw);
        try
        {
            left.modData[Key]=raw;
            left.appearance.Value=id;
            return true;
        }
        catch
        {
            left.appearance.Value=oldAppearance;
            if(hadRecord)left.modData[Key]=oldRaw;else left.modData.Remove(Key);
            throw;
        }
    }

    internal static void Copy(Item source,Item target)
    {
        if(source is not MeleeWeapon || target is not MeleeWeapon)return;
        if(source.modData.TryGetValue(Key,out var raw))target.modData[Key]=raw;
        else target.modData.Remove(Key);
    }

    private static void AfterForge(MeleeWeapon __instance,bool __result)
    {
        if(__result && __instance.modData.ContainsKey(Key) && Read(__instance) is null)
            __instance.modData.Remove(Key); // Native appearance/base-ID changes (e.g. Infinity upgrades) win.
    }

    internal static void ResetAppearance(MeleeWeapon weapon)
    {
        if(Read(weapon) is null)return;
        weapon.modData.Remove(Key);
        weapon.appearance.Value=null;
        weapon.ResetIndexOfMenuItemView();
    }

    private static IEnumerable<CodeInstruction> Unforge(IEnumerable<CodeInstruction> instructions,ILGenerator generator)
    {
        var codes=instructions.ToList();
        var field=AccessTools.Field(typeof(MeleeWeapon),nameof(MeleeWeapon.appearance));
        int index=codes.FindIndex(c=>c.opcode==OpCodes.Ldfld && Equals(c.operand,field));
        if(index<0)throw new InvalidOperationException("Native forge appearance-reset branch was not found.");
        // This is inside the completed-unforge branch, after gem removal, before donor reconstruction.
        // Keep native gem refunds, but don't create a stat-less bead donor or refund the skin payment.
        var duplicate=new CodeInstruction(OpCodes.Dup);
        duplicate.labels.AddRange(codes[index].labels);codes[index].labels.Clear();
        duplicate.blocks.AddRange(codes[index].blocks);codes[index].blocks.Clear();
        codes.InsertRange(index,new[]{duplicate,CodeInstruction.Call(typeof(WeaponAppearancePatches),nameof(ResetAppearance))});
        int payment=codes.FindIndex(c=>c.operand is MethodInfo m && m.Name=="ReduceId");
        int start=payment<0?-1:codes.FindLastIndex(payment,c=>c.operand is MethodInfo m && m.DeclaringType==typeof(Game1)&&m.Name=="get_player");
        if(start<index)throw new InvalidOperationException("Native forge payment branch was not found.");
        // Recheck just before native payment, after its animation. Invalid late changes spend nothing.
        var next=generator.DefineLabel();
        var enter=new CodeInstruction(OpCodes.Ldarg_0);
        enter.labels.AddRange(codes[start].labels);codes[start].labels.Clear();
        enter.blocks.AddRange(codes[start].blocks);codes[start].blocks.Clear();
        codes[start].labels.Add(next);
        codes.InsertRange(start,new[]{enter,CodeInstruction.Call(typeof(WeaponAppearancePatches),nameof(BeforePayment)),
            new CodeInstruction(OpCodes.Brtrue,next),new CodeInstruction(OpCodes.Ret)});
        return codes;
    }

    private static bool BeforePayment(ForgeMenu menu)
    {
        if(menu.leftIngredientSpot.item is not MeleeWeapon left || menu.rightIngredientSpot.item is not MeleeWeapon right
            || !Handles(left,right))return true;
        if(ValidPair(left,right) && Game1.player.Items.CountId("(O)848")>=10 && menu.CanFitCraftedItem())return true;
        AccessTools.FieldRefAccess<ForgeMenu,int>("_timeUntilCraft")(menu)=0;
        AccessTools.FieldRefAccess<ForgeMenu,int>("_sparklingTimer")(menu)=0;
        AccessTools.Method(typeof(ForgeMenu),"_ValidateCraft").Invoke(menu,null);
        Game1.showRedMessage(ContentText.Get("forge.appearance.changed","幻化条件已改变，请检查武器、背包空间与火山晶石。"));
        return false;
    }

    private static void Description(ForgeMenu __instance,ref string ___displayedDescription)
    {
        if(__instance.IsBusy())return;
        if(__instance.leftIngredientSpot.item is not MeleeWeapon left)return;
        var right=__instance.rightIngredientSpot.item;
        if(right is null && Read(left) is not null)
        {
            ___displayedDescription=descriptionLayout.Get(ContentText.Get("forge.appearance.unforge","解除锻造将移除锻造并恢复原外观；不返还外观武器。"),Game1.smallFont,320);
            return;
        }
        if(right is not MeleeWeapon other || !Handles(left,other))return;
        if(!ValidPair(left,other))
            ___displayedDescription=descriptionLayout.Get(ContentText.Get("forge.appearance.invalid","需要同类型、图案有效的武器；仅支持原版与拼豆武器。"),Game1.smallFont,320);
        else if(Game1.player.Items.CountId("(O)848")>=10 && __instance.CanFitCraftedItem())
            ___displayedDescription=descriptionLayout.Get(ContentText.Get("forge.appearance.confirm","保留左侧属性，采用右侧外观。消耗右侧武器和10个火山晶石。"),Game1.smallFont,320);
        // Native missing-shard feedback is retained.
    }
    internal static void Clear(){cache=new();descriptionLayout.Clear();}
}
