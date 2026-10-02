using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using BetterBeads.Data;
using HarmonyLib;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.GameData.Weapons;
using StardewValley.Menus;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Tools;
using Microsoft.Xna.Framework.Graphics;

internal static class ForgeRuntimeChecks
{
    private static bool SinglePlayerFixture(ref bool __result){__result=false;return false;}
    private static Texture2D textureFixture=null!;
    private static bool TextureFixture(ParsedItemData __instance)
    {
        if(!__instance.QualifiedItemId.StartsWith("(W)xinzh.BetterBeads.",StringComparison.Ordinal))return true;
        AccessTools.Field(typeof(ParsedItemData),"Texture").SetValue(__instance,textureFixture);
        AccessTools.Field(typeof(ParsedItemData),"DefaultSourceRect").SetValue(__instance,__instance.ItemType.GetSourceRect(__instance,textureFixture,__instance.SpriteIndex));
        return false;
    }
    internal static void Inspect()
    {
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        foreach(var m in typeof(ItemRegistry).GetMethods(flags).Where(m=>m.DeclaringType==typeof(ItemRegistry)))Console.WriteLine(m);
        foreach(var f in typeof(ItemRegistry).GetFields(flags))Console.WriteLine(f);
        foreach(var t in typeof(Item).Assembly.GetTypes().Where(t=>t.Namespace=="StardewValley.ItemTypeDefinitions"))Console.WriteLine(t);
        Directory.CreateDirectory(".tools/forge-reference");
        foreach(var m in typeof(FarmerRenderer).GetMethods(flags).Where(m=>m.Name=="draw"))
        {
            Console.WriteLine(m+" "+string.Join(",",m.GetParameters().Select(p=>p.Name)));
            File.WriteAllLines($".tools/forge-reference/FarmerRenderer.draw-{m.GetParameters().Length}.txt",PatchProcessor.GetOriginalInstructions(m).Select(c=>c.ToString()));
        }
        File.WriteAllLines(".tools/forge-reference/ForgeMenu.description.txt",PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ForgeMenu),"_UpdateDescriptionText")).Select(c=>c.ToString()));
        File.WriteAllLines(".tools/forge-reference/ParsedItemData.rect.txt",PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ParsedItemData),"GetSourceRect")).Select(c=>c.ToString()));
    }
    internal static void Run()
    {
        int passed=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
        var assembly=typeof(Blueprint).Assembly;
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        var appearances=assembly.GetType("BetterBeads.Runtime.WeaponAppearancePatches",true)!;
        object? Call(string name,params object?[] args)=>appearances.GetMethod(name,flags)!.Invoke(null,args);
        const string key="xinzh.BetterBeads/weaponAppearance",snapshotKey="xinzh.BetterBeads/snapshot";
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        Game1.weaponData=new Dictionary<string,WeaponData>();
        foreach(var (id,type) in new[]{("4",3),("62",3),("21",1),("29",2),("47",0)})
            Game1.weaponData[id]=new WeaponData{Name="Test "+id,DisplayName="Test "+id,Type=type,MinDamage=70,MaxDamage=90,Texture="TileSheets/weapons",SpriteIndex=int.Parse(id)};
        foreach(var template in WeaponTemplates.All)
            Game1.weaponData[template.Id]=new WeaponData{Name=template.Id,DisplayName=template.Id,Type=template.NativeType,Texture=template.TextureAsset,SpriteIndex=0};
        Game1.objectData=new Dictionary<string,StardewValley.GameData.Objects.ObjectData>();
        ItemRegistry.AddTypeDefinition(new WeaponDataDefinition());
        ItemRegistry.AddTypeDefinition(new ObjectDataDefinition());
        var products=Activator.CreateInstance(assembly.GetType("BetterBeads.Runtime.ProductItems",true)!,true)!;
        var patches=assembly.GetType("BetterBeads.Runtime.ProductPatches",true)!;
        patches.GetMethod("Apply",flags)!.Invoke(null,new object?[]{"BetterBeads.ForgeRuntimeChecks",products,null,null});
        // Only the SMAPI session-state query is stubbed; no GameRunner/Game1 instance is started.
        new Harmony("BetterBeads.ForgeFixture").Patch(AccessTools.PropertyGetter(Assembly.Load("StardewModdingAPI").GetType("StardewModdingAPI.Context")!,"IsMultiplayer"),
            prefix:new HarmonyMethod(typeof(ForgeRuntimeChecks),nameof(SinglePlayerFixture)));
        // Dimensions-only texture fixture: no graphics device, pixel upload or game content loader.
        textureFixture=(Texture2D)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Texture2D));
        foreach(var f in typeof(Texture2D).GetFields(flags).Where(f=>f.FieldType==typeof(int)&&f.Name.TrimStart('_') is "width" or "height"))f.SetValue(textureFixture,16);
        Check(textureFixture.Width==16&&textureFixture.Height==16,"dimensions-only sprite fixture");
        new Harmony("BetterBeads.ForgeFixture").Patch(AccessTools.Method(typeof(ParsedItemData),"LoadTextureIfNeeded"),prefix:new HarmonyMethod(typeof(ForgeRuntimeChecks),nameof(TextureFixture)));
        ProductSnapshot Snapshot(string id,uint color)
        {
            var design=SimpleCrafting.Blank(id);design.Name="test pattern";
            design.Views["front"].Cells[0]=new BeadCell{ColorId="test",Rgba=color,MaterialId="copper"};
            return new ProductSnapshot{Design=design,WeaponRulesVersion="test",FinalStats=new()
            {{"minDamage",20},{"maxDamage",28},{"speed",0},{"knockback",1},{"critChance",.02},{"critMultiplier",3}}};
        }
        MeleeWeapon Bead(string id,uint color)
        {
            var snapshot=Snapshot(id,color);
            return (MeleeWeapon)products.GetType().GetMethod("Create",flags)!.Invoke(products,new object[]{snapshot})!;
        }
        string State(MeleeWeapon weapon)=>JsonSerializer.Serialize(new{weapon.ItemId,Min=weapon.minDamage.Value,Max=weapon.maxDamage.Value,
            Speed=weapon.speed.Value,Knockback=weapon.knockback.Value,Crit=weapon.critChance.Value,Multiplier=weapon.critMultiplier.Value,
            Enchantments=weapon.enchantments.Select(e=>e.GetType().FullName).ToArray(),Snapshot=weapon.modData.TryGetValue(snapshotKey,out var raw)?raw:null});
        foreach(var (id,native,type) in new[]{(SimpleCrafting.Sword,"4",3),(SimpleCrafting.Dagger,"21",1),(SimpleCrafting.Hammer,"29",2)})
        {
            var left=new MeleeWeapon(native);left.enchantments.Add(new VampiricEnchantment());
            var donor=Bead(id,0xCC88FFFF);string stats=State(left),donorStats=State(donor);
            Check(left.CanForge(donor),"native accepts bead donor "+id);
            var preview=(MeleeWeapon)left.getOne();
            Check(preview.Forge(donor,false)&&!left.modData.ContainsKey(key)&&left.appearance.Value is null,"preview isolated "+id);
            Check(left.Forge(donor)&&State(left)==stats&&State(donor)==donorStats,"native stats/enchantments and donor preserved "+id);
            Check(left.appearance.Value==donor.QualifiedItemId,"native draw ID points at zero-index bead tile "+id);
            var drawData=ItemRegistry.GetData(left.GetDrawnItemId());
            Check(drawData.GetSourceRect().X==0,"bead source rectangle matches custom texture "+id);
            var visual=Call("ReadVisual",left,left.GetDrawnItemId());
            Check(visual is not null&&ReferenceEquals(visual,Call("ReadVisual",left,left.GetDrawnItemId())),"render cache reused "+id);
            bool reused=true;for(int i=0;i<600;i++)reused&=ReferenceEquals(visual,Call("ReadVisual",left,left.GetDrawnItemId()));
            Check(reused,"600 stable cached frames "+id);
            var copy=(MeleeWeapon)left.getOne();
            Check(copy.modData[key]==left.modData[key]&&Call("ReadVisual",copy,copy.GetDrawnItemId()) is not null,"native copy retains independent appearance "+id);
            var loaded=new MeleeWeapon(native);loaded.modData[key]=left.modData[key];loaded.appearance.Value=left.appearance.Value;
            Check(Call("ReadVisual",loaded,loaded.GetDrawnItemId()) is not null,"serialized modData reload "+id);
            donor.modData[snapshotKey]=DesignStorage.Serialize(Snapshot(id,0xAABBCCFF));
            Check(ReferenceEquals(visual,Call("ReadVisual",left,left.GetDrawnItemId())),"editing donor cannot recolor result "+id);
            var beadLeft=Bead(id,0xFFBB88FF);string beadStats=State(beadLeft);
            Check(beadLeft.Forge(left)&&State(beadLeft)==beadStats,"bead copies donor current bead appearance "+id);
            Check(Call("ReadVisual",beadLeft,beadLeft.GetDrawnItemId()) is not null,"bead-to-bead preview "+id);
            var nativeDonor=new MeleeWeapon(native);
            Check(beadLeft.Forge(nativeDonor)&&State(beadLeft)==beadStats,"bead accepts native appearance without stat changes "+id);
            Check(Call("ReadVisual",beadLeft,beadLeft.GetDrawnItemId()) is null&&beadLeft.GetDrawnItemId()==nativeDonor.QualifiedItemId,"native texture not replaced "+id);
            Call("ResetAppearance",beadLeft);
            Check(!beadLeft.modData.ContainsKey(key)&&beadLeft.GetDrawnItemId()==beadLeft.QualifiedItemId&&State(beadLeft)==beadStats,"unforge restores original bead design "+id);
            Call("ResetAppearance",left);
            Check(!left.modData.ContainsKey(key)&&left.GetDrawnItemId()==left.QualifiedItemId&&State(left)==stats,"unforge restores original native appearance "+id);
        }
        var sword=Bead(SimpleCrafting.Sword,0xABCDEFff);
        var shapeSnapshot=Snapshot(SimpleCrafting.Sword,0xABCDEFff);
        shapeSnapshot.WeaponRulesVersion=WeaponGeometry.Version;shapeSnapshot.FinalStats["reachScale"]=1.6;
        shapeSnapshot.FinalStats["shapeSerration"]=.75;shapeSnapshot.FinalStats["speed"]=-10;
        shapeSnapshot.FinalStats["shapeSwingTimeScale"]=.95;
        shapeSnapshot.FinalStats["shapeWave"]=.8;shapeSnapshot.FinalStats["shapeBleeding"]=.75;
        var shaped=(MeleeWeapon)products.GetType().GetMethod("Create",flags)!.Invoke(products,new object[]{shapeSnapshot})!;
        string shapedState=State(shaped);
        Check(shaped.Forge(new MeleeWeapon("4"))&&State(shaped)==shapedState,"appearance cannot replace left-hand frozen shape effects, speed or reach");
        var timing=assembly.GetType("BetterBeads.Runtime.WeaponPatches",true)!.GetMethod("ApplySwingTime",flags)!;
        var timingIl=assembly.GetType("BetterBeads.Runtime.WeaponPatches",true)!.GetMethod("SwingTiming",flags)!;
        var instructions=(IEnumerable<CodeInstruction>)timingIl.Invoke(null,new object[]{PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(MeleeWeapon),"setFarmerAnimating"))})!;
        Check(instructions.Count(c=>c.operand is MethodInfo m&&m.Name=="ApplySwingTime")==1,"native swing timing is adapted exactly once");
        var swipeDuration=AccessTools.FieldRefAccess<MeleeWeapon,float>("swipeSpeed");
        swipeDuration(shaped)=100;timing.Invoke(null,new object[]{shaped});
        Check(Math.Abs(swipeDuration(shaped)-95)<.001,"frozen five-percent swing timing survives transmog");
        swipeDuration(shaped)=-20;timing.Invoke(null,new object[]{shaped});
        Check(swipeDuration(shaped)==40,"movement and speed buffs cannot produce non-positive custom timing");
        var plain=new MeleeWeapon("4");swipeDuration(plain)=100;timing.Invoke(null,new object[]{plain});
        Check(swipeDuration(plain)==100,"vanilla weapon timing remains unchanged");
        var legacy=Snapshot(SimpleCrafting.Sword,0xABCDEFff);legacy.WeaponRulesVersion="simple-4";legacy.FinalStats["reachScale"]=1.75;
        legacy.FinalStats["speed"]=-16;legacy.FinalStats["minDamage"]=191;legacy.FinalStats["maxDamage"]=250;
        var legacyItem=(MeleeWeapon)products.GetType().GetMethod("Create",flags)!.Invoke(products,new object[]{legacy})!;
        Check(legacyItem.minDamage.Value==191&&legacyItem.maxDamage.Value==250&&legacyItem.speed.Value==-16,"old heavy weapon stats are not recalculated");
        var range=assembly.GetType("BetterBeads.Runtime.WeaponPatches",true)!.GetMethod("ScaleArea",flags)!;
        object[] rangeArgs={legacyItem,1,new Microsoft.Xna.Framework.Rectangle(90,90,20,20),new Microsoft.Xna.Framework.Rectangle(80,80,40,40)};
        range.Invoke(null,rangeArgs);
        var oldArea=(Microsoft.Xna.Framework.Rectangle)rangeArgs[3];
        Check(oldArea.Width>40&&oldArea.Height==40,"simple-4 frozen forward reach still works after version bump");
        var previous=legacy.Copy();previous.WeaponRulesVersion="simple-5";
        previous.FinalStats["speed"]=0;previous.FinalStats["minDamage"]=59;previous.FinalStats["maxDamage"]=77;
        previous.FinalStats["shapeSerration"]=.5;
        var previousItem=(MeleeWeapon)products.GetType().GetMethod("Create",flags)!.Invoke(products,new object[]{previous})!;
        Check(previousItem.speed.Value==0&&previousItem.minDamage.Value==59&&previousItem.maxDamage.Value==77,
            "simple-5 frozen damage and speed do not adopt new calibration");
        swipeDuration(previousItem)=100;timing.Invoke(null,new object[]{previousItem});
        Check(swipeDuration(previousItem)==100,"simple-5 swing timing remains unchanged");
        object[] previousArea={previousItem,1,new Microsoft.Xna.Framework.Rectangle(90,90,20,20),new Microsoft.Xna.Framework.Rectangle(80,80,40,40)};
        range.Invoke(null,previousArea);
        Check(((Microsoft.Xna.Framework.Rectangle)previousArea[3]).Width>40&&WeaponGeometry.HasShapeEffects(previous.WeaponRulesVersion),
            "simple-5 frozen reach and serration remain enabled");
        var calibrated=previous.Copy();calibrated.WeaponRulesVersion="simple-6";calibrated.FinalStats["shapeSwingTimeScale"]=.95;
        var calibratedItem=(MeleeWeapon)products.GetType().GetMethod("Create",flags)!.Invoke(products,new object[]{calibrated})!;
        swipeDuration(calibratedItem)=100;timing.Invoke(null,new object[]{calibratedItem});
        Check(Math.Abs(swipeDuration(calibratedItem)-95)<.001,"simple-6 frozen faster timing remains after wave rule upgrade");
        object[] calibratedArea={calibratedItem,1,new Microsoft.Xna.Framework.Rectangle(90,90,20,20),new Microsoft.Xna.Framework.Rectangle(80,80,40,40)};
        range.Invoke(null,calibratedArea);
        Check(((Microsoft.Xna.Framework.Rectangle)calibratedArea[3]).Width>40&&WeaponGeometry.BleedingStrength(calibrated)==.5,
            "simple-6 frozen range and serration survive wave rule upgrade");
        var dagger=new MeleeWeapon("21");var scythe=new MeleeWeapon("47");
        Check(!sword.CanForge(dagger)&&!sword.Forge(dagger)&&sword.appearance.Value is null,"wrong type rejected without mutation");
        Check(!sword.CanForge(sword),"same object rejected");
        Check(!scythe.CanForge(sword),"scythe rejected");
        var invalid=Bead(SimpleCrafting.Sword,0xABCDEFff);invalid.modData[snapshotKey]="broken";
        Check(!new MeleeWeapon("4").CanForge(invalid),"corrupt bead snapshot rejected");
        sword.modData[key]="broken";
        Check(!new MeleeWeapon("4").CanForge(sword),"corrupt appearance donor rejected");
        var clean=new MeleeWeapon("4");var nativeRight=new MeleeWeapon("62");
        Check(clean.Forge(nativeRight)&&!clean.modData.ContainsKey(key)&&clean.appearance.Value=="(W)62","native-native unchanged");
        var appearance=Bead(SimpleCrafting.Sword,0xABCDEFff);var changing=new MeleeWeapon("4");changing.Forge(appearance);
        changing.appearance.Value=null;
        Check(Call("ReadVisual",changing,appearance.QualifiedItemId) is null,"native reset invalidates visual");
        var ordinary=new MeleeWeapon("62");
        Check(!(bool)Call("Handles",ordinary,changing)!&&ordinary.CanForge(changing),"stale record leaves native pair under native rules");
        var beadRecipient=Bead(SimpleCrafting.Sword,0x123456FF);
        Check(beadRecipient.Forge(changing)&&beadRecipient.GetDrawnItemId()=="(W)4"
            &&Call("ReadVisual",beadRecipient,"(W)4") is null,"reset donor provides current native appearance");
        changing.appearance.Value="(W)62";
        Check(beadRecipient.Forge(changing)&&beadRecipient.GetDrawnItemId()=="(W)62"
            &&Call("ReadVisual",beadRecipient,"(W)62") is null,"changed native donor never restores superseded bead pattern");
        Check(changing.Forge(new MeleeWeapon("4"))&&!changing.modData.ContainsKey(key),"successful native forge cleans stale record");
        var resetBead=Bead(SimpleCrafting.Sword,0x123456FF);
        resetBead.Forge(appearance);resetBead.appearance.Value=null;
        var fresh=new MeleeWeapon("4");
        Check(fresh.Forge(resetBead),"reset bead donor accepted");
        using(var restored=JsonDocument.Parse(fresh.modData[key]))
            Check(restored.RootElement.GetProperty("Pattern").GetProperty("Views").GetProperty("front")
                .GetProperty("Cells")[0].GetProperty("Rgba").GetUInt32()==0x123456FF,"reset bead donor uses original snapshot");
        object? Product(string name,Item item)=>products.GetType().GetMethod(name,flags)!.Invoke(products,new object[]{item});
        var morph=Bead(SimpleCrafting.Sword,0xABCDEFff);
        var initial=Product("ReadVisual",morph);
        Check(initial is not null&&ReferenceEquals(initial,Product("ReadVisual",morph)),"product visual cache reused before identity change");
        morph.ItemId=SimpleCrafting.Dagger;
        Check(Product("Read",morph) is null&&Product("ReadVisual",morph) is null,"item identity change invalidates unchanged snapshot cache");
        morph.ItemId=SimpleCrafting.Sword;
        Check(Product("Read",morph) is not null&&Product("ReadVisual",morph) is not null,"restored matching identity revalidates snapshot");
        morph.modData[snapshotKey]=DesignStorage.Serialize(Snapshot(SimpleCrafting.Sword,0x123456FF));
        Check(!ReferenceEquals(initial,Product("ReadVisual",morph)),"snapshot edit rebuilds cached visual");
        // Verify narrow patching of the actual native transaction and drawing code without starting Game1.
        var update=AccessTools.Method(typeof(ForgeMenu),nameof(ForgeMenu.update));
        var original=PatchProcessor.GetOriginalInstructions(update);
        var generator=new DynamicMethod("ForgeGuardCheck",typeof(void),Type.EmptyTypes).GetILGenerator();
        var rewritten=((IEnumerable<CodeInstruction>)Call("Unforge",original,generator)!).ToList();
        Check(rewritten.Count(c=>c.Calls(AccessTools.Method(appearances,"ResetAppearance")))==1,"single reset hook in completed unforge branch");
        Check(rewritten.Count(c=>c.operand is MethodInfo m&&m.Name=="ReduceId")==original.Count(c=>c.operand is MethodInfo m&&m.Name=="ReduceId"),"native shard deduction untouched");
        Check(rewritten.Count(c=>c.operand is MethodInfo m&&m.Name=="SpendRightItem")==1,"single native donor consumption retained");
        Check(rewritten.FindIndex(c=>c.Calls(AccessTools.Method(appearances,"BeforePayment")))<rewritten.FindIndex(c=>c.operand is MethodInfo m&&m.Name=="ReduceId"),"late validation precedes native payment");
        var forgeMenu=(ForgeMenu)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ForgeMenu));
        Check(forgeMenu.GetForgeCost(clean,appearance)==10,"native appearance cost remains ten shards");
        Console.WriteLine($"PASS {passed} forge integration checks, including 600 cached frames per weapon type. Session state and texture dimensions use offline fixtures. No game, save or GPU started.");
    }
}
