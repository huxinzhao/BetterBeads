#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace BetterBeads.Runtime;

internal sealed class OnlinePacket
{
    public string Protocol {get;set;}=OnlineRules.Protocol;
    public string Kind {get;set;}="";
    public string Request {get;set;}="";
    public string Token {get;set;}="";
    public string Location {get;set;}="";
    public int X {get;set;}
    public int Y {get;set;}
    public string Error {get;set;}="";
    public Blueprint? Design {get;set;}
    public SaveProgress? Progress {get;set;}
    public ProductSnapshot? Product {get;set;}
    public Dictionary<string,GiftGalleryRecord>? Gallery {get;set;}
    public string Npc {get;set;}="";
    public string[]? Backpack {get;set;}
    public Dictionary<int,string>? ChangedSlots {get;set;}
    public bool Creative {get;set;}
}

/// <summary>Main-thread host authority. Messages never request human approval.</summary>
internal sealed class OnlineSession
{
    private sealed class Lease
    {
        public long Player; public string Token=""; public StardewValley.Object Bench=null!;
        public GameLocation Location=null!; public double Seen;
    }
    private sealed class CraftWork
    {
        public long Player; public OnlinePacket Request=null!; public InventoryService Inventory=null!;
        public Lease Lease=null!; public Queue<Chest> Remaining=new(); public HashSet<Chest> Held=new();
        public double Started; public bool Done;
    }
    public static OnlineSession? Current {get;private set;}
    public static bool Busy=>Context.IsMultiplayer&&Current?.pending is not null;
    private const string MessageType="Online/v1",ModId="xinzh.BetterBeads";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly SaveSession session;
    private readonly ProcessingCatalog catalog;
    private readonly ProductItems products;
    private readonly Action<StardewValley.Object> open;
    private readonly Action ready;
    private readonly Dictionary<long,Lease> leases=new();
    private readonly HashSet<long> peers=new();
    private readonly Dictionary<(long,string),OnlinePacket> receipts=new();
    private readonly List<CraftWork> work=new();
    private readonly Dictionary<Chest,CraftWork> reservations=new();
    private readonly Dictionary<long,byte[]?> inventoryBroadcasts=new();
    private readonly List<(long Player,OnlinePacket Packet,double Started)> incomingGifts=new();
    private bool? lastCreative;
    private OnlinePacket? delivered;
    private OnlinePacket? pending;
    private Action<OnlinePacket>? completion;
    private double sent,hello;
    private string token="";
    private bool connected;
    private long personalVersion,syncedVersion;
    public static void MarkPersonalChanged(){if(Current is {} c)c.personalVersion++;}
    private StardewValley.Object? openingBench;
    public SaveProgress Gallery {get;}=new();
    public event Action? GalleryChanged;
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;

    public OnlineSession(IModHelper helper,IMonitor monitor,SaveSession session,ProcessingCatalog catalog,ProductItems products,
        Action<StardewValley.Object> open,Action ready)
    {
        this.helper=helper;this.monitor=monitor;this.session=session;this.catalog=catalog;this.products=products;this.open=open;this.ready=ready;
        Current=this;
        helper.Events.Multiplayer.ModMessageReceived+=Message;
        helper.Events.Multiplayer.PeerDisconnected+=(_,e)=>{leases.Remove(e.Peer.PlayerID);peers.Remove(e.Peer.PlayerID);};
        helper.Events.GameLoop.UpdateTicked+=Tick;
        helper.Events.Display.MenuChanged+=(_,e)=>{if(e.OldMenu is WorkbenchMenu&&e.NewMenu is not WorkbenchMenu)Release();};
        helper.Events.Player.Warped+=(_,e)=>{if(e.IsLocalPlayer){Release();if(Game1.activeClickableMenu is WorkbenchMenu)Game1.exitActiveMenu();}};
        helper.Events.GameLoop.ReturnedToTitle+=(_,_)=>Clear();
    }
    private void Send(OnlinePacket p,long player)
    {
        if(player==Game1.player.UniqueMultiplayerID)Receive(Game1.player.UniqueMultiplayerID,p);
        else helper.Multiplayer.SendMessage(p,MessageType,new[]{ModId},new[]{player});
    }
    private void ToHost(OnlinePacket p)=>Send(p,Game1.MasterPlayer.UniqueMultiplayerID);
    private bool CompatiblePeers()=>helper.Multiplayer.GetConnectedPlayers().All(p=>peers.Contains(p.PlayerID));
    private void Message(object? sender,ModMessageReceivedEventArgs e)
    {
        if(e.FromModID!=ModId||e.Type!=MessageType||!Context.IsWorldReady)return;
        try{Receive(e.FromPlayerID,e.ReadAs<OnlinePacket>());}
        catch(Exception ex){monitor.Log("Rejected online message: "+ex.Message,LogLevel.Warn);}
    }
    public void Open(StardewValley.Object bench)
    {
        if(Context.IsSplitScreen){Game1.showRedMessage(Text("split"));return;}
        if(!session.OnlineReadable||!Context.IsMainPlayer&&!connected){Game1.showRedMessage(Text("version"));return;}
        if(pending is not null)return;
        openingBench=bench;
        Begin(new(){Kind="open",Location=Game1.currentLocation.NameOrUniqueName,X=(int)bench.TileLocation.X,Y=(int)bench.TileLocation.Y},p=>
        {
            if(p.Error.Length>0){Game1.showRedMessage(Text(p.Error));return;}
            token=p.Token;
            if(openingBench is {} current&&Game1.currentLocation.Objects.TryGetValue(current.TileLocation,out var placed)
                &&ReferenceEquals(current,placed)&&Game1.activeClickableMenu is null)open(current);
            else Release();
            openingBench=null;
        });
    }
    public void Craft(Blueprint design,Action<ManufacturingResult> done)
    {
        if(pending is not null)return;
        Begin(new(){Kind="craft",Token=token,Design=design.Copy(),Backpack=Bag(Game1.player),Creative=PlayMode.Creative},p=>
        {
            if(p.Progress is not null&&session.Progress is {} local)local.CollectorLetters=p.Progress.CollectorLetters;
            done(new(p.Error.Length==0,p.Error.Length==0?"manufacturing.success":p.Error,p.Product));
        });
    }
    public void Sync(Action<bool>? done=null)
    {
        if(!Context.IsMultiplayer||Context.IsMainPlayer){done?.Invoke(true);return;}
        if(!connected||pending is not null||session.Progress is not {} p){done?.Invoke(false);return;}
        long version=personalVersion;
        Begin(new(){Kind="personal",Progress=Copy(p)},reply=>
        {if(reply.Error.Length==0){syncedVersion=version;}done?.Invoke(reply.Error.Length==0);});
    }
    private void Begin(OnlinePacket p,Action<OnlinePacket> done)
    {p.Request=Guid.NewGuid().ToString("N");pending=p;completion=done;sent=Now;ToHost(p);}
    private static SaveProgress Copy(SaveProgress p)=>System.Text.Json.JsonSerializer.Deserialize<SaveProgress>(DesignStorage.Serialize(p))!;
    private void Release()
    {
        if(!Context.IsWorldReady||!Context.IsMultiplayer)return;
        if(token.Length>0)ToHost(new(){Kind="release",Token=token});
        token="";
        Sync();
    }
    private static string ItemKey(Item? item)=>item is null?"":DesignStorage.Serialize(new{item.QualifiedItemId,item.Stack,item.Quality,Data=item.modData.Pairs.OrderBy(p=>p.Key,StringComparer.Ordinal).ToArray()});
    private static string[] Bag(Farmer farmer)=>Enumerable.Range(0,farmer.MaxItems).Select(i=>ItemKey(i<farmer.Items.Count?farmer.Items[i]:null)).ToArray();
    private void Reply(long player,OnlinePacket request,string error="",ProductSnapshot? product=null,string lease="")
    {
        var response=new OnlinePacket{Kind="reply",Request=request.Request,Error=error,Product=product,Token=lease};
        // Remember the outcome before preparing presentation/sync metadata or invoking transport.
        receipts[(player,request.Request)]=response;
        if(product is not null)response.Progress=new(){CollectorLetters=session.ForPlayer(player).CollectorLetters};
        if(product is not null&&request.Backpack is {} before&&Game1.GetPlayer(player) is {} farmer)
            response.ChangedSlots=Bag(farmer).Select((value,index)=>(value,index)).Where(v=>v.index>=before.Length||before[v.index]!=v.value)
                .ToDictionary(v=>v.index,v=>v.value);
        Send(response,player);
    }
    private void Receive(long sender,OnlinePacket p)
    {
        if(p.Protocol!=OnlineRules.Protocol)
        {
            if(!Context.IsMainPlayer&&sender==Game1.MasterPlayer.UniqueMultiplayerID){connected=false;Game1.showRedMessage(Text("version"));}
            return;
        }
        if(p.Kind is "welcome" or "reply" or "gallery" or "settings")
        {
            if(sender!=Game1.MasterPlayer.UniqueMultiplayerID)return;
            if(p.Kind is "welcome" or "settings")PlayMode.RemoteCreative=p.Creative;
            if(p.Kind=="welcome"&&p.Progress is not null&&!Context.IsMainPlayer)
            {
                if(connected)return;
                session.Accept(p.Progress);connected=true;ready();
            }
            if(p.Gallery is not null){Gallery.GiftGalleryRecords=p.Gallery;GalleryChanged?.Invoke();}
            if(p.Kind=="reply"&&pending?.Request==p.Request)
            {
                // SMAPI's reply can arrive before the game's inventory delta. Keep input locked until both arrive.
                delivered=p;CompleteReply();
            }
            return;
        }
        if(!Context.IsMainPlayer||session.Progress is null||!session.OnlineReadable)return;
        var farmer=Game1.GetPlayer(sender,onlyOnline:true);
        if(farmer is null)return;
        if(p.Kind=="hello")
        {peers.Add(sender);Send(new(){Kind="welcome",Progress=Copy(session.ForPlayer(sender)),Gallery=GalleryRecords(),Creative=PlayMode.Creative},sender);return;}
        if(sender!=Game1.player.UniqueMultiplayerID&&!peers.Contains(sender))return;
        if(p.Kind=="heartbeat"){if(leases.TryGetValue(sender,out var l)&&l.Token==p.Token)l.Seen=Now;return;}
        if(p.Kind=="release"){if(leases.TryGetValue(sender,out var l)&&l.Token==p.Token)leases.Remove(sender);return;}
        if(p.Request.Length is <1 or >64)return;
        if(receipts.TryGetValue((sender,p.Request),out var receipt)){Send(receipt,sender);return;}
        if(p.Kind=="open")
        {
            if(!CompatiblePeers()){Reply(sender,p,"version");return;}
            var location=farmer.currentLocation;var tile=new Vector2(p.X,p.Y);
            if(location?.NameOrUniqueName!=p.Location||!location.Objects.TryGetValue(tile,out var bench)
                ||bench.QualifiedItemId!="(BC)"+WorkbenchContent.Id||Vector2.Distance(farmer.Tile,tile)>3)
            {Reply(sender,p,"session");return;}
            if(leases.Values.Any(l=>ReferenceEquals(l.Bench,bench)&&l.Player!=sender)) {Reply(sender,p,"occupied");return;}
            var lease=new Lease{Player=sender,Bench=bench,Location=location,Token=Guid.NewGuid().ToString("N"),Seen=Now};
            leases[sender]=lease;Reply(sender,p,lease:lease.Token);return;
        }
        if(p.Kind=="personal")
        {
            if(p.Progress is null||!OnlineRules.ValidPersonal(p.Progress)){Reply(sender,p,"data");return;}
            OnlineRules.ApplyPersonal(session.ForPlayer(sender),p.Progress);Reply(sender,p);return;
        }
        if(p.Kind=="gift")
        {
            if(p.Product is null||!GiftGallery.Eligible(p.Product)||Game1.getCharacterFromName(p.Npc) is null
                ||!farmer.friendshipData.TryGetValue(p.Npc,out var friendship)||friendship.GiftsToday<1)
            {
                if(!incomingGifts.Any(g=>g.Player==sender&&g.Packet.Request==p.Request))incomingGifts.Add((sender,p,Now));
                return;
            }
            OnlineRules.Receive(session.Online,p.Npc,sender,farmer.Name,p.Product,Game1.Date.TotalDays);
            Reply(sender,p);return;
        }
        if(p.Kind!="craft"||work.Any(w=>w.Player==sender&&!w.Done))return;
        if(!CompatiblePeers()){Reply(sender,p,"online.version");return;}
        if(!leases.TryGetValue(sender,out var active)||active.Token!=p.Token||!Valid(active)
            ||p.Design is null||!DesignStorage.IsStructurallyValid(p.Design)||!SimpleCrafting.Supported(p.Design))
        {Reply(sender,p,"manufacturing.changed");return;}
        if(p.Creative!=PlayMode.Creative){Reply(sender,p,"online.settings");return;}
        if(p.Backpack is null||!Bag(farmer).SequenceEqual(p.Backpack))
        {Reply(sender,p,"manufacturing.changed");return;}
        var inventory=new InventoryService(catalog);inventory.Bind(active.Bench,5,farmer);
        var job=new CraftWork{Player=sender,Request=p,Lease=active,Inventory=inventory,Started=Now,
            Remaining=new(inventory.AvailableChests())};
        inventory.LockedChests=job.Held;work.Add(job);NextLock(job);
    }
    private bool Valid(Lease l)=>leases.TryGetValue(l.Player,out var active)&&ReferenceEquals(active,l)
        &&Now-l.Seen<15&&Game1.GetPlayer(l.Player,onlyOnline:true) is {} f
        &&ReferenceEquals(f.currentLocation,l.Location)&&l.Location.Objects.TryGetValue(l.Bench.TileLocation,out var bench)
        &&ReferenceEquals(bench,l.Bench)&&Vector2.Distance(f.Tile,bench.TileLocation)<=4;
    private void NextLock(CraftWork job)
    {
        if(job.Done)return;
        if(!Valid(job.Lease)||Now-job.Started>10){Finish(job,new(false,"manufacturing.changed"));return;}
        if(job.Remaining.TryDequeue(out var chest))
        {
            // Never borrow a mutex already owned by an open native chest menu or another transaction.
            if(chest.GetMutex().IsLocked()||reservations.ContainsKey(chest)){Finish(job,new(false,"manufacturing.changed"));return;}
            reservations[chest]=job;
            chest.GetMutex().RequestLock(()=>
            {
                if(job.Done){chest.GetMutex().ReleaseLock();reservations.Remove(chest);return;}
                job.Held.Add(chest);NextLock(job);
            },()=>{reservations.Remove(chest);Finish(job,new(false,"manufacturing.changed"));});
            chest.GetMutex().Update(job.Lease.Location);
            return;
        }
        try
        {
            var service=new ManufacturingService(job.Inventory,products,monitor,SimpleCrafting.Recipes());
            var design=job.Request.Design!;var recipe=service.FindRecipe(design.TemplateId);
            var progress=session.ForPlayer(job.Player);
            if(!Bag(job.Inventory.Owner).SequenceEqual(job.Request.Backpack!))
            {Finish(job,new(false,"manufacturing.changed"));return;}
            var preview=recipe is null?null:service.Preview(design,recipe,progress,job.Request.Request);
            var result=preview?.Plan is {} plan?service.SubmitResult(plan,recipe!,progress,session.Progress)
                :new ManufacturingResult(false,"manufacturing.error."+(preview?.Failure??ManufacturingFailure.InvalidInventory));
            Finish(job,result);
        }
        catch(Exception ex){monitor.Log("Online craft failed: "+ex,LogLevel.Error);Finish(job,new(false,"manufacturing.failed"));}
    }
    private void Finish(CraftWork job,ManufacturingResult result)
    {
        if(job.Done)return;
        job.Done=true;
        foreach(var chest in job.Held){chest.GetMutex().ReleaseLock();reservations.Remove(chest);}
        // Native broadcastFarmerDeltas sends only the local farmer. Explicitly publish the remote
        // creator's dirty root after a host commit, using the game's own delta codec and transport.
        if(result.Success&&job.Player!=Game1.player.UniqueMultiplayerID)inventoryBroadcasts.TryAdd(job.Player,null);
        Reply(job.Player,job.Request,result.Success?"":result.Message,result.Product);
        FlushInventories();
    }
    public void Gift(string npc,ProductSnapshot painting)
    {
        // Native receiving has already consumed the local item and applied native friendship exactly once.
        if(Context.IsMainPlayer){OnlineRules.Receive(session.Online,npc,Game1.player.UniqueMultiplayerID,Game1.player.Name,painting,Game1.Date.TotalDays);return;}
        if(pending is not null){monitor.Log("Gallery recording deferred while an online request is pending.",LogLevel.Warn);queuedGifts.Enqueue((npc,painting.Copy()));return;}
        Begin(new(){Kind="gift",Npc=npc,Product=painting.Copy()},p=>{if(p.Error.Length>0)monitor.Log("Gallery gift rejected: "+p.Error,LogLevel.Warn);});
    }
    private readonly Queue<(string Npc,ProductSnapshot Painting)> queuedGifts=new();
    public void DayStarted()
    {
        if(!Context.IsMainPlayer||session.Progress is null||!session.OnlineReadable)return;
        // Seed old single-player displays without changing the existing artwork or its physical version.
        foreach(var pair in session.Progress.GiftGalleryRecords)
        {
            if(session.Online.Gallery.ContainsKey(pair.Key))continue;
            var record=pair.Value;var slot=new OnlineGallerySlot();session.Online.Gallery[pair.Key]=slot;
            if(record.Displayed is {} old)slot.Displayed=new(){FarmerId=Game1.MasterPlayer.UniqueMultiplayerID,
                Painting=old.Copy(),Giver=record.Giver,Day=Math.Max(0,record.DisplayDay),Sequence=++session.Online.GiftSequence};
            if(record.Latest is {} latest)OnlineRules.Receive(session.Online,pair.Key,Game1.MasterPlayer.UniqueMultiplayerID,record.Giver,latest,Math.Max(0,record.GiftDay));
        }
        foreach(var pair in session.Online.Gallery)
            OnlineRules.Advance(pair.Value,Game1.Date.TotalDays,id=>Game1.GetPlayer(id)?.getFriendshipHeartLevelForNPC(pair.Key)??0);
        Gallery.GiftGalleryRecords=GalleryRecords();GalleryChanged?.Invoke();
        foreach(long peer in peers)Send(new(){Kind="gallery",Gallery=GalleryRecords()},peer);
    }
    private Dictionary<string,GiftGalleryRecord> GalleryRecords()=>session.Online.Gallery
        .Where(p=>p.Value.Displayed is not null).ToDictionary(p=>p.Key,p=>new GiftGalleryRecord
        {Displayed=p.Value.Displayed!.Painting.Copy(),Giver=p.Value.Displayed.Giver,DisplayDay=p.Value.Displayed.Day});
    private void Tick(object? sender,UpdateTickedEventArgs e)
    {
        if(!Context.IsWorldReady||!Context.IsMultiplayer)return;
        CompleteReply();
        if(!Context.IsMainPlayer&&!connected&&Now-hello>3){hello=Now;ToHost(new(){Kind="hello"});}
        if(pending is not null&&Now-sent>3){sent=Now;ToHost(pending);}
        if(e.IsMultipleOf(60))
        {
            if(token.Length>0)ToHost(new(){Kind="heartbeat",Token=token});
            if(Context.IsMainPlayer)
            {
                FlushInventories();
                if(lastCreative!=PlayMode.Creative)
                {lastCreative=PlayMode.Creative;foreach(long peer in peers)Send(new(){Kind="settings",Creative=PlayMode.Creative},peer);}
                foreach(var gift in incomingGifts.ToArray())
                {
                    if(Game1.GetPlayer(gift.Player,onlyOnline:true) is {} giver&&gift.Packet.Product is {} painting
                        &&GiftGallery.Eligible(painting)&&Game1.getCharacterFromName(gift.Packet.Npc) is not null
                        &&giver.friendshipData.TryGetValue(gift.Packet.Npc,out var friendship)&&friendship.GiftsToday>0)
                    {
                        OnlineRules.Receive(session.Online,gift.Packet.Npc,gift.Player,giver.Name,painting,Game1.Date.TotalDays);
                        Reply(gift.Player,gift.Packet);incomingGifts.Remove(gift);
                    }
                    else if(Now-gift.Started>5){Reply(gift.Player,gift.Packet,"data");incomingGifts.Remove(gift);}
                }
                foreach(var id in leases.Where(p=>!Valid(p.Value)).Select(p=>p.Key).ToArray())leases.Remove(id);
                foreach(var job in work.Where(j=>!j.Done&&(!Valid(j.Lease)||Now-j.Started>10)).ToArray())Finish(job,new(false,"manufacturing.changed"));
                work.RemoveAll(j=>j.Done);
            }
            else if(pending is null&&connected&&session.Progress is not null&&personalVersion!=syncedVersion)Sync();
        }
        if(pending is null&&queuedGifts.TryDequeue(out var queuedGift))Gift(queuedGift.Npc,queuedGift.Painting);
    }
    public void Clear()
    {
        foreach(var job in work)foreach(var chest in job.Held)if(chest.GetMutex().IsLockHeld())chest.GetMutex().ReleaseLock();
        work.Clear();reservations.Clear();inventoryBroadcasts.Clear();incomingGifts.Clear();lastCreative=null;PlayMode.RemoteCreative=null;
        leases.Clear();peers.Clear();receipts.Clear();queuedGifts.Clear();pending=null;completion=null;delivered=null;
        token="";connected=false;personalVersion=syncedVersion=0;hello=0;Gallery.GiftGalleryRecords.Clear();GalleryChanged?.Invoke();
    }
    public static string Text(string key)=>ContentText.Get("online."+key,key);
    private void CompleteReply()
    {
        if(delivered is not {} p)return;
        if(p.Product is {} product&&!Game1.player.Items.Any(item=>products.Read(item)?.InstanceId==product.InstanceId))return;
        if(p.ChangedSlots is {} changes&&changes.Any(pair=>pair.Key>=Game1.player.MaxItems
            ||ItemKey(pair.Key<Game1.player.Items.Count?Game1.player.Items[pair.Key]:null)!=pair.Value))return;
        var action=completion;pending=null;completion=null;delivered=null;action?.Invoke(p);
    }
    private void FlushInventories()
    {
        foreach(long id in inventoryBroadcasts.Keys.ToArray())
        {
            try
            {
                var multiplayer=(StardewValley.Multiplayer)HarmonyLib.AccessTools.Field(typeof(Game1),"multiplayer").GetValue(null)!;
                var root=multiplayer.farmerRoot(id);
                if(root is null)continue;
                var delta=inventoryBroadcasts[id]??multiplayer.writeObjectDeltaBytes(root);
                inventoryBroadcasts[id]=delta; // Retain the encoded delta if transmission throws; never repeat the craft.
                helper.Reflection.GetMethod(multiplayer,"broadcastFarmerDelta").Invoke(root.Value,delta);
                inventoryBroadcasts.Remove(id);
            }
            catch(Exception ex){monitor.Log("Inventory delivery will retry without repeating the craft: "+ex.Message,LogLevel.Warn);}
        }
    }
}
#endif
