using System.Reflection;
using System.Runtime.Serialization;
using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Locations;
using xTile;
using xTile.Dimensions;
using xTile.Layers;
using xTile.Tiles;

internal static class ReliabilityRuntimeChecks
{
    private static bool busy;
    private static int undoCalls,leaveCalls;
    private static GamePadState pad;
    private static bool PadState(ref GamePadState __result){__result=pad;return false;}
    private static bool ControllerMode(ref bool __result){__result=true;return false;}
    private static bool Busy(ref bool __result){__result=busy;return false;}
    private static bool Undo(){undoCalls++;return false;}
    private static bool Leave(object __instance){leaveCalls++;AccessTools.Field(__instance.GetType(),"padCanvas").SetValue(__instance,false);return false;}
    private static bool PadTick()=>false;
    private static void NativeFloorFailure(DecoratableLocation __instance,string which_room)
    {
        var back=__instance.map.GetLayer("Back");
        back.Tiles[0,0]=new StaticTile(back,back.Tiles[0,0].TileSheet,BlendMode.Alpha,99);
        back.Tiles[3,0]=new StaticTile(back,back.Tiles[0,0].TileSheet,BlendMode.Alpha,99);
        __instance.appliedFloor[which_room]="failed-floor";
        throw new InvalidOperationException("Injected floor failure after changing a previously empty cell");
    }
    private static void NativeFailure(DecoratableLocation __instance,string which_room)
    {
        var back=__instance.map.GetLayer("Back");var buildings=__instance.map.GetLayer("Buildings");
        back.Tiles[0,0]=new StaticTile(back,back.Tiles[0,0].TileSheet,BlendMode.Alpha,99);
        buildings.Tiles[1,0]=new StaticTile(buildings,buildings.Tiles[1,0].TileSheet,BlendMode.Alpha,98);
        __instance.appliedWallpaper[which_room]="failed-new";
        throw new InvalidOperationException("Injected failure after modifying both native layers");
    }
    internal static void Run()
    {
        int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        var runtime=typeof(Blueprint).Assembly.GetType("BetterBeads.Runtime.DecorationPlacement",true)!;
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var map=new Map();var back=new Layer("Back",map,new Size(4,4),new Size(16,16));
        var buildings=new Layer("Buildings",map,new Size(4,4),new Size(16,16));map.AddLayer(back);map.AddLayer(buildings);
        var native=new TileSheet("walls_and_floors",map,"fixture",new Size(16,16),new Size(16,16));
        var unrelated=new TileSheet("outdoors",map,"fixture",new Size(16,16),new Size(16,16));map.AddTileSheet(native);map.AddTileSheet(unrelated);
        back.Tiles[0,0]=new StaticTile(back,native,BlendMode.Alpha,1);
        back.Tiles[1,0]=new StaticTile(back,native,BlendMode.Alpha,2);
        buildings.Tiles[1,0]=new StaticTile(buildings,native,BlendMode.Alpha,3);
        back.Tiles[2,0]=new StaticTile(back,unrelated,BlendMode.Alpha,4);
        back.Tiles[0,0].Properties["Test"]="keep-back";buildings.Tiles[1,0].Properties["Test"]="keep-buildings";
        var room=(DecoratableLocation)FormatterServices.GetUninitializedObject(typeof(DecoratableLocation));room.map=map;
        foreach(string field in new[]{"appliedWallpaper","appliedFloor"})
        {var f=AccessTools.Field(typeof(DecoratableLocation),field);f.SetValue(room,Activator.CreateInstance(f.FieldType));}
        var modData=AccessTools.Field(typeof(GameLocation),"<modData>k__BackingField");modData.SetValue(room,Activator.CreateInstance(modData.FieldType));
        var tiles=new List<Vector3>{new(0,0,0),new(1,0,2),new(2,0,0),new(3,0,0),new(-1,0,0)};
        AccessTools.Field(typeof(DecoratableLocation),"wallpaperTiles").SetValue(room,new Dictionary<string,List<Vector3>>{{"room",tiles}});
        AccessTools.Field(typeof(DecoratableLocation),"floorTiles").SetValue(room,new Dictionary<string,List<Vector3>>{{"room",tiles}});
        var select=runtime.GetMethod("RegionTargets",flags)!;
        var wall=(List<(Layer Layer,int X,int Y)>)select.Invoke(null,new object[]{room,tiles,false})!;
        Check(wall.Count==2&&ReferenceEquals(wall[0].Layer,back)&&ReferenceEquals(wall[1].Layer,buildings),
            "native bottom-wall tile uses Buildings; unrelated, missing and outside tiles are excluded");
        var floor=(List<(Layer Layer,int X,int Y)>)select.Invoke(null,new object[]{room,tiles,true})!;
        Check(floor.Count==2&&floor.All(t=>ReferenceEquals(t.Layer,back)),"floor validation only accepts native Back tiles");
        var custom=new TileSheet("BetterBeadsDecorfixture",map,"fixture",new Size(16,1),new Size(16,16));map.AddTileSheet(custom);
        var harmony=new Harmony("BetterBeads.ReliabilityFixture");
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),"IsWallAndFloorTilesheet",new[]{typeof(string)}),
            postfix:new HarmonyMethod(runtime.GetMethod("DecorSheet",flags)!));
        back.Tiles[0,1]=new StaticTile(back,custom,BlendMode.Alpha,0);
        var cover=(List<(Layer Layer,int X,int Y)>)select.Invoke(null,new object[]{room,new[]{new Vector3(0,1,0)},true})!;
        Check(cover.Count==1,"native decoration can replace a previously applied bead surface");
        var snapshot=new ProductSnapshot{Design=SimpleCrafting.Blank(SimpleCrafting.Wallpaper16)};
        snapshot.Design.Views["front"].Cells[0]=new(){ColorId="test",MaterialId="decoration",Rgba=0x123456ff};
        var commit=runtime.GetMethod("CommitRegion",flags)!;
        const string key="xinzh.BetterBeads/decor/wall/room";
        room.modData[key]="old record";room.appliedWallpaper["room"]="old native";
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),"SetWallpaper",new[]{typeof(string),typeof(string)}),
            prefix:new HarmonyMethod(typeof(ReliabilityRuntimeChecks),nameof(NativeFailure)));
        bool result=(bool)commit.Invoke(null,new object?[]{room,"room",false,snapshot,"new-native",null})!;
        Check(!result&&room.modData[key]=="old record"&&room.appliedWallpaper["room"]=="old native",
            "native failure restores artwork record and applied wallpaper identity");
        Check(back.Tiles[0,0].TileIndex==1&&buildings.Tiles[1,0].TileIndex==3
            &&back.Tiles[0,0].Properties["Test"].ToString()=="keep-back"
            &&buildings.Tiles[1,0].Properties["Test"].ToString()=="keep-buildings",
            "failure restores both actual map layers and their properties");
        room.modData.Remove(key);room.appliedWallpaper.Remove("room");
        result=(bool)commit.Invoke(null,new object?[]{room,"room",false,snapshot,"new-native",null})!;
        Check(!result&&!room.modData.ContainsKey(key)&&!room.appliedWallpaper.ContainsKey("room"),
            "first placement failure does not leave new native or mod records");
        result=(bool)commit.Invoke(null,new object?[]{room,"room",false,snapshot,"new-native",new Point(2,0)})!;
        Check(!result&&back.Tiles[2,0].TileIndex==4,"clicking a non-decoratable tile cannot alter the region");
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),"SetFloor",new[]{typeof(string),typeof(string)}),
            prefix:new HarmonyMethod(typeof(ReliabilityRuntimeChecks),nameof(NativeFloorFailure)));
        var flooring=new ProductSnapshot{Design=SimpleCrafting.Blank(SimpleCrafting.Flooring16)};
        flooring.Design.Views["front"].Cells[0]=new(){ColorId="test",MaterialId="decoration",Rgba=0x123456ff};
        room.appliedFloor["room"]="old-floor";
        result=(bool)commit.Invoke(null,new object?[]{room,"room",true,flooring,"new-floor",null})!;
        Check(!result&&room.appliedFloor["room"]=="old-floor"&&!room.modData.ContainsKey("xinzh.BetterBeads/decor/floor/room")
            &&back.Tiles[0,0].TileIndex==1&&back.Tiles[3,0] is null,"floor rollback restores native identity, tile data and previously empty cells");
        var assembly=typeof(Blueprint).Assembly;
        var menuType=assembly.GetType("BetterBeads.Runtime.WorkbenchMenu",true)!;
        var editorType=assembly.GetType("BetterBeads.Runtime.EditorPanel",true)!;
        var inputType=assembly.GetType("BetterBeads.Runtime.WorkbenchInput",true)!;
        var onlineType=assembly.GetType("BetterBeads.Runtime.OnlineSession",true)!;
        void Patch(MethodInfo method,string handler)=>harmony.Patch(method,prefix:new HarmonyMethod(typeof(ReliabilityRuntimeChecks),handler));
        Patch(AccessTools.Method(typeof(GamePad),"GetState",new[]{typeof(PlayerIndex)}),nameof(PadState));
        Patch(AccessTools.PropertyGetter(inputType,"Controller"),nameof(ControllerMode));
        Patch(AccessTools.PropertyGetter(onlineType,"Busy"),nameof(Busy));
        Patch(AccessTools.Method(editorType,"ControllerUndo"),nameof(Undo));
        Patch(AccessTools.Method(editorType,"ControllerLeaveCanvas"),nameof(Leave));
        Patch(AccessTools.Method(editorType,"ControllerTick"),nameof(PadTick));
        var menu=FormatterServices.GetUninitializedObject(menuType);var editor=FormatterServices.GetUninitializedObject(editorType);
        AccessTools.Field(menuType,"editor").SetValue(menu,editor);AccessTools.Field(menuType,"selected").SetValue(menu,1);
        var canvas=AccessTools.Field(editorType,"padCanvas");canvas.SetValue(editor,true);
        var update=AccessTools.Method(menuType,"UpdatePad");
        pad=new GamePadState(Vector2.Zero,Vector2.Zero,0,0,Buttons.LeftShoulder);busy=true;
        update.Invoke(menu,null);
        Check(pad.IsConnected&&undoCalls==0&&leaveCalls==1,"busy online transaction exits controller painting and does not undo");
        busy=false;canvas.SetValue(editor,true);update.Invoke(menu,null);
        Check(undoCalls==0,"held button after waiting is not replayed as a new undo");
        pad=new GamePadState(Vector2.Zero,Vector2.Zero,0,0,(Buttons)0);update.Invoke(menu,null);
        pad=new GamePadState(Vector2.Zero,Vector2.Zero,0,0,Buttons.LeftShoulder);update.Invoke(menu,null);
        Check(undoCalls==1,"release and fresh press still undo normally after online waiting ends");
        Console.WriteLine($"PASS {passed} reliability runtime checks. Native failure is injected; no game is launched.");
    }
}
