using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using BetterBeads.Data;
using BetterBeads.Runtime;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;

internal static class Rc6ReliabilityChecks
{
    private static Farmer farmer=null!;
    private static readonly List<Farmer> farmers=new();
    private static int farmerQueries;
    private static GameLocation room=null!;
    private static bool True(ref bool __result){__result=true;return false;}
    private static bool False(ref bool __result){__result=false;return false;}
    private static bool Farmers(ref StardewValley.Network.FarmerCollection __result){farmerQueries++;__result=new(null);return false;}
    private static bool GetFarmer(ref Farmer __result){__result=farmer;return false;}
    private static bool Location(Character __instance,ref GameLocation __result){if(!ReferenceEquals(__instance,farmer))return true;__result=farmers.Count==0?null!:room;return false;}
    private sealed class TestMonster:Monster {}
    internal static void Run()
    {
        int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
        var progress=new SaveProgress();var original=SimpleCrafting.Blank(SimpleCrafting.Picture16);original.Name="Original";
        original.Views["front"].Cells[17]=new(){Rgba=0x112233FF,ColorId="old",MaterialId="decoration"};
        new BlueprintRepository(progress).Save(original);string oldRecord=progress.BlueprintRecords[original.Id];
        var later=original.Copy();later.Revision++;later.Views["front"].Cells[18]=new(){Rgba=0xAABBCCFF,ColorId="new",MaterialId="decoration"};
        var recovery=new DraftRecoveryEntry{Draft=later.Copy(),Baseline=later.Copy()};
        recovery.Draft.Views["front"].Cells[19]=new(){Rgba=0xFFCCAAFF,ColorId="draft",MaterialId="decoration"};
        var doc=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),SimpleCrafting.Catalog(),"front");
        Check(doc.RestoreRecoveryCopy(recovery),"rolled-back game can restore the newer cached draft as an independent copy");
        var copy=doc.Snapshot();
        Check(copy.Id!=original.Id&&copy.Revision==0&&copy.Name==""&&doc.IsDirty,"recovery copy has fresh identity and requires explicit naming/save");
        Check(BlueprintManualSave.Prepare(progress,copy).Kind==BlueprintSaveKind.NeedsName,"recovered copy avoids stale revision conflict");
        copy.Name="Recovered";var save=BlueprintManualSave.Prepare(progress,copy);save.Change!.Apply(progress);doc.AdoptSaved(save.Change.Saved);
        Check(progress.BlueprintRecords[original.Id]==oldRecord&&progress.BlueprintRecords.Count==2&&!doc.IsDirty,"recovery saves successfully without modifying the original record");
        var equivalent=BlueprintManualSave.NewCopy(original);
        Check(BlueprintManualSave.Prepare(progress,equivalent).Kind==BlueprintSaveKind.Reused,"save-as still reuses identical player artwork rather than duplicating it");
        var changed=BlueprintManualSave.NewCopy(original);changed.Views["front"].Cells[17]!.Rgba=0x998877FF;
        Check(changed.Id!=original.Id&&changed.Revision==0&&original.Views["front"].Cells[17]!.Rgba==0x112233FF,"save-as candidate never mutates current artwork before confirmation");
        foreach(var (oldId,newId) in new[]{(ProductTemplates.Picture,SimpleCrafting.Picture16),(ProductTemplates.DetailedPicture,SimpleCrafting.Picture32)})
        {
            var old=SimpleCrafting.Blank(newId);old.TemplateId=oldId;old.Views["front"].Cells[3]=new(){Rgba=0xABCDEFff,ColorId="alias",MaterialId="decoration"};
            string code=BlueprintSharing.Encode(old);
            Check(BlueprintSharing.TryDecode(code,out var decoded)&&decoded!.TemplateId==newId&&decoded.Id!=old.Id,$"legacy {oldId} share code migrates into a new compatible draft");
            Check(BlueprintSharing.TryDecode(BlueprintSharing.Encode(decoded!),out var again)&&again!.Views["front"].Cells.Select(c=>c?.Rgba).SequenceEqual(old.Views["front"].Cells.Select(c=>c?.Rgba)),"old picture dimensions/colors survive repeated sharing");
        }
        var session=new SaveSession(null!,null!);
        Check(!session.CanWriteGallery,"unreadable main progress prevents gallery writes");
        AccessTools.Field(typeof(SaveSession),"<Progress>k__BackingField").SetValue(session,new SaveProgress());
        Check(session.CanWriteGallery,"loaded main and gallery progress allows gallery writes");
        AccessTools.Field(typeof(SaveSession),"<OnlineReadable>k__BackingField").SetValue(session,false);
        Check(!session.CanWriteGallery,"unreadable shared gallery disables gifts even for the host");
        var h=new Harmony("checks.rc6.reliability");
        try
        {
            h.Patch(AccessTools.PropertyGetter(typeof(Context),"IsMainPlayer"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(True)));
            h.Patch(AccessTools.PropertyGetter(typeof(PlayMode),"Creative"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(False)));
            var editor=(EditorPanel)FormatterServices.GetUninitializedObject(typeof(EditorPanel));
            AccessTools.Field(typeof(EditorPanel),"document").SetValue(editor,new EditorDocument(original,SimpleCrafting.Catalog(),"front"));
            AccessTools.Field(typeof(EditorPanel),"costVersion").SetValue(editor,-1L);
            AccessTools.Field(typeof(EditorPanel),"cachedOwned").SetValue(editor,1);
            string Status(Blueprint design,ManufacturingFailure failure)=>(string)AccessTools.Method(typeof(EditorPanel),"StatusText").Invoke(editor,new object[]{design,new ManufacturingAvailability(failure,null)})!;
            Check(Status(original,ManufacturingFailure.NoSpace).Contains("背包"),"full backpack reason is displayed directly in the status bar without hovering");
            AccessTools.Field(typeof(EditorPanel),"cachedOwned").SetValue(editor,0);
            Check(Status(original,ManufacturingFailure.InvalidDesign).Contains("还缺"),"missing material amount is visible to controller and touch players");
            Check(Status(SimpleCrafting.Blank(SimpleCrafting.Picture16),ManufacturingFailure.InvalidDesign).Contains("摆豆"),"empty canvas instruction is visible before focusing any control");
            var online=(OnlineSession)FormatterServices.GetUninitializedObject(typeof(OnlineSession));AccessTools.Field(typeof(OnlineSession),"session").SetValue(online,session);
            Check(!online.CanRecordGift,"native gift guard does not bypass failed gallery load in single player/host mode");
            h.Patch(AccessTools.PropertyGetter(typeof(Context),"IsWorldReady"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(True)));
            h.Patch(AccessTools.Method(typeof(Game1),"getOnlineFarmers"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(Farmers)));
            h.Patch(AccessTools.Method(typeof(Game1),"GetPlayer"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(GetFarmer)));
            h.Patch(AccessTools.PropertyGetter(typeof(Game1),"player"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(GetFarmer)));
            h.Patch(AccessTools.Method(typeof(Game1),"shouldTimePass"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(True)));
            farmer=(Farmer)FormatterServices.GetUninitializedObject(typeof(Farmer));
            var id=AccessTools.Field(typeof(Farmer),"uniqueMultiplayerID");id.SetValue(farmer,Activator.CreateInstance(id.FieldType));id.FieldType.GetProperty("Value")!.SetValue(id.GetValue(farmer),99L);
            room=new GameLocation();h.Patch(AccessTools.PropertyGetter(typeof(Character),"currentLocation"),prefix:new(typeof(Rc6ReliabilityChecks),nameof(Location)));
            farmers.Clear();farmers.Add(farmer);
            Game1.otherFarmers=new();
            var monster=new TestMonster{Health=100};room.characters.Add(monster);
            ShapeCombat.Clear();AccessTools.Field(typeof(ShapeCombat),"tagTicks").SetValue(null,1);
            var effect=new ProductSnapshot{WeaponRulesVersion=WeaponGeometry.Version,FinalStats=new(){{"shapeBleeding",1}}};
            void Add()=>AccessTools.Method(typeof(ShapeCombat),"Add").Invoke(null,new object[]{room,monster,farmer,effect,2d});
            var update=(Action)AccessTools.Method(typeof(ShapeCombat),"Update").CreateDelegate(typeof(Action));
            void Update()=>update();
            int Active()=>((IList)AccessTools.Field(typeof(ShapeCombat),"activeTargets").GetValue(null)!).Count;
            int Credits()=>((IDictionary)AccessTools.Field(typeof(ShapeCombat),"targets").GetValue(null)!).Count;
            Game1.currentGameTime=new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(.25));Add();
            for(int i=0;i<12;i++)Update();
            Check(Active()==0&&Credits()==1&&monster.Health==100,"finished fractional wound leaves the active loop while preserving credit");
            farmerQueries=0;long allocation=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<600;i++)Update();allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;
            Check(Active()==0&&farmerQueries==10,"600 idle frames only sweep retained credit once per second");
            Console.WriteLine($"INFO 600 inactive frames allocated {allocation} bytes (periodic native-location sweeps included)");
            Add();for(int i=0;i<12;i++)Update();
            Check(Active()==0&&monster.Health==99,"reactivating an idle target uses preserved fractional damage exactly once");
            farmers.Clear();AccessTools.Field(typeof(ShapeCombat),"tagTicks").SetValue(null,0);Update();
            Check(Credits()==0,"leaving a location discards its retained fractional credit");ShapeCombat.Clear();
        }
        finally{h.UnpatchAll(h.Id);farmers.Clear();}
        var file=Path.GetFullPath(".tools/rc6-import/alpha.png");
        var decodedImage=Task.Run(()=>ImageCpuDecoder.Load(file,CancellationToken.None)).GetAwaiter().GetResult();
        Check(decodedImage.Width==2&&decodedImage.Height==2&&decodedImage.Pixels[0]==0x112233FF&&decodedImage.Pixels[1]==0xABCDEF80,"background CPU decode preserves exact opaque and half-alpha RGB pixels");
        var prepared=ImageBlueprintImport.Prepare(decodedImage.Pixels,2,2,false,24);
        Check(ReferenceEquals(prepared.Pixels,decodedImage.Pixels)&&prepared.HasHalfAlpha,"normal preparation reuses the single decoder buffer");
        var result=ImageBlueprintImport.Create(prepared,new(SimpleCrafting.Picture16));
        Check(result.Design.Views["front"].Cells[7*16+7]!.Rgba==0x112233FF&&result.HasHalfAlpha,"conversion keeps original resolution and makes half-alpha beads solid only at commit");
        var large=Task.Run(()=>{long before=GC.GetAllocatedBytesForCurrentThread();var image=ImageCpuDecoder.Load(".tools/rc6-import/large.png",CancellationToken.None);return(image,Allocated:GC.GetAllocatedBytesForCurrentThread()-before);}).GetAwaiter().GetResult();
        Check(large.image.Pixels.Count==1024*768&&large.image.PreviewWidth==512&&large.image.PreviewHeight==384,"full-resolution conversion buffer uses a bounded proportional preview");
        Check(large.Allocated<1024L*768*8+2_000_000,"CPU decode avoids two additional full-size managed pixel copies");
        Console.WriteLine($"INFO 1024x768 decode allocated {large.Allocated} managed bytes");
        var jpeg=ImageCpuDecoder.Load(".tools/rc6-import/sample.jpg",CancellationToken.None);
        Check(jpeg.Width==8&&jpeg.Height==6&&jpeg.Pixels.All(c=>(c&255)==255),"JPEG decode supports opaque original RGB on the worker path");
        foreach(string bad in new[]{"broken.png","oversize.png"})
        {bool rejected=false;try{ImageCpuDecoder.Load(".tools/rc6-import/"+bad,CancellationToken.None);}catch(Exception){rejected=true;}Check(rejected,"CPU decode rejects "+bad+" before replacing panel state");}
        using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;
        try{ImageCpuDecoder.Load(file,cancel.Token);}catch(OperationCanceledException){cancelled=true;}
        Check(cancelled,"cancelled page prevents a late CPU decode result from becoming a new preview");
        Console.WriteLine($"PASS {passed} RC6 targeted reliability/performance checks; no game launched.");
    }
}
