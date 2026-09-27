using BetterBeads.Data;
using StardewModdingAPI;

namespace BetterBeads.Runtime;

internal sealed class SaveSession
{
#if BEADS_LITE
    // Keep the historical key so 0.16.x Lite saves upgrade without moving or rewriting save data.
    private const string SaveKey = "progress-lite";
#else
    private const string SaveKey = "progress";
#endif
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    public SaveProgress? Progress { get; private set; }
    public BlueprintRepository? Blueprints { get; private set; }
    public bool LoadAttempted { get; private set; }
#if BEADS_LITE
    public OnlineProgress Online {get;private set;}=new();
    public bool OnlineReadable {get;private set;}=true;
    public SaveProgress ForPlayer(long id)
    {
        if(id==StardewValley.Game1.MasterPlayer.UniqueMultiplayerID)return Progress!;
        if(!Online.Players.TryGetValue(id,out var data))Online.Players[id]=data=new(){Workshop=new(),FavoriteColors=BeadPalette.Normalize(null)};
        return data;
    }
    public void Accept(SaveProgress data)
    {Progress=data;Blueprints=new(data);LoadAttempted=true;}
#endif

    public SaveSession(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public void Load()
    {
        Clear();
        LoadAttempted = true;
        if (!Context.IsMainPlayer) return;
#if !BEADS_LITE
        if(Context.IsMultiplayer)return;
#endif
        try
        {
            var stored = helper.Data.ReadSaveData<SaveProgress>(SaveKey);
            var data = stored ?? new SaveProgress { Workshop = new() };
            data.Workshop ??= WorkshopProgress.Legacy();
            if(data.Workshop.Version==1)data.Workshop.UpgradeFromPrevious(StardewValley.Game1.Date.TotalDays);
            if (data.SchemaVersion != 1 || data.UnlockedColors is null || data.ProcessedSources is null
                || data.BlueprintRecords is null || !data.Workshop.IsValid)
            {
                monitor.Log(helper.Translation.Get("data.unreadable").ToString(), LogLevel.Error);
                return;
            }
            data.FavoriteColors=BeadPalette.Normalize(data.FavoriteColors);
#if BEADS_LITE
            data.HiddenLiteTemplates ??= new();
            LibraryPreferences.Normalize(data);
            data.CollectorLetters ??= new();
            data.CollectorLetters.RemoveAll(l=>l is null || l.Works is null || l.Works.Any(w=>w is null || string.IsNullOrWhiteSpace(w.InstanceId)));
            if(data.ArtworkValuationSequence<0)data.ArtworkValuationSequence=0;
            data.GiftGalleryRecords ??= new();
            foreach(var key in data.GiftGalleryRecords.Where(pair=>string.IsNullOrWhiteSpace(pair.Key)||pair.Value is null
                || pair.Value.Latest is not null&&!GiftGallery.Eligible(pair.Value.Latest)
                || pair.Value.Displayed is not null&&!GiftGallery.Eligible(pair.Value.Displayed)).Select(pair=>pair.Key).ToArray())
                data.GiftGalleryRecords.Remove(key);
#endif
            Progress = data;
            Blueprints = new(data);
#if BEADS_LITE
            try
            {
                Online=helper.Data.ReadSaveData<OnlineProgress>("online-progress-v1")??new();
                if(!OnlineRules.ValidWorld(Online))throw new InvalidDataException("Invalid online progress");
                foreach(var personal in Online.Players.Values)
                {
                    personal.FavoriteColors=BeadPalette.Normalize(personal.FavoriteColors);
                    if(!OnlineRules.ValidPersonal(personal))throw new InvalidDataException("Invalid online personal progress");
                    personal.Workshop??=new();
                }
            }
            catch(Exception ex){OnlineReadable=false;monitor.Log("Online progress cannot be read; multiplayer editing is disabled. "+ex,LogLevel.Error);}
#endif
        }
        catch (Exception ex)
        {
            // Never replace data we could not read with an empty save.
            monitor.Log(helper.Translation.Get("data.unreadable") + "\n" + ex, LogLevel.Error);
        }
    }

    public void Save()
    {
        if (Progress is null || !Context.IsMainPlayer) return;
#if !BEADS_LITE
        if(Context.IsMultiplayer)return;
#endif
        helper.Data.WriteSaveData(SaveKey, Progress);
#if BEADS_LITE
        if(OnlineReadable)helper.Data.WriteSaveData("online-progress-v1",Online);
#endif
    }

    public void Clear()
    {
        Blueprints = null;
        Progress = null;
        LoadAttempted = false;
#if BEADS_LITE
        Online=new();OnlineReadable=true;
#endif
    }
}
