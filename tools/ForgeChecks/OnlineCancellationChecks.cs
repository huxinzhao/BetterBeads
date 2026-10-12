using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using BetterBeads.Data;
using HarmonyLib;
using StardewValley;
using StardewValley.Network;
using StardewValley.Objects;

internal static class OnlineCancellationChecks
{
    private static Farmer player=null!;
    private static int releases,replies;
    private static bool Player(ref Farmer __result){__result=player;return false;}
    private static bool Client(ref bool __result){__result=false;return false;}
    private static bool Valid(ref bool __result){__result=true;return false;}
    private static bool DeferUpdate()=>false;
    private static bool ChestMutex(Chest __instance,ref NetMutex __result)
    {
        // The fixture is a regular chest without a game registry or global-inventory metadata.
        __result=(NetMutex)AccessTools.Field(typeof(Chest),"mutex").GetValue(__instance)!;
        return false;
    }
    private static bool Reply(){replies++;return false;}
    private static bool Flush()=>false;
    private static void Released()=>releases++;

    internal static void Run()
    {
        int passed=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
        var assembly=typeof(Blueprint).Assembly;
        var onlineType=assembly.GetType("BetterBeads.Runtime.OnlineSession",true)!;
        var jobType=onlineType.GetNestedType("CraftWork",BindingFlags.NonPublic)!;
        var leaseType=onlineType.GetNestedType("Lease",BindingFlags.NonPublic)!;
        var resultType=assembly.GetType("BetterBeads.Runtime.ManufacturingResult",true)!;
        var harmony=new Harmony("BetterBeads.OnlineCancellationFixture");
        void Patch(MethodInfo method,string handler)=>harmony.Patch(method,prefix:new HarmonyMethod(typeof(OnlineCancellationChecks),handler));
        player=(Farmer)FormatterServices.GetUninitializedObject(typeof(Farmer));
        var playerId=AccessTools.Field(typeof(Farmer),"uniqueMultiplayerID");
        playerId.SetValue(player,Activator.CreateInstance(playerId.FieldType));
        playerId.FieldType.GetProperty("Value")!.SetValue(playerId.GetValue(player),101L);
        Patch(AccessTools.PropertyGetter(typeof(Game1),"player"),nameof(Player));
        // Keep the actual NetMutex.RequestLock pending; tests deliver its real stored callbacks later.
        Patch(AccessTools.PropertyGetter(typeof(Game1),"IsMasterGame"),nameof(Client));
        Patch(AccessTools.Method(typeof(Chest),"GetMutex"),nameof(ChestMutex));
        Patch(AccessTools.Method(typeof(NetMutex),"Update",new[]{typeof(GameLocation)}),nameof(DeferUpdate));
        harmony.Patch(AccessTools.Method(typeof(NetMutex),"ReleaseLock"),prefix:new HarmonyMethod(typeof(OnlineCancellationChecks),nameof(Released)));
        Patch(AccessTools.Method(onlineType,"Valid"),nameof(Valid));
        Patch(AccessTools.Method(onlineType,"Reply"),nameof(Reply));
        Patch(AccessTools.Method(onlineType,"FlushInventories"),nameof(Flush));
        try
        {
            object Get(object value,string name)=>AccessTools.Field(value.GetType(),name).GetValue(value)!;
            void Set(object value,string name,object? data)=>AccessTools.Field(value.GetType(),name).SetValue(value,data);
            object Session()
            {
                var session=FormatterServices.GetUninitializedObject(onlineType);
                foreach(var field in onlineType.GetFields(BindingFlags.Instance|BindingFlags.NonPublic))
                    if(field.FieldType.IsGenericType&&field.FieldType.GetGenericTypeDefinition() is var generic
                        &&(generic==typeof(Dictionary<,>)||generic==typeof(HashSet<>)||generic==typeof(List<>)
                            ||generic==typeof(System.Runtime.CompilerServices.ConditionalWeakTable<,>)))
                        field.SetValue(session,Activator.CreateInstance(field.FieldType));
                Set(session,"<Gallery>k__BackingField",new SaveProgress());
                return session;
            }
            object Job(object session,params Chest[] chests)
            {
                var job=Activator.CreateInstance(jobType,true)!;
                Set(job,"Lease",Activator.CreateInstance(leaseType,true));
                Set(job,"Started",Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);
                Set(job,"Remaining",new Queue<Chest>(chests));
                ((IList)Get(session,"work")).Add(job);
                return job;
            }
            Chest Chest()
            {
                var chest=(Chest)FormatterServices.GetUninitializedObject(typeof(Chest));
                AccessTools.Field(typeof(Chest),"mutex").SetValue(chest,new NetMutex());
                return chest;
            }
            void Owner(Chest chest,long id)
            {
                var owner=AccessTools.Field(typeof(NetMutex),"owner").GetValue(chest.GetMutex())!;
                owner.GetType().GetProperty("Value")!.SetValue(owner,id);
            }
            object? Reservation(object session,Chest chest)
            {
                var table=Get(session,"reservations");var args=new object?[]{chest,null};
                return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table,args)!?args[1]:null;
            }
            void Reserve(object session,Chest chest,object job)
            {
                var table=Get(session,"reservations");
                table.GetType().GetMethod("Remove")!.Invoke(table,new object[]{chest});
                table.GetType().GetMethod("Add")!.Invoke(table,new[]{(object)chest,job});
            }
            void Clear(object session)=>AccessTools.Method(onlineType,"Clear").Invoke(session,null);
            (Action Acquired,Action Failed) Begin(object session,object job,Chest chest)
            {
                AccessTools.Method(onlineType,"NextLock").Invoke(session,new[]{job});
                Check(ReferenceEquals(Reservation(session,chest),job)&&!chest.GetMutex().IsLocked(),"native pending request belongs to its craft");
                return ((Action)AccessTools.Field(typeof(NetMutex),"onLockAcquired").GetValue(chest.GetMutex())!,
                    (Action)AccessTools.Field(typeof(NetMutex),"onLockFailed").GetValue(chest.GetMutex())!);
            }
            var session=Session();var pending=Chest();var old=Job(session,pending);var callbacks=Begin(session,old,pending);
            Clear(session);Clear(session);
            Check((bool)Get(old,"Done")&&((IList)Get(session,"work")).Count==0&&ReferenceEquals(Reservation(session,pending),old),
                "repeated clear cancels work and preserves only the pending weak reservation");
            Check(releases==0&&replies==0,"clearing an unacquired request does not release or reply");
            Owner(pending,101);callbacks.Acquired();callbacks.Acquired();callbacks.Failed();
            Check(!pending.GetMutex().IsLocked()&&Reservation(session,pending) is null&&releases==1&&replies==0,
                "late acquisition releases exactly once and cannot resume or reply after clear");
            var reused=Job(session,pending);Begin(session,reused,pending);
            Check(ReferenceEquals(Reservation(session,pending),reused),"released chest is immediately available to a new craft");

            session=Session();pending=Chest();old=Job(session,pending);callbacks=Begin(session,old,pending);
            Clear(session);Owner(pending,202);int before=releases;callbacks.Failed();
            Check(Reservation(session,pending) is null&&pending.GetMutex().IsLocked()&&releases==before&&replies==0,
                "late failure forgets its reservation without releasing another player's lock or replying");

            session=Session();pending=Chest();old=Job(session,pending);callbacks=Begin(session,old,pending);
            Clear(session);var replacement=Job(session);Reserve(session,pending,replacement);Owner(pending,101);before=releases;
            callbacks.Acquired();callbacks.Failed();
            Check(ReferenceEquals(Reservation(session,pending),replacement)&&pending.GetMutex().IsLockHeld()&&releases==before&&replies==0,
                "old success and failure callbacks cannot erase a replacement reservation or release its lock");

            session=Session();var held=Chest();var lost=Chest();var heldJob=Job(session);var lostJob=Job(session);
            ((HashSet<Chest>)Get(heldJob,"Held")).Add(held);((HashSet<Chest>)Get(lostJob,"Held")).Add(lost);
            Reserve(session,held,heldJob);Reserve(session,lost,lostJob);Owner(held,101);Owner(lost,202);before=releases;
            Clear(session);Clear(session);
            Check(!held.GetMutex().IsLocked()&&lost.GetMutex().IsLocked()&&Reservation(session,held) is null&&Reservation(session,lost) is null
                &&releases==before+1&&replies==0,"clear releases owned locks once and leaves lost locks untouched");

            session=Session();pending=Chest();old=Job(session,pending);callbacks=Begin(session,old,pending);
            var failure=Activator.CreateInstance(resultType,new object?[]{false,"manufacturing.changed",null,null})!;
            AccessTools.Method(onlineType,"Finish").Invoke(session,new[]{old,failure});before=releases;
            Owner(pending,101);callbacks.Acquired();
            Check((bool)Get(old,"Done")&&Reservation(session,pending) is null&&!pending.GetMutex().IsLocked()&&releases==before+1&&replies==1,
                "normal timeout replies once and late acquisition still releases its own lock");
            Console.WriteLine($"PASS {passed} online cancellation checks. Native mutex state is exercised offline; no game is launched.");
        }
        finally{harmony.UnpatchAll(harmony.Id);}
    }
}
