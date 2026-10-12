using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace BetterBeads.Runtime;

/// <summary>Transient wounds, exact monster identity, and host-only advancement/kill credit.</summary>
internal static class ShapeCombat
{
    private const string Key="xinzh.BetterBeads/WoundTarget",Message="ShapeHit/v1";
    private sealed class Hit
    {
        public string Protocol{get;set;}="";public long Sequence{get;set;}public string Target{get;set;}="";
        public string Location{get;set;}="";public string Weapon{get;set;}="";public double Baseline{get;set;}
    }
    private sealed record Target(GameLocation Location,Dictionary<long,ShapeBleeding> Owners)
    {public List<long> ActiveOwners {get;}=new();}
    private static readonly Dictionary<Monster,Target> targets=new();
    private static readonly List<Monster> activeTargets=new(),retiredTargets=new();
    private static readonly HashSet<GameLocation> occupiedLocations=new();
    private static readonly ShapeHitReceipts received=new();
    private static IModHelper helper=null!;private static ProductItems products=null!;private static IMonitor monitor=null!;
    private static long sequence;private static bool failed;private static int tagTicks;
    private static readonly Action<GameLocation,Farmer,Monster,Rectangle,bool> Killed=
        AccessTools.Method(typeof(GameLocation),"onMonsterKilled").CreateDelegate<Action<GameLocation,Farmer,Monster,Rectangle,bool>>();
    public static void Register(IModHelper modHelper,IMonitor log,ProductItems items)
    {
        helper=modHelper;products=items;monitor=log;
        helper.Events.GameLoop.UpdateTicked+=(_,_)=>Update();
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>Clear();
        helper.Events.World.NpcListChanged+=(_,e)=>{if(Context.IsMainPlayer)foreach(var m in e.Added.OfType<Monster>())Tag(m);};
        helper.Events.Player.Warped+=(_,e)=>{if(Context.IsMainPlayer)foreach(var m in e.NewLocation.characters.OfType<Monster>())Tag(m);};
        helper.Events.Multiplayer.PeerDisconnected+=(_,e)=>received.Forget(e.Peer.PlayerID);
        helper.Events.Multiplayer.ModMessageReceived+=(_,e)=>
        {
            if(!Context.IsWorldReady||!Context.IsMainPlayer||e.FromModID!="xinzh.BetterBeads"||e.Type!=Message)return;
            var hit=e.ReadAs<Hit>();
            if(hit.Protocol!=Protocol||!double.IsFinite(hit.Baseline)||hit.Baseline<=0)return;
            var who=Game1.GetPlayer(e.FromPlayerID);if(who?.CurrentTool is not MeleeWeapon weapon||weapon.isOnSpecial
                ||products.Read(weapon) is not {} snapshot||snapshot.InstanceId!=hit.Weapon)return;
            var location=who.currentLocation;if(location?.NameOrUniqueName!=hit.Location)return;
            var matched=location.characters.OfType<Monster>().Where(m=>m.modData.TryGetValue(Key,out string id)&&id==hit.Target).Take(2).ToArray();
            if(matched.Length!=1)return;var monster=matched[0];
            if(monster.Health<=0||Vector2.DistanceSquared(who.Position,monster.Position)>1024*1024)return;
            if(!received.TryAccept(e.FromPlayerID,hit.Sequence))return;
            // Never trust a remote budget. Bound its non-critical report by the left-hand weapon.
            double limit=snapshot.FinalStats.GetValueOrDefault("maxDamage")*4+Math.Max(0,who.Attack)*3;
            Add(location,monster,who,snapshot,Math.Min(hit.Baseline,limit));
        };
    }
#if BEADS_LITE
    private static string Protocol=>OnlineRules.Protocol;
#else
    private const string Protocol="full-shape7";
#endif
    public static void Clear(){targets.Clear();activeTargets.Clear();retiredTargets.Clear();occupiedLocations.Clear();received.Clear();sequence=0;failed=false;tagTicks=0;}
    private static void Tag(Monster monster){if(!monster.modData.ContainsKey(Key))monster.modData[Key]=Guid.NewGuid().ToString("N");}
    public static void HitMonster(Monster monster,Farmer who,ProductSnapshot snapshot,double baseline)
    {
        if(helper is null||!WeaponGeometry.HasShapeEffects(snapshot.WeaponRulesVersion)||monster.Health<=0||baseline<=0)return;
        double strength=WeaponGeometry.BleedingStrength(snapshot);if(strength<=0)return;
        var location=monster.currentLocation??who.currentLocation;if(location is null)return;
        if(Context.IsMainPlayer){Add(location,monster,who,snapshot,baseline);return;}
        if(!monster.modData.TryGetValue(Key,out string id))return; // No positional/index fallback can hit another monster.
        helper.Multiplayer.SendMessage(new Hit{Protocol=Protocol,Sequence=++sequence,Target=id,Location=location.NameOrUniqueName,
            Weapon=snapshot.InstanceId,Baseline=baseline},Message,new[]{"xinzh.BetterBeads"},new[]{Game1.MasterPlayer.UniqueMultiplayerID});
    }
    private static void Add(GameLocation location,Monster monster,Farmer who,ProductSnapshot snapshot,double baseline)
    {
        if(!WeaponGeometry.HasShapeEffects(snapshot.WeaponRulesVersion))return;
        double budget=ShapeBleeding.Budget(WeaponGeometry.BleedingStrength(snapshot),baseline,monster.GetType().Name);
        if(budget<=0)return;
        if(!targets.TryGetValue(monster,out var target))targets[monster]=target=new(location,new());
        if(!target.Owners.TryGetValue(who.UniqueMultiplayerID,out var wounds))target.Owners[who.UniqueMultiplayerID]=wounds=new();
        if(!wounds.Active)
        {
            if(target.ActiveOwners.Count==0)activeTargets.Add(monster);
            target.ActiveOwners.Add(who.UniqueMultiplayerID);
        }
        wounds.Add(budget);
    }
    private static void Update()
    {
        if(!Context.IsWorldReady||!Context.IsMainPlayer)return;
        bool sweep=tagTicks++%60==0;
        if(activeTargets.Count==0&&!sweep)return;
        occupiedLocations.Clear();foreach(var farmer in Game1.getOnlineFarmers())if(farmer.currentLocation is {} l)occupiedLocations.Add(l);
        if(sweep)
        {
            foreach(var location in occupiedLocations)foreach(var npc in location.characters)if(npc is Monster monster)Tag(monster);
            retiredTargets.Clear();
            foreach(var pair in targets)if(pair.Key.Health<=0||!occupiedLocations.Contains(pair.Value.Location)||!pair.Value.Location.characters.Contains(pair.Key))retiredTargets.Add(pair.Key);
            foreach(var monster in retiredTargets)targets.Remove(monster);
            retiredTargets.Clear();
        }
        if(!Game1.shouldTimePass())return;
        double elapsed=Math.Min(.25,Math.Max(0,Game1.currentGameTime.ElapsedGameTime.TotalSeconds));
        for(int i=activeTargets.Count-1;i>=0;i--)
        {
            var monster=activeTargets[i];
            if(!targets.TryGetValue(monster,out var target)||monster.Health<=0||!occupiedLocations.Contains(target.Location)||!target.Location.characters.Contains(monster))
            {targets.Remove(monster);activeTargets.RemoveAt(i);continue;}
            for(int j=target.ActiveOwners.Count-1;j>=0;j--)
            {
                long owner=target.ActiveOwners[j];
                var who=Game1.GetPlayer(owner);if(who is null){target.Owners.Remove(owner);target.ActiveOwners.RemoveAt(j);continue;}
                var wound=target.Owners[owner];int damage=wound.Advance(elapsed);
                if(!wound.Active)target.ActiveOwners.RemoveAt(j);
                if(damage<=0)continue;
                // The installment is already post-defense. No area query, crit, extra hit proc,
                // knockback or invincibility is applied a second time.
                if(ApplyTick(target.Location,monster,who,damage))
                {
                    targets.Remove(monster);break;
                }
            }
            if(!targets.ContainsKey(monster)||target.ActiveOwners.Count==0)activeTargets.RemoveAt(i);
            // Idle fractional credit is retained but never advances in the per-frame loop.
        }
    }
    private static bool ApplyTick(GameLocation location,Monster monster,Farmer who,int damage)
    {
        if(damage<=0||monster.Health<=0)return false;
        monster.Health=Math.Max(0,monster.Health-damage);
        if(monster.Health>0)return false;
        // Keep kill/loot credit in the native lifecycle; no area hit can affect a neighbor.
        var bounds=monster.GetBoundingBox();
        try{monster.deathAnimation();}
        catch(Exception ex){Report(ex);}
        try{Killed(location,who,monster,bounds,false);}
        catch(Exception ex){Report(ex);}
        return true;
    }
    private static void Report(Exception ex)
    {if(!failed){failed=true;monitor?.Log("Shape wound kill lifecycle failed: "+ex,LogLevel.Error);}}
}
