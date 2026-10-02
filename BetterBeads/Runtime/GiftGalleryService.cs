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

namespace BetterBeads.Runtime;

internal sealed class GiftGalleryService
{
    private sealed record Placement(string Villager,ProductSnapshot Painting,Rectangle WorldBounds);
    private static GiftGalleryService? current;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly ProductItems products;
    private readonly Func<SaveProgress?> progress;
    private readonly GiftGallerySettings settings;
    private GameLocation? placedLocation;
    private List<Placement> placements=new();
    private readonly HashSet<string> shownUnavailable=new();
    private readonly Dictionary<GameLocation,List<Rectangle>> siteCache=new();
    private bool bypass;
    private bool received;
    private NPC? giftingTo;
    private Item? giftingItem;

    public GiftGalleryService(IModHelper helper,IMonitor monitor,ProductItems products,Func<SaveProgress?> progress)
    {
        this.helper=helper;this.monitor=monitor;this.products=products;this.progress=progress;
        try{settings=helper.Data.ReadJsonFile<GiftGallerySettings>("assets/gift-gallery.json")??new();}
        catch(Exception ex){monitor.Log("Gift gallery positions could not be read: "+ex.Message,LogLevel.Warn);settings=new();}
        if(!settings.IsValid)monitor.Log("Gift gallery positions are invalid; home display is disabled.",LogLevel.Warn);
        current=this;
        var harmony=new Harmony("xinzh.BetterBeads.GiftGallery");
        harmony.Patch(AccessTools.Method(typeof(Furniture),nameof(Furniture.canBeGivenAsGift)),postfix:new(typeof(GiftGalleryService),nameof(CanGift)));
        harmony.Patch(AccessTools.Method(typeof(NPC),nameof(NPC.getGiftTasteForThisItem)),postfix:new(typeof(GiftGalleryService),nameof(Taste)));
        harmony.Patch(AccessTools.Method(typeof(NPC),nameof(NPC.tryToReceiveActiveObject),new[]{typeof(Farmer),typeof(bool)}),
            prefix:new(typeof(GiftGalleryService),nameof(BeforeGift)));
        harmony.Patch(AccessTools.Method(typeof(NPC),nameof(NPC.receiveGift)),postfix:new(typeof(GiftGalleryService),nameof(AfterNativeGift)));
        helper.Events.Display.RenderedWorld+=Draw;
        helper.Events.Input.ButtonPressed+=Inspect;
        helper.Events.Player.Warped+=(_,_)=>Invalidate();
        helper.Events.World.FurnitureListChanged+=(_,_)=>Invalidate();
    }

    private ProductSnapshot? Painting(Item? item)
    {
        var snapshot=products.Read(item);
        return GiftGallery.Eligible(snapshot)?snapshot:null;
    }
    private static void CanGift(Furniture __instance,ref bool __result)
    {if(current?.Painting(__instance) is not null)__result=true;}
    private static void Taste(NPC __instance,Item item,ref int __result)
    {if(current?.Painting(item) is not null)__result=2;}
    private static bool BeforeGift(NPC __instance,Farmer who,bool probe,ref bool __result)
    {
        var service=current;
        if(service is null||service.bypass||probe||!who.IsLocalPlayer||Game1.eventUp
            ||Game1.activeClickableMenu is not null||who.ActiveObject is not {} item
            ||service.Painting(item) is not {} painting)return true;
        // Let the native game reject gifts that cannot be received today.
        if(!__instance.tryToReceiveActiveObject(who,true))return true;
        Game1.activeClickableMenu=new GiftPaintingMenu(__instance,who,item,painting,service.Confirm);
        __result=true;return false;
    }
    private static void AfterNativeGift(NPC __instance,StardewValley.Object o,Farmer giver)
    {
        var service=current;
        if(service is not null&&service.bypass&&ReferenceEquals(service.giftingTo,__instance)
            &&ReferenceEquals(service.giftingItem,o)&&giver.IsLocalPlayer)service.received=true;
    }
    private void Confirm(NPC npc,Farmer giver,Item item,ProductSnapshot snapshot)
    {
        if(!Context.IsWorldReady||progress() is not {} data
            ||!ReferenceEquals(giver.ActiveObject,item)||!ReferenceEquals(products.Read(item),products.Read(giver.ActiveObject))
            ||Painting(item)?.InstanceId!=snapshot.InstanceId)return;
        if(OnlineSession.Current?.CanRecordGift==false){Game1.showRedMessage(OnlineSession.Text("data"));return;}
        bypass=true;received=false;giftingTo=npc;giftingItem=item;
        try
        {
            npc.tryToReceiveActiveObject(giver,false);
            if(received&&!ReferenceEquals(giver.ActiveObject,item))
            {
                OnlineSession.Current?.Gift(npc.Name,snapshot);
                var site=CurrentSite(npc,snapshot);
                string key=GiftFeedback.Key(site,giver.getFriendshipHeartLevelForNPC(npc.Name));
                string next=ContentText.Get(key,key switch{
                    "gift.display-hearts"=>"画作已记录，达到6心后的次日才会展示。",
                    "gift.display-unsupported"=>"当前住宅暂不适配展示，赠画记录仍会保留。",
                    "gift.display-unavailable"=>"当前挂点不可用；记录已保留，请检查住宅布局。",
                    _=>"计划次日在当前住宅展示；挂点需保持可用。"});
                Game1.addHUDMessage(new HUDMessage(ContentText.Format("gift.received",$"{npc.displayName}收下了你的画作。")+" "+next));
            }
        }
        catch(Exception ex){monitor.Log("Bead painting gift failed: "+ex,LogLevel.Error);}
        finally{bypass=false;received=false;giftingTo=null;giftingItem=null;}
    }
    public void Invalidate(){placedLocation=null;placements.Clear();siteCache.Clear();}
    public void Clear(){Invalidate();shownUnavailable.Clear();bypass=false;received=false;giftingTo=null;giftingItem=null;}

    private void EnsurePlaced(GameLocation location)
    {
        if(ReferenceEquals(placedLocation,location))return;
        placedLocation=location;placements=new();
        if(!settings.IsValid||progress() is not {} data)return;
        try
        {
            var home=settings.Homes.FirstOrDefault(h=>h.Location==location.NameOrUniqueName);
            IEnumerable<string> names=location is FarmHouse house && !string.IsNullOrWhiteSpace(Game1.GetPlayer(house.OwnerId)?.spouse)
                ?new[]{Game1.GetPlayer(house.OwnerId)!.spouse}:(IEnumerable<string>?)home?.Villagers??Array.Empty<string>();
            foreach(string name in names)
            {
                if(!data.GiftGalleryRecords.TryGetValue(name,out var record)||record.Displayed is not {} painting)continue;
                var state=FindSite(location,name,painting,out var bounds);
                if(state==GallerySiteState.Ready)placements.Add(new(name,painting,bounds));
                else if(state==GallerySiteState.Unavailable&&shownUnavailable.Add(location.NameOrUniqueName+":"+name+":"+painting.InstanceId))
                {
                    monitor.Log("Gift gallery has no usable wall slot: "+location.NameOrUniqueName+" / "+name,LogLevel.Warn);
                    Game1.addHUDMessage(new HUDMessage(ContentText.Format("gift.display-blocked",$"{Game1.getCharacterFromName(name)?.displayName??name}的挂画暂未显示：住宅挂点不可用。")));
                }
            }
        }
        catch(Exception ex){placements.Clear();monitor.Log("Gift gallery skipped an incompatible home map: "+ex.Message,LogLevel.Warn);}
    }

    private GallerySiteState CurrentSite(NPC npc,ProductSnapshot painting)
    {
        try
        {
            var spouse=Game1.getAllFarmers().FirstOrDefault(f=>f.spouse==npc.Name);
            var home=spouse is not null?Utility.getHomeOfFarmer(spouse):Game1.getLocationFromName(npc.DefaultMap);
            return home is null?GallerySiteState.Unsupported:FindSite(home,npc.Name,painting,out _);
        }
        catch{return GallerySiteState.Unavailable;}
    }
    private GallerySiteState FindSite(GameLocation location,string name,ProductSnapshot painting,out Rectangle bounds)
    {
        bounds=Rectangle.Empty;
        if(!settings.IsValid||location is not DecoratableLocation decorated)return GallerySiteState.Unsupported;
        var home=settings.Homes.FirstOrDefault(h=>h.Location==location.NameOrUniqueName);
        bool spouse=location is FarmHouse house&&Game1.GetPlayer(house.OwnerId)?.spouse==name;
        int index=spouse?0:home?.Villagers.IndexOf(name)??-1;
        if(index<0||!spouse&&(Game1.getAllFarmers().Any(f=>f.spouse==name)
            ||Game1.getCharacterFromName(name)?.DefaultMap!=location.Name))return GallerySiteState.Unsupported;
        if(!spouse)index=home!.WallSlots.GetValueOrDefault(name,index);
        if(!siteCache.TryGetValue(location,out var slots))
        {
        slots=new List<Rectangle>();
        int width=location.Map?.Layers.FirstOrDefault()?.LayerWidth??0,height=location.Map?.Layers.FirstOrDefault()?.LayerHeight??0;
        for(int y=0;y<height-2;y++)for(int x=0;x<width-3;x++)
        {
            if(decorated.GetWallTopY(x,y)!=y||!Enumerable.Range(x,3).All(px=>decorated.isTileOnWall(px,y)&&decorated.isTileOnWall(px,y+1)))continue;
            var candidate=new Rectangle(x*64,y*64,3*64,3*64);
            if(slots.Any(s=>s.Intersects(candidate)))continue;
            slots.Add(candidate);
        }
        if(location is FarmHouse farmHouse)
        {
            var corner=farmHouse.GetSpouseRoomCorner();
            slots=slots.Where(s=>s.X/64>=corner.X&&s.X/64<corner.X+7&&s.Y/64>=corner.Y&&s.Y/64<corner.Y+6).ToList();
        }
        siteCache[location]=slots;
        }
        if(index>=slots.Count||location.furniture.Any(f=>f.GetBoundingBox().Intersects(slots[index])))return GallerySiteState.Unavailable;
        var slot=slots[index];int pixels=painting.Design.Views["front"].Width+(FurnitureFinish.IsNew(painting.FurnitureVariantId)?0:16);
        bounds=new Rectangle(slot.X+(slot.Width-pixels*4)/2,slot.Y,pixels*4,pixels*4);
        return GallerySiteState.Ready;
    }
    private void Draw(object? sender,RenderedWorldEventArgs e)
    {
        if(!Context.IsWorldReady||Game1.eventUp||Game1.currentLocation is not {} location)return;
        EnsurePlaced(location);
        foreach(var p in placements)
        {
            var preview=ArtResources.SceneTexture(p.Painting);
            var point=Game1.GlobalToLocal(Game1.viewport,new Vector2(p.WorldBounds.X,p.WorldBounds.Y));
            e.SpriteBatch.Draw(preview.Texture,new Rectangle((int)point.X,(int)point.Y,p.WorldBounds.Width,p.WorldBounds.Height),preview.Source,Color.White);
        }
    }
    private void Inspect(object? sender,ButtonPressedEventArgs e)
    {
        if(!Context.IsWorldReady||Game1.eventUp||Game1.activeClickableMenu is not null
            ||e.Button is not (SButton.MouseLeft or SButton.MouseRight)||Game1.currentLocation is not {} location)return;
        EnsurePlaced(location);
        var cursor=e.Cursor.AbsolutePixels;
        var painting=placements.FirstOrDefault(p=>p.WorldBounds.Contains((int)cursor.X,(int)cursor.Y));
        if(painting is null)return;
        helper.Input.Suppress(e.Button);
        Game1.drawObjectDialogue(ContentText.Format("gift.inspect",$"「{painting.Painting.Design.Name}」——赠自{progress()?.GiftGalleryRecords.GetValueOrDefault(painting.Villager)?.Giver ?? "?"}"));
    }
}
#endif
