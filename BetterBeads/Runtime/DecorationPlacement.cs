#if BEADS_LITE
using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using xTile.Dimensions;
using xTile.Tiles;
using System.Text.Json;
using System.Reflection;
using System.Reflection.Emit;
using xTile.Display;
using Rectangle=Microsoft.Xna.Framework.Rectangle;

namespace BetterBeads.Runtime;

/// <summary>Stores one detached artwork per native decorating area. The game owns the applied area ID;
/// only the appearance of its wallpaper/floor tiles is replaced.</summary>
internal static class DecorationPlacement
{
    private sealed class DecorRecord
    {
        public string Code {get;set;}="";
        public string Name {get;set;}="";
        public string InstanceId {get;set;}="";
    }
    private const string Prefix="xinzh.BetterBeads/decor/";
    private const string TexturePrefix="Mods/xinzh.BetterBeads/Decor/";
    private static ProductItems products=null!;
    private static IModHelper contentHelper=null!;
    private static IMonitor monitor=null!;
    private static bool denseTilesEnabled;
    private delegate bool NativeTileCheck(DecoratableLocation location,int x,int y,string layer);
    private static NativeTileCheck? nativeTileCheck;
    private static bool tileCheckBound;
    private static readonly Dictionary<string,ProductSnapshot> patterns=new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<xTile.Map> touchedMaps=new();
    private static readonly HashSet<string> applying=new(StringComparer.Ordinal);
    private static int lastLocalSignature;
    private static int lastArtRevision;
    private static DecoratableLocation? lastLocalLocation;
    public static void Apply(string harmonyId,ProductItems source,IModHelper helper,IMonitor log)
    {
        products=source;contentHelper=helper;monitor=log;lastArtRevision=ArtResources.FrameRevision;
        var harmony=new Harmony(harmonyId);
        try
        {
            harmony.Patch(AccessTools.Method(typeof(XnaDisplayDevice),"DrawImpl"),
                transpiler:new HarmonyMethod(typeof(DecorationPlacement),nameof(DenseTileCalls)));
            if(!denseTilesEnabled)monitor.Log("Detailed decoration tiles are unavailable in this game build; using ordinary tiles.",LogLevel.Warn);
        }
        catch(Exception ex)
        {
            denseTilesEnabled=false;
            monitor.Log("Detailed decoration tiles are unavailable; using ordinary tiles: "+ex.Message,LogLevel.Warn);
        }
        harmony.Patch(AccessTools.Method(typeof(Wallpaper),nameof(Wallpaper.placementAction),new[]{typeof(GameLocation),typeof(int),typeof(int),typeof(Farmer)}),
            prefix:new HarmonyMethod(typeof(DecorationPlacement),nameof(Place)));
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),nameof(DecoratableLocation.SetWallpaper)),
            prefix:new HarmonyMethod(typeof(DecorationPlacement),nameof(SetWallpaper)));
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),nameof(DecoratableLocation.SetFloor)),
            prefix:new HarmonyMethod(typeof(DecorationPlacement),nameof(SetFloor)));
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),nameof(DecoratableLocation.UpdateWallpaper)),
            postfix:new HarmonyMethod(typeof(DecorationPlacement),nameof(Updated)));
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),nameof(DecoratableLocation.UpdateFloor)),
            postfix:new HarmonyMethod(typeof(DecorationPlacement),nameof(Updated)));
        harmony.Patch(AccessTools.Method(typeof(DecoratableLocation),"IsWallAndFloorTilesheet",new[]{typeof(string)}),
            postfix:new HarmonyMethod(typeof(DecorationPlacement),nameof(DecorSheet)));
        helper.Events.GameLoop.SaveLoaded+=(_,_)=>RestoreAll();
        helper.Events.Display.RenderedWorld+=(_,e)=>DrawPlacementArea(e.SpriteBatch,helper);
        helper.Events.Player.Warped+=(_,e)=>{if(e.NewLocation is DecoratableLocation decor){Restore(decor);ReleaseUnusedPatterns();}};
        helper.Events.GameLoop.UpdateTicked+=(_,e)=>
        {
            if(!e.IsMultipleOf(30))return;
            if(lastArtRevision!=ArtResources.FrameRevision)
            {lastArtRevision=ArtResources.FrameRevision;RefreshArt();}
            if(Game1.currentLocation is not DecoratableLocation decor)return;
            var hash=new HashCode();
            foreach(var pair in decor.modData.Pairs.Where(p=>p.Key.StartsWith(Prefix,StringComparison.Ordinal)).OrderBy(p=>p.Key,StringComparer.Ordinal))
            {hash.Add(pair.Key,StringComparer.Ordinal);hash.Add(pair.Value,StringComparer.Ordinal);}
            int signature=hash.ToHashCode();
            if(!ReferenceEquals(lastLocalLocation,decor)||lastLocalSignature!=signature)
            {lastLocalLocation=decor;lastLocalSignature=signature;Restore(decor);ReleaseUnusedPatterns();}
        };
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>{patterns.Clear();touchedMaps.Clear();applying.Clear();lastLocalSignature=0;lastArtRevision=ArtResources.FrameRevision;lastLocalLocation=null;placementKey=null;placementTargets.Clear();};
    }
    public static bool TryAsset(AssetRequestedEventArgs e)
    {
        string name=e.NameWithoutLocale.Name;
        if(!name.StartsWith(TexturePrefix,StringComparison.OrdinalIgnoreCase))return false;
        string key=name[TexturePrefix.Length..];
        if(!patterns.TryGetValue(key,out var snapshot))return false;
        e.LoadFrom(()=>Texture(snapshot),AssetLoadPriority.Low);return true;
    }
    private static Texture2D Texture(ProductSnapshot snapshot)
    {
        var atlas=DecorationPattern.Atlas(snapshot.Design,ArtResources.Definition,denseTilesEnabled);
        var pixels=atlas.Pixels.Select(rgba=>new Color((byte)(rgba>>24),(byte)(rgba>>16),(byte)(rgba>>8),(byte)255)).ToArray();
        var texture=new Texture2D(Game1.graphics.GraphicsDevice,atlas.Width,atlas.Height);
        try{texture.SetData(pixels);}catch{texture.Dispose();throw;}
        if(denseTilesEnabled)TextureCache.MarkDense(texture);
        return texture;
    }
    private static IEnumerable<CodeInstruction> DenseTileCalls(IEnumerable<CodeInstruction> source)
    {
        bool matched=false;
        foreach(var instruction in source)
        {
            if(instruction.operand is MethodInfo draw&&draw.DeclaringType==typeof(SpriteBatch)&&draw.Name==nameof(SpriteBatch.Draw)
                &&draw.GetParameters() is {Length:9} args&&args[1].ParameterType==typeof(Vector2)
                &&args[6].ParameterType==typeof(float))
            {
                instruction.opcode=OpCodes.Call;
                instruction.operand=AccessTools.Method(typeof(DecorationPlacement),nameof(DrawDecorTile));
                matched=true;
            }
            yield return instruction;
        }
        denseTilesEnabled=matched;
    }
    private static void DrawDecorTile(SpriteBatch batch,Texture2D texture,Vector2 position,Rectangle? source,
        Color color,float rotation,Vector2 origin,float scale,SpriteEffects effects,float layer)
    {
        int density=texture.Width==1024&&texture.Height==64?TextureCache.Density(texture):1;
        if(density>1)
        {
            if(source is {} r)source=new Rectangle(r.X*density,r.Y*density,r.Width*density,r.Height*density);
            origin*=density;scale/=density;
        }
        batch.Draw(texture,position,source,color,rotation,origin,scale,effects,layer);
    }
    private static bool Place(Wallpaper __instance,GameLocation location,int x,int y,Farmer who,ref bool __result)
    {
        var snapshot=products.Read(__instance);
        if(snapshot is null||!SimpleCrafting.IsDecoration(snapshot.Design.Use))return true;
        if(location is not DecoratableLocation decor||who is null||!ReferenceEquals(who.currentLocation,location))
        {__result=false;return false;}
        if(Context.IsMultiplayer&&!Context.IsMainPlayer)
        {OnlineSession.Current?.Decorate(snapshot,location,x,y);__result=false;return false;}
        __result=PlaceApproved(__instance,location,x,y,who);
        return false;
    }
    internal static bool PlaceApproved(Wallpaper item,GameLocation location,int x,int y,Farmer who)
    {
        var snapshot=products.Read(item);
        if(snapshot is null||!SimpleCrafting.IsDecoration(snapshot.Design.Use))return false;
        if(location is not DecoratableLocation decor||decor.IsOutdoors||who is null||!ReferenceEquals(who.currentLocation,location))return false;
        bool floor=snapshot.Design.Use==ProductUse.Flooring;
        if(decor.wallpaperTiles.Count==0&&decor.floorTiles.Count==0)decor.ReadWallpaperAndFloorTileData();
        var areas=floor?decor.floorTiles:decor.wallpaperTiles;
        var point=new Point(x/64,y/64);
        var match=areas.FirstOrDefault(area=>area.Value.Any(tile=>(int)tile.X==point.X&&(int)tile.Y==point.Y));
        if(match.Key is null||!SimpleCrafting.CanSell(snapshot))return false;
        return CommitRegion(decor,match.Key,floor,snapshot,item.ItemId,point);
    }
    internal static bool CommitRegion(DecoratableLocation decor,string region,bool floor,ProductSnapshot snapshot,string nativeId,Point? clicked=null)
    {
        var areas=floor?decor.floorTiles:decor.wallpaperTiles;
        if(!areas.TryGetValue(region,out var regionTiles))return false;
        if(decor.map is null)return false;
        var targets=RegionTargets(decor,regionTiles,floor);
        if(targets.Count==0||clicked is {} point&&!targets.Any(t=>t.X==point.X&&t.Y==point.Y))return false;
        // Native wallpaper can change both Back and Buildings; retain even empty cells.
        var originalTiles=new List<(xTile.Layers.Layer Layer,int X,int Y,Tile? Tile)>();
        foreach(var name in new[]{"Back","Buildings"})
        {
            var layer=decor.map.GetLayer(name);if(layer is null)continue;
            foreach(var p in regionTiles.Select(t=>((int)t.X,(int)t.Y)).Distinct())
                if(p.Item1>=0&&p.Item2>=0&&p.Item1<layer.LayerWidth&&p.Item2<layer.LayerHeight)
                {
                    var source=layer.Tiles[p.Item1,p.Item2];
                    var backup=source?.Clone(layer);
                    // xTile.Clone retains sheet/index but does not retain per-tile properties.
                    if(source is not null&&backup is not null)backup.Properties.CopyFrom(source.Properties);
                    originalTiles.Add((layer,p.Item1,p.Item2,backup));
                }
        }
        string recordKey=Prefix+(floor?"floor/":"wall/")+region;
        string? previous=decor.modData.TryGetValue(recordKey,out var old)?old:null;
        var native=floor?decor.appliedFloor:decor.appliedWallpaper;
        bool hadNative=native.TryGetValue(region,out string previousNative);
        string value=DesignStorage.Serialize(new DecorRecord{Code=BlueprintSharing.Encode(snapshot.Design),Name=snapshot.Design.Name,InstanceId=snapshot.InstanceId});
        try
        {
            // The native decorating call replaces the old surface. No item is consumed if it fails.
            decor.modData[recordKey]=value;
            applying.Add(recordKey);
            if(floor)decor.SetFloor(nativeId,region);
            else decor.SetWallpaper(nativeId,region);
            Register(snapshot);
            if(ApplyRegion(decor,region,floor,snapshot)!=targets.Count)throw new InvalidOperationException("Decoration tiles changed during placement");
            ReleaseUnusedPatterns();
            return true;
        }
        catch
        {
            if(previous is null)decor.modData.Remove(recordKey);else decor.modData[recordKey]=previous;
            if(hadNative)native[region]=previousNative;else native.Remove(region);
            foreach(var tile in originalTiles)tile.Layer.Tiles[tile.X,tile.Y]=tile.Tile;
            ReleaseUnusedPatterns();
            return false;
        }
        finally{applying.Remove(recordKey);placementKey=null;}
    }
    private static (DecoratableLocation Location,string Region,bool Floor)? placementKey;
    private static List<(xTile.Layers.Layer Layer,int X,int Y)> placementTargets=new();
    private static void DrawPlacementArea(SpriteBatch batch,IModHelper helper)
    {
        if(!Context.IsWorldReady||Game1.eventUp||Game1.activeClickableMenu is not null
            ||Game1.currentLocation is not DecoratableLocation location||location.IsOutdoors
            ||Game1.player.CurrentItem is not Wallpaper item||products.Read(item) is not {} product
            ||!SimpleCrafting.IsDecoration(product.Design.Use)){placementKey=null;placementTargets.Clear();return;}
        bool floor=product.Design.Use==ProductUse.Flooring;
        if(location.wallpaperTiles.Count==0&&location.floorTiles.Count==0)location.ReadWallpaperAndFloorTileData();
        var cursor=helper.Input.GetCursorPosition().AbsolutePixels;int x=(int)cursor.X/64,y=(int)cursor.Y/64;
        var regions=floor?location.floorTiles:location.wallpaperTiles;
        var region=regions.FirstOrDefault(a=>a.Value.Any(t=>(int)t.X==x&&(int)t.Y==y));
        if(region.Key is null){placementKey=null;placementTargets.Clear();return;}
        var key=(location,region.Key,floor);
        if(placementKey!=key){placementKey=key;placementTargets=RegionTargets(location,region.Value,floor);}
        if(!placementTargets.Any(t=>t.X==x&&t.Y==y))return;
        foreach(var target in placementTargets)
        {
            var point=Game1.GlobalToLocal(Game1.viewport,new Vector2(target.X*64,target.Y*64));
            var r=new Rectangle((int)point.X,(int)point.Y,64,64);var ink=new Color(255,213,105)*.8f;
            batch.Draw(Game1.staminaRect,r,new Color(255,213,105)*.14f);
            batch.Draw(Game1.staminaRect,new Rectangle(r.X,r.Y,64,2),ink);
            batch.Draw(Game1.staminaRect,new Rectangle(r.X,r.Bottom-2,64,2),ink);
            batch.Draw(Game1.staminaRect,new Rectangle(r.X,r.Y,2,64),ink);
            batch.Draw(Game1.staminaRect,new Rectangle(r.Right-2,r.Y,2,64),ink);
        }
    }
    private static void SetWallpaper(DecoratableLocation __instance,string which,string which_room)
        =>ForgetIfNative(__instance,which,which_room,false);
    private static void SetFloor(DecoratableLocation __instance,string which,string which_room)
        =>ForgetIfNative(__instance,which,which_room,true);
    private static void ForgetIfNative(DecoratableLocation location,string which,string room,bool floor)
    {
        string key=Prefix+(floor?"floor/":"wall/")+room;
        if(!applying.Contains(key)&&which is not (SimpleCrafting.Wallpaper16 or SimpleCrafting.Wallpaper32 or SimpleCrafting.Flooring16 or SimpleCrafting.Flooring32))
            location.modData.Remove(key);
    }
    private static void DecorSheet(string __0,ref bool __result)
    {if(__0?.StartsWith("BetterBeadsDecor",StringComparison.Ordinal)==true)__result=true;}
    private static void Updated(DecoratableLocation __instance)
    {placementKey=null;if(applying.Count>0)return;Restore(__instance);ReleaseUnusedPatterns();}
    private static void RestoreAll()
    {
        if(!Context.IsWorldReady)return;
        foreach(var location in Game1.locations.OfType<DecoratableLocation>())Restore(location);
    }
    private static void Restore(DecoratableLocation location)
    {
        foreach(var pair in location.modData.Pairs.Where(p=>p.Key.StartsWith(Prefix,StringComparison.Ordinal)).ToArray())
        {
            bool floor=pair.Key.StartsWith(Prefix+"floor/",StringComparison.Ordinal);
            string region=pair.Key[(Prefix.Length+(floor?6:5))..];
            DecorRecord? stored=null;
            try{stored=JsonSerializer.Deserialize<DecorRecord>(pair.Value);}catch(JsonException){}
            if(stored is not null&&BlueprintSharing.TryDecode(stored.Code,out var design)&&design is not null
                &&SimpleCrafting.IsDecoration(design.Use)&&floor==(design.Use==ProductUse.Flooring))
            {
                design.Name=stored.Name;
                var snapshot=new ProductSnapshot{InstanceId=stored.InstanceId,Design=design};
                Register(snapshot);ApplyRegion(location,region,floor,snapshot);
            }
        }
    }
    private static void Register(ProductSnapshot snapshot)
    {
        string key=DesignStorage.VisualKey(snapshot);
        if(!patterns.ContainsKey(key))patterns.Add(key,snapshot.Copy());
    }
    private static int ApplyRegion(DecoratableLocation location,string room,bool floor,ProductSnapshot snapshot)
    {
        var regions=floor?location.floorTiles:location.wallpaperTiles;
        if(!regions.TryGetValue(room,out var tiles)||tiles.Count==0||location.map is null)return 0;
        var targets=RegionTargets(location,tiles,floor);if(targets.Count==0)return 0;
        touchedMaps.Add(location.map);
        string key=DesignStorage.VisualKey(snapshot),sheetId="BetterBeadsDecor"+key;
        var sheet=location.map.TileSheets.FirstOrDefault(s=>s.Id==sheetId);
        if(sheet is null)
        {
            sheet=new TileSheet(sheetId,location.map,TexturePrefix+key,new Size(16,1),new Size(16,16));
            location.map.AddTileSheet(sheet);
            location.map.LoadTileSheets(Game1.mapDisplayDevice);
        }
        int minX=(int)tiles.Min(p=>p.X),minY=(int)tiles.Min(p=>p.Y);
        int replaced=0;
        foreach(var target in targets)
        {
            var layer=target.Layer;int x=target.X,y=target.Y;
            var old=layer.Tiles[x,y];
            var tile=new StaticTile(layer,sheet,BlendMode.Alpha,DecorationPattern.Part(snapshot.Design.Views["front"].Width,x-minX,y-minY));
            tile.Properties.CopyFrom(old.TileIndexProperties);
            tile.Properties.CopyFrom(old.Properties);
            layer.Tiles[x,y]=tile;replaced++;
        }
        return replaced;
    }
    internal static List<(xTile.Layers.Layer Layer,int X,int Y)> RegionTargets(DecoratableLocation location,IEnumerable<Vector3> tiles,bool floor)
    {
        var result=new List<(xTile.Layers.Layer,int,int)>();
        var seen=new HashSet<(string,int,int)>();
        if(location.map is null)return result;
        foreach(var point in tiles)
        {
            int x=(int)point.X,y=(int)point.Y;
            string name=!floor&&(int)point.Z==2&&CanReplace(location,x,y,"Buildings")?"Buildings":"Back";
            if(!CanReplace(location,x,y,name))continue;
            var layer=location.map.GetLayer(name);
            if(seen.Add((name,x,y)))result.Add((layer,x,y));
        }
        return result;
    }
    private static bool CanReplace(DecoratableLocation location,int x,int y,string layer)
    {
        if(!tileCheckBound)
        {
            tileCheckBound=true;
            try{nativeTileCheck=AccessTools.Method(typeof(DecoratableLocation),"IsFloorableOrWallpaperableTile",
                new[]{typeof(int),typeof(int),typeof(string)})?.CreateDelegate<NativeTileCheck>();}
            catch(Exception ex){monitor?.Log("Native decoration tile validation is unavailable: "+ex.Message,LogLevel.Error);}
        }
        return nativeTileCheck?.Invoke(location,x,y,layer)==true;
    }
    private static void ReleaseUnusedPatterns()
    {
        try
        {
            var active=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var map in touchedMaps)
            foreach(var sheet in map.TileSheets.Where(s=>s.Id.StartsWith("BetterBeadsDecor",StringComparison.Ordinal)).ToArray())
                if(map.DependsOnTileSheet(sheet))active.Add(sheet.Id["BetterBeadsDecor".Length..]);
            foreach(var map in touchedMaps)
            foreach(var sheet in map.TileSheets.Where(s=>s.Id.StartsWith("BetterBeadsDecor",StringComparison.Ordinal)).ToArray())
            {
                string key=sheet.Id["BetterBeadsDecor".Length..];
                if(active.Contains(key))continue;
                map.RemoveTileSheet(sheet);
                Game1.mapDisplayDevice.DisposeTileSheet(sheet);
            }
            foreach(string key in patterns.Keys.Where(key=>!active.Contains(key)).ToArray())
            {patterns.Remove(key);contentHelper.GameContent.InvalidateCache(TexturePrefix+key);}
        }
        catch(Exception ex){monitor?.Log("Unused decoration texture cleanup will retry later: "+ex.Message,LogLevel.Warn);}
    }
    private static void RefreshArt()
    {
        try
        {
            foreach(var map in touchedMaps)
            foreach(var sheet in map.TileSheets.Where(s=>s.Id.StartsWith("BetterBeadsDecor",StringComparison.Ordinal)))
                Game1.mapDisplayDevice.DisposeTileSheet(sheet);
            foreach(var key in patterns.Keys)contentHelper.GameContent.InvalidateCache(TexturePrefix+key);
            foreach(var map in touchedMaps)map.LoadTileSheets(Game1.mapDisplayDevice);
        }
        catch(Exception ex){monitor.Log("Decoration art reload will retry on the next update: "+ex.Message,LogLevel.Warn);lastArtRevision=-1;}
    }
}
#endif
