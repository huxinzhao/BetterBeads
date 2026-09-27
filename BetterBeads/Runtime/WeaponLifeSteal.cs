using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BetterBeads.Data;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace BetterBeads.Runtime;

internal static class WeaponLifeSteal
{
    private static ProductItems products=null!;
    [ThreadStatic] private static Attack? current;
    private static ConditionalWeakTable<MeleeWeapon,Balance> balances=new();
    private sealed class Balance { public Farmer? Owner;public double Remainder; }
    private sealed class Attack
    {
        public Attack? Previous;public MeleeWeapon Weapon=null!;public Farmer Farmer=null!;public long Damage;public double Rate;public bool Creative;
    }
    public static void Apply(Harmony harmony,ProductItems items)
    {
        products=items;
        harmony.Patch(AccessTools.Method(typeof(MeleeWeapon),"DoDamage"),prefix:new(typeof(WeaponLifeSteal),nameof(Begin)),
            finalizer:new(typeof(WeaponLifeSteal),nameof(End)));
        var signature=new[]{typeof(Microsoft.Xna.Framework.Rectangle),typeof(int),typeof(int),typeof(bool),typeof(float),typeof(int),
            typeof(float),typeof(float),typeof(bool),typeof(Farmer),typeof(bool)};
        harmony.Patch(AccessTools.Method(typeof(GameLocation),"damageMonster",signature),transpiler:new(typeof(WeaponLifeSteal),nameof(WrapHit)));
    }
    public static void Clear(){current=null;balances=new();}
    private static void Begin(MeleeWeapon __instance,Farmer who,out Attack? __state)
    {
        __state=current;
        // Nested attacks never inherit the outer weapon's bonus.
        current=who==Game1.player && products.Read(__instance) is {} snapshot
            ?new Attack{Previous=__state,Weapon=__instance,Farmer=who,Rate=snapshot.EffectParameters.GetValueOrDefault("lifeStealRate"),Creative=snapshot.CreatedInCreativeMode}:null;
    }
    private static Exception? End(Exception? __exception,Attack? __state)
    {
        var attack=current;current=__state;
        if(__exception is null && attack is not null)
        {
            var balance=balances.GetValue(attack.Weapon,_=>new Balance());
            if(!ReferenceEquals(balance.Owner,attack.Farmer)){balance.Owner=attack.Farmer;balance.Remainder=0;}
            attack.Farmer.health+=LifeSteal.HealRate(attack.Damage,attack.Farmer.health,attack.Farmer.maxHealth,attack.Rate,ref balance.Remainder);
        }
        return __exception;
    }
    private static IEnumerable<CodeInstruction> WrapHit(IEnumerable<CodeInstruction> instructions)
    {
        var original=AccessTools.Method(typeof(Monster),"takeDamage",new[]{typeof(int),typeof(int),typeof(int),typeof(bool),typeof(double),typeof(Farmer)});
        var code=instructions.ToArray();
        if(code.Count(i=>i.Calls(original))!=1)throw new InvalidOperationException("Unsupported monster damage call layout.");
        foreach(var instruction in code)
        {
            if(instruction.Calls(original)){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(WeaponLifeSteal),nameof(TakeDamage));}
            yield return instruction;
        }
    }
    private static int TakeDamage(Monster monster,int damage,int xTrajectory,int yTrajectory,bool isBomb,double precision,Farmer who)
    {
        var attack=current;int before=monster.Health;
        int reported=monster.takeDamage(damage,xTrajectory,yTrajectory,isBomb,precision,who);
        if(attack is not null && ReferenceEquals(attack.Farmer,who) && !isBomb)
        {
            attack.Damage=Math.Min(int.MaxValue,attack.Damage+LifeSteal.ActualDamage(before,monster.Health,reported));
#if !BEADS_LITE
            if(before>0 && monster.Health<=0 && reported>0)WorkshopService.Current?.RecordKill(attack.Creative);
#endif
        }
        return reported;
    }
}
