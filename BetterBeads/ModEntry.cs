using StardewModdingAPI;
using BetterBeads.Runtime;
using BetterBeads.Data;
using StardewValley;

namespace BetterBeads;

/// <summary>更好的拼豆入口。</summary>
public sealed class ModEntry : Mod
{
    public override object GetApi()=>new CircuitBridge(()=>session?.Progress,products);
    private SaveSession? session;
    private readonly TextureCache textures = new();
    private readonly ProductItems products = new();
    private InventoryService? processing;
    private ManufacturingService? manufacturing;
    private WorkbenchMenu? workbench;
    private EditorDocument? draft;
    private BeadCraftingContent? beadCrafting;
    private WorkbenchSettings settings=new();
    private WorkbenchContent? workbenchContent;
#if BEADS_LITE
    private OnlineSession? online;
    private CollectorMailContent? collectorMail;
    private GiftGalleryService? giftGallery;
    private DraftRecovery? recovery;
#endif
#if !BEADS_LITE
    private WorkshopService? workshop;
#endif

    public override void Entry(IModHelper helper)
    {
        ContentText.Resolve=key=>{var value=helper.Translation.Get(key);return value.HasValue()?value.ToString():null;};
        ContentText.InvalidFormat=key=>Monitor.Log("Invalid text format; using built-in copy: "+key,LogLevel.Warn);
        ArtResources.Register(helper,Monitor);
#if BEADS_LITE
        try
        {
            if(!MardPaletteReference.Configure(helper.Data.ReadJsonFile<MardPaletteDefinition>("assets/Mard221.json")))
                Monitor.Log("MARD 221 reference is invalid; using the bundled twenty-color fallback.",LogLevel.Warn);
        }
        catch(Exception ex)
        {
            MardPaletteReference.Configure(null);
            Monitor.Log("MARD 221 reference could not be read; using the bundled twenty-color fallback: "+ex.Message,LogLevel.Warn);
        }
#endif
        session = new(helper, Monitor);
#if BEADS_LITE
        recovery=new(helper.DirectoryPath,Monitor);
        helper.Events.GameLoop.UpdateTicked+=(_,e)=>{if(Context.IsWorldReady&&e.IsMultipleOf(60))recovery.Checkpoint(draft);};
        helper.Events.Display.MenuChanged+=(_,e)=>{if(e.OldMenu is WorkbenchMenu)recovery.Checkpoint(draft);};
#endif
        FurnitureTemplates.Configure(DefaultManufacturing.Furniture().Concat(SimpleCrafting.Frames()).Concat(SimpleCrafting.Ornaments()).Concat(FurnitureFinish.Definitions()));
        ClothingSettings clothing;
        try{clothing=helper.Data.ReadJsonFile<ClothingSettings>("assets/clothing.json")??new();}
        catch(Exception ex){Monitor.Log(ContentText.Get("copy.ModEntry.15c24010a9","服装配置无法读取，保留文件并使用默认值：")+ex.Message,LogLevel.Warn);clothing=new();}
        if(!clothing.IsValid){Monitor.Log(ContentText.Get("copy.ModEntry.3c7101906f","服装预算无效，使用默认值。"),LogLevel.Warn);clothing=new();}
#if !BEADS_LITE
        if(GameplayBalance.UpgradeClothing(clothing))
        {
            helper.Data.WriteJsonFile("assets/clothing.json",clothing);
            Monitor.Log(ContentText.Get("copy.ModEntry.d42d13e93b","服装预算已更新为0.16默认值，自定义预算保留。"),LogLevel.Info);
        }
#endif
        ClothingTemplates.Configure(clothing);
        WeaponTemplates.Configure(DefaultWeapons.Templates());
#if !BEADS_LITE
        ReferencePatternContent.Register(helper,Monitor);
#endif
        MenuPause.Register();
        try
        {
#if BEADS_LITE
            var catalog = SimpleCrafting.Catalog();
#else
            var catalog = helper.Data.ReadJsonFile<ProcessingCatalog>("processing.json");
            if (catalog is null)
            {
                if (File.Exists(Path.Combine(helper.DirectoryPath, "processing.json")))
                    throw new InvalidDataException("Existing processing.json could not be read.");
                catalog = DefaultProcessing.Create();
                helper.Data.WriteJsonFile("processing.json", catalog);
            }
            var issues = catalog.Validate();
            if (issues.Count > 0) throw new InvalidDataException(string.Join(", ", issues));
            if(DefaultWeapons.UpgradeLegacy(catalog) is {} upgraded)
            {
                string backup="processing-before-weapons-"+Guid.NewGuid().ToString("N")+".json";
                helper.Data.WriteJsonFile(backup,catalog);
                helper.Data.WriteJsonFile("processing.json",upgraded);
                catalog=upgraded;
                Monitor.Log(helper.Translation.Get("weapon.config-upgraded",new{backup}).ToString(),LogLevel.Info);
            }
            if(DefaultEffects.UpgradeLegacy(catalog) is {} effectsCatalog)
            {
                string backup="processing-before-effects-"+Guid.NewGuid().ToString("N")+".json";
                helper.Data.WriteJsonFile(backup,catalog);
                helper.Data.WriteJsonFile("processing.json",effectsCatalog);
                catalog=effectsCatalog;
                Monitor.Log(helper.Translation.Get("effect.config-upgraded",new{backup}).ToString(),LogLevel.Info);
            }
            if(DefaultRefinement.UpgradeLegacy(catalog) is {} refinedCatalog)
            {
                string backup="processing-before-refinement-"+Guid.NewGuid().ToString("N")+".json";
                helper.Data.WriteJsonFile(backup,catalog);
                helper.Data.WriteJsonFile("processing.json",refinedCatalog);
                catalog=refinedCatalog;
                Monitor.Log(helper.Translation.Get("refinement.config-upgraded",new{backup}).ToString(),LogLevel.Info);
            }
            if(GameplayBalance.Upgrade(catalog) is {} balanced)
            {
                helper.Data.WriteJsonFile("processing-before-016-"+Guid.NewGuid().ToString("N")+".json",catalog);
                helper.Data.WriteJsonFile("processing.json",balanced);catalog=balanced;
            }
#endif
            processing = new(catalog);
            #if BEADS_LITE
            new BeadContent(DefaultProcessing.Create(), helper.Translation).Register(helper);
#else
            new BeadContent(catalog, helper.Translation).Register(helper);
#endif
            beadCrafting=new();
            beadCrafting.Register(helper);
#if !BEADS_LITE
            new RefinementContent(catalog,Monitor).Register(helper);
#endif
            try{manufacturing = new(processing,products,Monitor,
#if BEADS_LITE
SimpleCrafting.Recipes()
#else
DefaultManufacturing.Recipes().Concat(DefaultWeapons.Recipes()).Concat(ClothingTemplates.Recipes())
#endif
);}
            catch(Exception ex){Monitor.Log(helper.Translation.Get("manufacturing.config-error")+"\n"+ex,LogLevel.Error);}
        }
        catch (Exception ex) { Monitor.Log(helper.Translation.Get("processing.config-error") + "\n" + ex, LogLevel.Error); }
        WorkbenchSettings workbenchSettings;
        try { workbenchSettings = helper.ReadConfig<WorkbenchSettings>() ?? new(); }
        catch (Exception ex)
        {
            workbenchSettings = new() { RecipePrice = null, Ingredients = new() };
            Monitor.Log(helper.Translation.Get("bench.invalid-config") + "\n" + ex, LogLevel.Error);
        }
        PlayMode.Register(helper,ModManifest,workbenchSettings);
        settings=workbenchSettings;
        if (!workbenchSettings.IsConfigured)
            Monitor.Log(helper.Translation.Get("bench.unconfigured").ToString(), LogLevel.Warn);
        workbenchContent=new WorkbenchContent(workbenchSettings, helper.Translation, OpenWorkbench);
        workbenchContent.Register(helper);
#if BEADS_LITE
        if(processing is not null)online=new OnlineSession(helper,Monitor,session,processing.Catalog,products,OpenWorkbenchCore,()=>{CreateDraft();collectorMail?.DayStarted();workbenchContent?.DayStarted();});
        collectorMail=new CollectorMailContent(helper,()=>session?.Progress);
        giftGallery=new GiftGalleryService(helper,Monitor,products,()=>online?.Gallery??session?.Progress);
        if(online is not null)online.GalleryChanged+=()=>giftGallery.Invalidate();
#endif
        helper.Events.Content.AssetRequested += new ProductContent(helper.Translation).OnAssetRequested;
        ProductPatches.Apply(ModManifest.UniqueID, products, textures,helper.Translation);
        ShapeCombat.Register(helper,Monitor,products);
#if BEADS_LITE
        DecorationPlacement.Apply(ModManifest.UniqueID,products,helper,Monitor);
#endif
#if !BEADS_LITE
        workshop=new WorkshopService(helper,Monitor,()=>session.Progress,products);
#endif
        helper.Events.Display.Rendered += (_, _) => textures.ReleaseRetired();
#if !BEADS_LITE
        helper.ConsoleCommands.Add("betterbeads_samples", helper.Translation.Get("debug.command").ToString(), (_, _) => GiveSamples());
#endif
        helper.Events.GameLoop.SaveLoaded += (_, _) => LoadSession();
        helper.Events.GameLoop.DayStarted += (_, _) =>
        {
            if (!session.LoadAttempted) LoadSession();
            workbenchContent?.DayStarted();
#if BEADS_LITE
            collectorMail?.DayStarted();
            online?.DayStarted();
#endif
#if !BEADS_LITE
            workshop?.DayStarted();
#endif
        };
        helper.Events.GameLoop.Saving += (_, _) => {beadCrafting?.Synchronize();session.Save();};
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
#if BEADS_LITE
            recovery?.Checkpoint(draft);
#endif
            workbench?.ReleasePreview();
            workbench?.ReleaseInputs();
            workbench = null;
            draft = null;
            session.Clear();
#if BEADS_LITE
            giftGallery?.Clear();
#endif
            textures.Dispose();
            products.Clear();
            WeaponLifeSteal.Clear();
            ProductPatches.Clear();
            processing?.Clear();
            manufacturing?.Clear();
        };
        Monitor.Log(helper.Translation.Get("mod.loaded").ToString(), LogLevel.Info);
    }

    private void OpenWorkbench(StardewValley.Object bench)
    {
#if BEADS_LITE
        if(Context.IsMultiplayer){online?.Open(bench);return;}
#endif
        OpenWorkbenchCore(bench);
    }
    private void OpenWorkbenchCore(StardewValley.Object bench)
    {
        beadCrafting?.Synchronize();
        if (!Context.IsWorldReady || session?.Progress is null)
        {
            Game1.showRedMessage(Helper.Translation.Get("debug.single-player").ToString());
            return;
        }
#if !BEADS_LITE
        workshop?.OpenedBench();
#endif
#if BEADS_LITE
        processing?.Bind(bench,5);
#else
        processing?.Bind(bench,settings.NearbyChestRadius);
#endif
        workbench = new WorkbenchMenu(session.Progress, Helper.Translation, processing, draft,new ReferenceReader(Monitor),manufacturing,Helper.DirectoryPath);
#if BEADS_LITE
        if(recovery?.Pending is {} pending&&draft is not null)
            workbench.OfferRecovery(pending,restore=>recovery.Resolve(draft,restore));
        else if(recovery?.ReadError==true)workbench.ShowRecoveryError();
#endif
        Game1.activeClickableMenu = workbench;
    }

    private void LoadSession()
    {
        draft = null;
        session!.Load();
        beadCrafting?.Synchronize();
#if !BEADS_LITE
        workshop?.DayStarted();
#endif
        CreateDraft();
    }
    private void CreateDraft()
    {
        if(draft is not null)return;
        if (session?.Progress is not null && processing is not null)
        {
#if BEADS_LITE
            draft=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),processing.Catalog,"front");
            recovery?.Bind(Game1.uniqueIDForThisGame,Game1.player.UniqueMultiplayerID);
#else
            draft = new EditorDocument(new Blueprint {
                Name = Helper.Translation.Get("editor.untitled").ToString(), Use = ProductUse.Picture, TemplateId = ProductTemplates.Picture,
                Views = new() { ["front"] = new() { Width=16, Height=16, Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList() } }
            },processing.Catalog,"front");
#endif
        }
    }

    private void GiveSamples()
    {
        if (!Context.IsWorldReady || session?.Progress is null)
        {
            Monitor.Log(Helper.Translation.Get("debug.single-player").ToString(), LogLevel.Warn);
            return;
        }
        if (Game1.player.freeSpotsInInventory() < 3)
        {
            Monitor.Log(Helper.Translation.Get("debug.space").ToString(), LogLevel.Warn);
            return;
        }
        var samples = new[] {
            products.Create(ProductTemplates.DebugSample(false, false, Helper.Translation.Get("debug.square").ToString())),
            products.Create(ProductTemplates.DebugSample(false, true, Helper.Translation.Get("debug.diamond").ToString())),
            products.Create(ProductTemplates.DebugSample(true, false, Helper.Translation.Get("product.hat").ToString()))
        };
        foreach (var item in samples) Game1.player.addItemToInventoryBool(item);
        Monitor.Log(Helper.Translation.Get("debug.delivered").ToString(), LogLevel.Info);
    }
}
