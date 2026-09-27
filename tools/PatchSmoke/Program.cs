using System.Reflection;
using System.Runtime.Loader;

// Registers patches in an isolated process; never starts a game or loads a save.
string game=Path.GetFullPath(args[0]),mod=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(_,name)=>
{
    foreach(var directory in new[]{Path.GetDirectoryName(mod)!,game,Path.Combine(game,"smapi-internal")})
    {
        string path=Path.Combine(directory,name.Name+".dll");
        if(File.Exists(path))return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }
    return null;
};
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
if(args.Length==2)
{
    var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var mp=native.GetType("StardewValley.Multiplayer")!;
    var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    if(mp.GetMethod("farmerRoot",flags,null,new[]{typeof(long)},null) is null
        ||mp.GetMethod("broadcastFarmerDelta",flags,null,new[]{native.GetType("StardewValley.Farmer")!,typeof(byte[])},null) is null
        ||!mp.GetMethods(flags).Any(m=>m.Name=="writeObjectDeltaBytes"&&m.IsGenericMethodDefinition)
        ||native.GetType("StardewValley.Game1")!.GetField("multiplayer",flags) is null
        ||native.GetType("StardewValley.Network.NetMutex")!.GetMethod("RequestLock",new[]{typeof(Action),typeof(Action)}) is null)
        throw new Exception("Native online transaction API changed");
    Console.WriteLine("PASS native chest mutex and explicit remote-farmer delta transport signatures");
}
if(args.Contains("--network-api"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    foreach(var m in ga.GetType("StardewValley.Game1")!.GetMethods().Where(m=>m.Name=="GetPlayer"))Console.WriteLine(m+" "+string.Join(",",m.GetParameters().Select(p=>p.Name+"="+p.DefaultValue)));
    foreach(var name in new[]{"StardewValley.Network.NetMutex","StardewValley.Objects.Chest","StardewValley.Locations.FarmHouse","StardewValley.Multiplayer"})
    {
        var t=ga.GetType(name)!;Console.WriteLine(name);
        foreach(var m in t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .Where(m=>name.EndsWith("NetMutex")||m.Name.Contains("Owner")||m.Name.Contains("ItemsFor")||m.Name.Contains("Mutex")||name.EndsWith("Multiplayer")&&m.MemberType==MemberTypes.Method))Console.WriteLine(m);
    }
    return;
}
if(args.Contains("--network-il"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var ha=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"smapi-internal","0Harmony.dll"));
    var original=ha.GetType("HarmonyLib.PatchProcessor")!.GetMethods().Single(m=>m.Name=="GetOriginalInstructions"&&m.GetParameters()[1].ParameterType.IsByRef);
    foreach(var type in ga.GetTypes().Where(t=>t.Name is "NetFarmerRoot" or "GameServer" or "Multiplayer" or "NetMutex"))
    foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
        .Where(m=>m.Name is "receivePlayerDelta" or "processIncomingMessage" or "RequestLock" or "GetPlayer" or "updateLate" or "broadcastFarmerDelta" or "sendFarmerDelta" or "updateRoots" || m.Name.Contains("FarmerDeltas") || m.Name=="updateRoot"))
    {if(method.ContainsGenericParameters)continue;Console.WriteLine(type.FullName+" "+method);foreach(var line in (System.Collections.IEnumerable)original.Invoke(null,new object?[]{method,null})!)Console.WriteLine(line);}
    return;
}
if(args.Contains("--furniture-held"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var ha=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"smapi-internal","0Harmony.dll"));
    var original=ha.GetType("HarmonyLib.PatchProcessor")!.GetMethods().Single(m=>m.Name=="GetOriginalInstructions"&&m.GetParameters()[1].ParameterType.IsByRef);
    foreach(string name in new[]{"StardewValley.Objects.Furniture","StardewValley.Object","StardewValley.Farmer","StardewValley.Game1"})
    foreach(var method in ga.GetType(name,true)!.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
        .Where(m=>m.Name is "canBeHeld" or "drawWhenHeld" or "isHoldingObject" or "showHoldingItem" or "drawAtNonTileSpot" or "drawPlacementBounds" || name.EndsWith("Furniture")&&m.Name is "drawInMenu" or "draw" or "updateDrawPosition"
            ||m.Name is "IsCarrying" or "drawPlayerHeldObject"
            ||name.EndsWith("Farmer")&&(m.Name=="draw"||m.Name.Contains("Holding",StringComparison.OrdinalIgnoreCase)||m.Name.Contains("ActiveObject")||m.Name.Contains("CurrentItem"))))
    {
        Console.WriteLine(name+" "+method);
        if(!method.IsAbstract)foreach(var line in (System.Collections.IEnumerable)original.Invoke(null,new object?[]{method,null})!)Console.WriteLine(line);
    }
    return;
}
if(args.Contains("--sebastian-reference"))
{
    var mg=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"MonoGame.Framework.dll"));
    using var file=File.OpenRead(Path.Combine(game,"Content","Characters","Sebastian.xnb"));
    using var header=new BinaryReader(file);
    if(new string(header.ReadChars(3))!="XNB")throw new Exception("Not XNB");
    header.ReadByte();header.ReadByte();byte flags=header.ReadByte();int total=header.ReadInt32(),expanded=header.ReadInt32();
    if((flags&128)==0)throw new Exception("Expected LZX XNB");
    var ctor=mg.GetType("MonoGame.Framework.Utilities.LzxDecoderStream",true)!.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single();
    var values=ctor.GetParameters().Select(p=>p.ParameterType==typeof(Stream)?(object)file:
        p.Name!.Contains("decompressed",StringComparison.OrdinalIgnoreCase)?expanded:total-14).ToArray();
    using var decoded=(Stream)ctor.Invoke(values);
    using var reader=new BinaryReader(decoded,System.Text.Encoding.UTF8,leaveOpen:true);
    int count=reader.Read7BitEncodedInt();
    for(int i=0;i<count;i++){string name=reader.ReadString();reader.ReadInt32();if(!name.Contains("Texture2DReader"))throw new Exception("Unexpected reader");}
    if(reader.Read7BitEncodedInt()!=0||reader.Read7BitEncodedInt()!=1||reader.ReadInt32()!=0)throw new Exception("Unsupported texture layout");
    int width=reader.ReadInt32(),height=reader.ReadInt32();reader.ReadInt32();int length=reader.ReadInt32();
    byte[] pixels=reader.ReadBytes(length);if(length!=width*height*4||pixels.Length!=length)throw new Exception("Texture length mismatch");
    var frame=new byte[16*32*4];for(int y=0;y<32;y++)Array.Copy(pixels,y*width*4,frame,y*16*4,16*4);
    string output=Path.GetFullPath("art/vertical-sword-1.0-RC2");Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output,"sebastian.json"),System.Text.Json.JsonSerializer.Serialize(new{width=16,height=32,rgba=Convert.ToBase64String(frame)}));
    Console.WriteLine($"Extracted actual Sebastian front frame from {width}x{height}; offline preview only.");return;
}
if(args.Contains("--swing-reference"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var ha=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"smapi-internal","0Harmony.dll"));
    var original=ha.GetType("HarmonyLib.PatchProcessor")!.GetMethods().Single(m=>m.Name=="GetOriginalInstructions"&&m.GetParameters()[1].ParameterType.IsByRef);
    var type=ga.GetType("StardewValley.Tools.MeleeWeapon",true)!;
    var il=(System.Collections.IEnumerable)original.Invoke(null,new object?[]{type.TypeInitializer,null})!;
    foreach(var line in il)Console.WriteLine(line);
    var mg=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"MonoGame.Framework.dll"));
    foreach(var t in mg.GetTypes().Where(t=>t.Name.Contains("Lzx",StringComparison.OrdinalIgnoreCase)))
    {Console.WriteLine(t.FullName);foreach(var m in t.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))Console.WriteLine(m);}
    return;
}
if(args.Contains("--forge-inspect"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var ha=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"smapi-internal","0Harmony.dll"));
    var original=ha.GetType("HarmonyLib.PatchProcessor")!.GetMethods().Single(m=>m.Name=="GetOriginalInstructions"&&m.GetParameters()[1].ParameterType.IsByRef);
    Directory.CreateDirectory(".tools/forge-reference");
    var flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    foreach(string name in new[]{"StardewValley.Menus.ForgeMenu","StardewValley.Tools.MeleeWeapon","StardewValley.Tool"})
    {
        var type=ga.GetType(name,true)!;
        if(type.Name=="ForgeMenu")foreach(var m in type.GetMethods(flags).Where(m=>m.DeclaringType==type))Console.WriteLine("MENU "+m);
        if(type.Name=="ForgeMenu")foreach(var f in type.GetFields(flags).Where(f=>f.FieldType==typeof(string)))Console.WriteLine("TEXT "+f);
        foreach(var f in type.GetFields(flags).Where(f=>f.Name.Contains("ppearance")||f.Name.Contains("forge",StringComparison.OrdinalIgnoreCase)))Console.WriteLine(type.Name+" FIELD "+f);
        foreach(var method in type.GetMethods(flags).Where(m=>m.DeclaringType==type && (m.Name.Contains("forge",StringComparison.OrdinalIgnoreCase)||m.Name.Contains("ppearance")||m.Name is "GetMenuViewIndex" or "drawInMenu" or "drawDuringUse" or "drawWhenHeld" or "GetDrawnItemId" or "ResetIndexOfMenuItemView" or "_ValidateCraft" or "UpdateDescriptionText" or "IsValidCraft" or "CraftItem" or "update" or "receiveLeftClick" or "GetForgeCost")))
        {
            Console.WriteLine(type.Name+" "+method);
            if(method.IsAbstract)continue;
            var il=(System.Collections.IEnumerable)original.Invoke(null,new object?[]{method,null})!;
            File.WriteAllLines($".tools/forge-reference/{type.Name}.{method.Name}-{method.GetParameters().Length}.txt",il.Cast<object>().Select(x=>x.ToString()!));
        }
    }
    return;
}
if(args.Contains("--received"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    var harmonyAssembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"smapi-internal","0Harmony.dll"));
    var original=harmonyAssembly.GetType("HarmonyLib.PatchProcessor")!.GetMethods().Single(m=>m.Name=="GetOriginalInstructions"&&m.GetParameters()[1].ParameterType.IsByRef);
    Directory.CreateDirectory(".tools/received-reference");
    foreach(var nested in ga.GetType("StardewValley.Farmer",true)!.GetNestedTypes(flags))
    foreach(var method in nested.GetMethods(flags).Where(m=>m.Name.Contains("holdUpItemThenMessage")))
    {
        var il=(System.Collections.IEnumerable)original.Invoke(null,new object?[]{method,null})!;
        File.WriteAllLines($".tools/received-reference/{nested.Name.Replace('<','_').Replace('>','_')}.{method.Name.Replace('<','_').Replace('>','_')}.txt",il.Cast<object>().Select(x=>x.ToString()!));
    }
    foreach(string typeName in new[]{"StardewValley.Farmer","StardewValley.FarmerSprite","StardewValley.Game1"})
    {
        var type=ga.GetType(typeName,true)!;
        foreach(var method in type.GetMethods(flags).Where(m=>m.Name.Contains("holdUp",StringComparison.OrdinalIgnoreCase)||(m.Name.Contains("showItem",StringComparison.OrdinalIgnoreCase)||m.Name=="showHoldingItem"||m.Name=="showReceiveNewItemMessage")||m.Name.Contains("jitter",StringComparison.OrdinalIgnoreCase)||m.Name=="animateOnce"))
        {
            Console.WriteLine(type.Name+" "+method+" "+string.Join(", ",method.GetParameters().Select(p=>p.Name)));
            if(method.Name.Contains("holdUp",StringComparison.OrdinalIgnoreCase)||(method.Name.Contains("showItem",StringComparison.OrdinalIgnoreCase)||method.Name=="showHoldingItem"||method.Name=="showReceiveNewItemMessage"))
            {
                var il=(System.Collections.IEnumerable)original.Invoke(null,new object?[]{method,null})!;
                File.WriteAllLines($".tools/received-reference/{type.Name}.{method.Name}.txt",il.Cast<object>().Select(x=>x.ToString()!));
            }
        }
    }
    return;
}
if(args.Contains("--ui"))
{
    var gameTypes=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var mono=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"MonoGame.Framework.dll"));
    foreach(var type in new[]{gameTypes.GetType("StardewValley.Game1")!,mono.GetType("Microsoft.Xna.Framework.Graphics.SpriteBatch")!,mono.GetType("Microsoft.Xna.Framework.Graphics.SpriteEffect")!})
    foreach(var member in type.GetMembers(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>type.Name is "SpriteBatch" or "SpriteEffect" || m.Name.Contains("matrix",StringComparison.OrdinalIgnoreCase) || m.Name.Contains("uiscale",StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(member+(member is FieldInfo f?$" public={f.IsPublic} static={f.IsStatic}":""));
    return;
}
if(args.Contains("--wall"))
{
    var ga=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var f=ga.GetType("StardewValley.Objects.Furniture",true)!;
    object item=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(f);
    var flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    var typeMethod=f.GetMethod("getTypeNumberFromName",flags)!;
    int type=(int)typeMethod.Invoke(typeMethod.IsStatic?null:item,new object[]{"painting"})!;
    if(type!=6)throw new Exception("Native painting type mismatch");
    var field=f.GetField("furniture_type",flags)!;
    object net=Activator.CreateInstance(field.FieldType)!;
    field.FieldType.GetProperty("Value")!.SetValue(net,6);field.SetValue(item,net);
    if((bool)f.GetMethod("isGroundFurniture",flags)!.Invoke(item,null)!)throw new Exception("Painting allowed on ground");
    Console.WriteLine("PASS installed game recognizes painting and rejects ground furniture behavior");
    var harmonyAssembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"smapi-internal","0Harmony.dll"));
    var processor=harmonyAssembly.GetType("HarmonyLib.PatchProcessor")!;
    var original=processor.GetMethods().Single(m=>m.Name=="GetOriginalInstructions"&&m.GetParameters()[1].ParameterType.IsByRef);
    foreach(string name in new[]{"canBePlacedHere","GetAdditionalFurniturePlacementStatus","rotate"})
    {
        var method=f.GetMethod(name,flags)!;
        var il=(System.Collections.IEnumerable)original.Invoke(null,new object?[]{method,null})!;
        File.WriteAllLines(Path.Combine(".tools",name+".il.txt"),il.Cast<object>().Select(x=>x.ToString()!));
    }
    return;
}
var patches=assembly.GetType("BetterBeads.Runtime.ProductPatches",true)!;
var scaledBatch=assembly.GetType("BetterBeads.Runtime.ScaledUiBatch");
if(scaledBatch is not null)
{
    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(scaledBatch.TypeHandle);
    Console.WriteLine("PASS scaled UI batch state bindings match installed MonoGame");
}
try
{
    patches.GetMethod("Apply",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new object?[]{"BetterBeads.OfflinePatchSmoke",null,null,null});
    Console.WriteLine("PASS ProductPatches.Apply: all product, clothing and weapon patches registered against installed game assemblies");
    var gameAssembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"Stardew Valley.dll"));
    var furniture=gameAssembly.GetType("StardewValley.Objects.Furniture",true)!;
    var binding=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    foreach(int logicalHeight in new[]{16,32})
    {
        object sample=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(furniture);
        void Field(string name,params object[] values)
        {
            var field=furniture.GetField(name,binding)!;object net=Activator.CreateInstance(field.FieldType)!;
            var value=field.FieldType.GetProperty("Value")!;
            if(values.Length>0)value.SetValue(net,Activator.CreateInstance(value.PropertyType,values));
            field.SetValue(sample,net);
        }
        Field("boundingBox",128,256,128,64);Field("sourceRect",0,0,32,logicalHeight);Field("drawPosition");
        furniture.GetMethod("updateDrawPosition",binding)!.Invoke(sample,null);
        var net=furniture.GetField("drawPosition",binding)!.GetValue(sample)!;
        var position=net.GetType().GetProperty("Value")!.GetValue(net)!;
        if((float)position.GetType().GetField("Y")!.GetValue(position)! != 256-(logicalHeight*4-64))
            throw new Exception("Tall furniture native base alignment changed");
    }
    Console.WriteLine("PASS native tall furniture draws above its one-row collision base");
    var harmony=AssemblyLoadContext.Default.Assemblies.Single(a=>a.GetName().Name=="0Harmony").GetType("HarmonyLib.Harmony",true)!;
    var getInfo=harmony.GetMethod("GetPatchInfo",BindingFlags.Public|BindingFlags.Static)!;
    foreach(var (name,kind,patchName) in new[]{("IsHeldOverHead","Postfixes","HeldOverHead"),("drawWhenHeld","Prefixes","DrawHeldFurniture"),("drawInMenu","Prefixes","DrawFurnitureIcon")})
    {
        var method=name=="drawInMenu"?furniture.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Single(m=>m.Name==name&&m.GetParameters().Length==8)
            :furniture.GetMethod(name,BindingFlags.Public|BindingFlags.Instance)!;
        var info=getInfo.Invoke(null,new object[]{method})??throw new Exception("Missing furniture patch: "+name);
        var entries=(System.Collections.IEnumerable)info.GetType().GetField(kind)!.GetValue(info)!;
        if(!entries.Cast<object>().Any(p=>((MethodInfo)p.GetType().GetProperty("PatchMethod")!.GetValue(p)!).Name==patchName))
            throw new Exception("Missing furniture behavior: "+patchName);
        Console.WriteLine("PASS furniture carry/icon hook: "+name);
    }
    var weaponType=gameAssembly.GetType("StardewValley.Tools.MeleeWeapon",true)!;
    var weaponDraw=weaponType.GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name=="drawDuringUse");
    if(!weaponDraw.GetParameters().Any(p=>p.Name=="weaponItemId"))throw new Exception("Native swing item ID parameter changed");
    var weaponInfo=getInfo.Invoke(null,new object[]{weaponDraw})??throw new Exception("Native swing method not patched");
    var weaponPrefixes=(System.Collections.IEnumerable)weaponInfo.GetType().GetField("Prefixes")!.GetValue(weaponInfo)!;
    var weaponTranspilers=(System.Collections.IEnumerable)weaponInfo.GetType().GetField("Transpilers")!.GetValue(weaponInfo)!;
    if(!weaponPrefixes.Cast<object>().Any()||!weaponTranspilers.Cast<object>().Any())
        throw new Exception("Swing context or sprite-draw replacement missing");
    Console.WriteLine("PASS native weapon swing has the direction context and sprite-draw replacement");
    foreach(string name in new[]{"drawAtNonTileSpot","drawPlacementBounds"})
    {
        var inherited=furniture.GetMethod(name,BindingFlags.Public|BindingFlags.Instance)!;
        var method=inherited.DeclaringType!.GetMethod(name,BindingFlags.Public|BindingFlags.Instance)!;
        var info=getInfo.Invoke(null,new object[]{method})??throw new Exception("Preview method not patched: "+name);
        var prefixes=(System.Collections.IEnumerable)info.GetType().GetField("Prefixes")!.GetValue(info)!;
        if(!prefixes.Cast<object>().Any())throw new Exception("Missing preview context: "+name);
        Console.WriteLine("PASS artwork context for placement preview: "+name);
    }
    foreach(var draw in furniture.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>m.Name=="draw"))
    {
        var info=getInfo.Invoke(null,new object[]{draw})??throw new Exception("Draw not patched");
        var transpilers=(System.Collections.IEnumerable)info.GetType().GetField("Transpilers")!.GetValue(info)!;
        if(!transpilers.Cast<object>().Any())throw new Exception("Missing furniture front transpiler");
        Console.WriteLine("PASS furniture overload: "+draw);
    }
}
catch(Exception ex){Console.Error.WriteLine(ex is TargetInvocationException invocation?invocation.InnerException:ex);Environment.ExitCode=1;}
