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
        public ProductSnapshot Snapshot=null!;public bool Ordinary;public HashSet<Monster> Wounded=new();
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
            ?new Attack{Previous=__state,Weapon=__instance,Farmer=who,Rate=snapshot.EffectParameters.GetValueOrDefault("lifeStealRate"),Creative=snapshot.CreatedInCreativeMode,
                Snapshot=snapshot,Ordinary=!__instance.isOnSpecial&&(__instance.type.Value!=1||MeleeWeapon.daggerHitsLeft<=0)}:null;
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
    private static IEnumerable<CodeInstruction> WrapHit(IEnumerable<CodeInstruction> instructions,ILGenerator generator)
    {
        var original=AccessTools.Method(typeof(Monster),"takeDamage",new[]{typeof(int),typeof(int),typeof(int),typeof(bool),typeof(double),typeof(Farmer)});
        var code=instructions.ToArray();
        var roll=AccessTools.Method(typeof(Random),nameof(Random.Next),new[]{typeof(int),typeof(int)});
        int rollIndex=Enumerable.Range(0,code.Length-1).FirstOrDefault(i=>code[i].Calls(roll)&&code[i+1].IsStloc()
            &&code[i+1].operand is LocalBuilder local&&local.LocalIndex==9,-1);
        if(code.Count(i=>i.Calls(original))!=1||rollIndex<0||!code[rollIndex+1].IsStloc()
            ||code[rollIndex+1].operand is not LocalBuilder nativeRoll||nativeRoll.LocalIndex!=9)
            throw new InvalidOperationException("Unsupported monster damage call layout.");
        var baseline=generator.DeclareLocal(typeof(int));
        for(int i=0;i<code.Length;i++)
        {
            var instruction=code[i];
            if(instruction.Calls(original))
            {
                var load=new CodeInstruction(OpCodes.Ldloc,baseline);load.labels.AddRange(instruction.labels);instruction.labels.Clear();yield return load;
                instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(WeaponLifeSteal),nameof(TakeDamage));
            }
            yield return instruction;
            if(i==rollIndex){yield return new(OpCodes.Dup);yield return new(OpCodes.Stloc,baseline);}
        }
    }
    private static int TakeDamage(Monster monster,int damage,int xTrajectory,int yTrajectory,bool isBomb,double precision,Farmer who,int baseRoll)
    {
        var attack=current;int before=monster.Health;
        int reported=monster.takeDamage(damage,xTrajectory,yTrajectory,isBomb,precision,who);
        if(attack is not null && ReferenceEquals(attack.Farmer,who) && !isBomb)
        {
            attack.Damage=Math.Min(int.MaxValue,attack.Damage+LifeSteal.ActualDamage(before,monster.Health,reported));
            if(reported>0&&monster.Health<before&&attack.Ordinary&&attack.Wounded.Add(monster))
            {
                double baseline=Math.Max(1,baseRoll+who.Attack*3);
                if(who.professions.Contains(24))baseline=Math.Ceiling(baseline*1.1);
                if(who.professions.Contains(26))baseline=Math.Ceiling(baseline*1.15);
                baseline=Math.Max(0,baseline-monster.resilience.Value);
                ShapeCombat.HitMonster(monster,who,attack.Snapshot,baseline);
            }
#if !BEADS_LITE
            if(before>0 && monster.Health<=0 && reported>0)WorkshopService.Current?.RecordKill(attack.Creative);
#endif
        }
        return reported;
    }
}
