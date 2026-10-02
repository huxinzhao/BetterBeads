using System.Reflection;
using System.Runtime.Serialization;
using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;

internal static class ShapeRuntimeChecks
{
    private static Monster? killed;private static Farmer? credited;private static int kills;
    private static bool KillFixture(Farmer who,Monster monster){killed=monster;credited=who;kills++;return false;}
    private static bool DeathFixture(Monster __instance){((TargetMonster)__instance).Deaths++;return false;}
    private sealed class TargetMonster:Monster
    {
        public int Deaths;
        public override Rectangle GetBoundingBox()=>new(100,100,64,64);
    }
    public static void Run()
    {
        int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
        var type=typeof(Blueprint).Assembly.GetType("BetterBeads.Runtime.ShapeCombat",true)!;
        var tick=type.GetMethod("ApplyTick",BindingFlags.NonPublic|BindingFlags.Static)!;
        var nativeKill=AccessTools.Method(typeof(GameLocation),"onMonsterKilled");
        new Harmony("BetterBeads.ShapeFixture").Patch(nativeKill,prefix:new(typeof(ShapeRuntimeChecks),nameof(KillFixture)));
        new Harmony("BetterBeads.ShapeFixture").Patch(AccessTools.Method(typeof(Monster),"deathAnimation"),prefix:new(typeof(ShapeRuntimeChecks),nameof(DeathFixture)));
        var a=new TargetMonster{Health=100};var b=new TargetMonster{Health=100};
        var farmer=(Farmer)FormatterServices.GetUninitializedObject(typeof(Farmer));
        var room=(GameLocation)FormatterServices.GetUninitializedObject(typeof(GameLocation));
        tick.Invoke(null,new object[]{room,a,farmer,17});
        Check(a.Health==83&&b.Health==100,"overlapping monster stays unharmed");
        Check(a.Deaths==0&&kills==0,"nonfatal wound cannot credit a kill");
        tick.Invoke(null,new object[]{room,a,farmer,100});
        Check(a.Health==0&&a.Deaths==1&&kills==1&&ReferenceEquals(killed,a)&&ReferenceEquals(credited,farmer),"fatal installment uses native exact-target kill credit once");
        tick.Invoke(null,new object[]{room,a,farmer,100});
        Check(a.Deaths==1&&kills==1,"late/dead-target installment cannot award twice");
        Console.WriteLine($"PASS {passed} shape runtime lifecycle checks; native kill entry is stubbed, no game is launched.");
    }
}
