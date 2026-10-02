using BetterBeads.Data;
using StardewModdingAPI;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed record ManufacturingResult(bool Success,string Message,ProductSnapshot? Product=null,Blueprint? Saved=null);

internal sealed partial class ManufacturingService
{
    private readonly InventoryService inventory;
    private readonly ProductItems products;
    private readonly IMonitor monitor;
    private readonly TransactionRequests requests=new();
    private readonly IReadOnlyDictionary<string,ManufacturingRecipe> recipes;
    public ManufacturingService(InventoryService inventory,ProductItems products,IMonitor monitor,IEnumerable<ManufacturingRecipe> recipes)
    {
        this.inventory=inventory;this.products=products;this.monitor=monitor;
        var definitions=recipes.ToArray();var issues=ManufacturingCatalog.Validate(definitions,inventory.Catalog);
        if(issues.Count>0)throw new ArgumentException(string.Join(", ",issues));
        this.recipes=definitions.ToDictionary(r=>r.Template.Id,r=>r with{Template=r.Template with{Views=(string[])r.Template.Views.Clone()}},StringComparer.Ordinal);
    }
    public ManufacturingRecipe? FindRecipe(string templateId)=>recipes.TryGetValue(templateId,out var recipe)
        ?recipe with{Template=recipe.Template with{Views=(string[])recipe.Template.Views.Clone()}}:null;
    public IReadOnlyList<TemplateSpec> SingleViewChoices=>recipes.Values.Select(r=>r.Template)
        .Where(t=>t.ManufacturingAvailable)
        .OrderBy(t=>t.Use).ThenBy(t=>t.Id,StringComparer.Ordinal).Select(t=>t with{Views=(string[])t.Views.Clone()}).ToArray();
    public int ReadRawCount(string itemId)=>(int)Math.Min(int.MaxValue,inventory.Capture().Slots.Where(s=>s?.ItemId==itemId && s.RawMaterial is not null).Sum(s=>(long)s!.Count));
    public IReadOnlyDictionary<string,int> ReadStock()=>MaterialSupply.Stock(inventory.Capture().Slots,inventory.Catalog);
    public (int Radius,int Chests) SupplyRange {get{var snapshot=inventory.Capture();return(inventory.Radius,snapshot.ChestCount);}}
    public ManufacturingPreview Preview(Blueprint design,ManufacturingRecipe recipe,SaveProgress progress,string request)
    {
        if(!inventory.IsAvailable)return new(ManufacturingFailure.InvalidInventory,null,null);
        if(progress.Workshop?.CanMake(design,inventory.Catalog,PlayMode.Creative)==false)return new(ManufacturingFailure.FeatureLocked,null,null);
        var snapshot=inventory.Capture();
        return ManufacturingTransaction.Preview(design,recipe,inventory.Catalog,progress.UnlockedColors,
            snapshot.Slots,inventory.Owner.Money,request,PlayMode.Creative,snapshot.BackpackSlots,snapshot.Scope);
    }
    public ManufacturingAvailability CheckAvailability(Blueprint design,ManufacturingRecipe recipe,SaveProgress progress)
    {
        if(!inventory.IsAvailable)return new(ManufacturingFailure.InvalidInventory,null);
        if(progress.Workshop?.CanMake(design,inventory.Catalog,PlayMode.Creative)==false)return new(ManufacturingFailure.FeatureLocked,null);
        var snapshot=inventory.Capture();
        return ManufacturingTransaction.CheckAvailability(design,recipe,inventory.Catalog,progress.UnlockedColors,
            snapshot.Slots,inventory.Owner.Money,PlayMode.Creative,snapshot.BackpackSlots);
    }

    public string Submit(ManufacturingPlan plan,ManufacturingRecipe currentRecipe,SaveProgress progress)
        =>SubmitResult(plan,currentRecipe,progress).Message;

    public ManufacturingResult SubmitResult(ManufacturingPlan plan,ManufacturingRecipe currentRecipe,SaveProgress progress,SaveProgress? marketProgress=null)
    {
        if(!Context.IsWorldReady || (Context.IsMultiplayer && !Context.IsMainPlayer))return new(false,"manufacturing.unavailable");
        if(!requests.TryBegin(plan.RequestId))return new(false,"manufacturing.duplicate");
        marketProgress??=progress;
        bool succeeded=false;
        BlueprintAutoSave? autoSave=null;
        Item?[] before=Array.Empty<Item?>();
        InventorySnapshot? scope=null;
        var lengths=new Dictionary<StardewValley.Inventories.IInventory,int>();
        var replacements=new Dictionary<int,Item?>();
        try
        {
            if(!inventory.IsAvailable)return new(false,"manufacturing.changed");
            scope=inventory.Capture();before=scope.Bindings.Select(b=>b.Read()).ToArray();
            foreach(var binding in scope.Bindings)lengths.TryAdd(binding.Items,binding.Items.Count);
            if(plan.InventoryScope!=scope.Scope || plan.BackpackSlots!=scope.BackpackSlots || plan.IsCreative!=PlayMode.Creative
                || !plan.Matches(scope.Slots,inventory.Owner.Money))return new(false,"manufacturing.changed");
            var snapshot=plan.Product;
#if !BEADS_LITE
            if(currentRecipe.DirectItems)
            {autoSave=BlueprintAutoSave.Prepare(progress,snapshot.Design);if(autoSave is null)return new(false,"library.conflict");}
#endif
            if(progress.Workshop?.CanMake(snapshot.Design,inventory.Catalog,PlayMode.Creative)==false)return new(false,"manufacturing.error.FeatureLocked");
            if(snapshot.RulesVersion!=inventory.Catalog.RulesVersion || !plan.MatchesRules(currentRecipe,inventory.Catalog))return new(false,"manufacturing.changed");
#if BEADS_LITE
            if(string.IsNullOrWhiteSpace(snapshot.Design.Name))
                snapshot.Design.Name=snapshot.Design.Use switch
                {
                    ProductUse.Picture=>ContentText.Get("simple.picture-category","拼豆画"),
                    ProductUse.WoodFurniture=>ContentText.Get("simple.ornament","拼豆摆件"),
                    ProductUse.Sword=>ContentText.Get("simple.sword","拼豆剑"),
                    ProductUse.Dagger=>ContentText.Get("simple.dagger","拼豆匕首"),
                    ProductUse.Hammer=>ContentText.Get("simple.hammer","拼豆锤"),
                    ProductUse.Wallpaper=>ContentText.Get("simple.wallpaper","拼豆壁纸"),
                    ProductUse.Flooring=>ContentText.Get("simple.flooring","拼豆地板"),
                    _=>ContentText.Get("simple.picture-category","拼豆画")
                };
            bool fixedPrice=SimpleCrafting.IsDecoration(snapshot.Design.Use);
            if(!fixedPrice&&marketProgress.ArtworkValuationSequence==long.MaxValue)return new(false,"manufacturing.failed");
            long valuationSequence=marketProgress.ArtworkValuationSequence;
            if(!snapshot.CreatedInCreativeMode&&!fixedPrice)
                snapshot.Valuation=ArtworkMarket.Roll(unchecked((long)Game1.uniqueIDForThisGame),valuationSequence,snapshot,BaseMaterialPrice);
#endif
            string rulesBefore=DesignStorage.Serialize(inventory.Catalog),recipeBefore=DesignStorage.Serialize(currentRecipe);
            // The frozen visual is intentional; current rules, costs and capacity are checked again.
            var refreshed=ManufacturingTransaction.Preview(snapshot.Design,currentRecipe,inventory.Catalog,
                progress.UnlockedColors,plan.InventoryBefore,inventory.Owner.Money,plan.RequestId,PlayMode.Creative,scope.BackpackSlots,scope.Scope);
            if(refreshed.Plan is not {} next || next.OutputItemId!=plan.OutputItemId || next.OutputSlot!=plan.OutputSlot
                || next.MoneyAfter!=plan.MoneyAfter || !next.Changes.SequenceEqual(plan.Changes)
                || !next.Product.ActualMaterials.OrderBy(p=>p.Key).SequenceEqual(snapshot.ActualMaterials.OrderBy(p=>p.Key))
                || next.Product.WeaponRulesVersion!=snapshot.WeaponRulesVersion
                || next.Product.EffectRulesVersion!=snapshot.EffectRulesVersion || !next.Product.Effects.SequenceEqual(snapshot.Effects)
                || !next.Product.EffectParameters.OrderBy(p=>p.Key).SequenceEqual(snapshot.EffectParameters.OrderBy(p=>p.Key))
                || !next.Product.FinalStats.OrderBy(p=>p.Key).SequenceEqual(snapshot.FinalStats.OrderBy(p=>p.Key)))
                return new(false,"manufacturing.changed");
            foreach(var change in plan.Changes)
            {
                if(change.Slot==plan.OutputSlot)
                {
                    var output=products.Create(snapshot);
                    if(output.QualifiedItemId!=plan.OutputItemId || output.Stack!=1
                        || !output.modData.TryGetValue(ProductItems.SnapshotKey,out var raw)
                        || raw!=DesignStorage.Serialize(snapshot))return new(false,"manufacturing.failed");
                    replacements[change.Slot]=output;
                }
                else if(change.After is null)replacements[change.Slot]=null;
                else
                {
                    bool created=change.Before?.Identity!=change.After.Identity;
                    var remainder=created?ItemRegistry.Create(change.After.ItemId,change.After.Count):before[change.Slot]!.getOne();
                    if(before.Any(item=>ReferenceEquals(item,remainder)))return new(false,"manufacturing.failed");
                    remainder.Stack=change.After.Count;
                    var described=inventory.Describe(remainder);
                    if(described is null || described with{Identity=change.After.Identity}!=change.After)return new(false,"manufacturing.failed");
                    replacements[change.Slot]=remainder;
                }
            }
            // Item constructors/copy hooks may invoke other mods. No original slot is touched until all are ready.
            var latest=inventory.Capture();
            if(!inventory.IsAvailable || latest.Scope!=scope.Scope || !plan.Matches(latest.Slots,inventory.Owner.Money))return new(false,"manufacturing.changed");
            if(rulesBefore!=DesignStorage.Serialize(inventory.Catalog) || recipeBefore!=DesignStorage.Serialize(currentRecipe)
                || plan.IsCreative!=PlayMode.Creative)return new(false,"manufacturing.changed");
            if(progress.Workshop?.CanMake(snapshot.Design,inventory.Catalog,PlayMode.Creative)==false)return new(false,"manufacturing.error.FeatureLocked");
            if(autoSave is not null && !autoSave.Matches(progress))return new(false,"library.conflict");
            AtomicCraftCommit.Apply(()=>
            {
                foreach(var replacement in replacements)scope.Bindings[replacement.Key].Write(replacement.Value);
                inventory.Owner.Money=plan.MoneyAfter;
            },()=>
            {
                foreach(int slot in replacements.Keys)scope!.Bindings[slot].Write(before[slot]);
                foreach(var pair in lengths)while(pair.Key.Count>pair.Value)pair.Key.RemoveAt(pair.Key.Count-1);
                inventory.Owner.Money=plan.MoneyBefore;
            },autoSave,progress
#if BEADS_LITE
            ,()=>
            {
                if(snapshot.CreatedInCreativeMode||fixedPrice)return;
                if(marketProgress.ArtworkValuationSequence!=valuationSequence)throw new InvalidOperationException("Artwork valuation sequence changed");
                marketProgress.ArtworkValuationSequence=valuationSequence+1;
                CollectorLedger.Record(progress,Game1.Date.TotalDays,snapshot);
            },()=>
            {
                if(fixedPrice)return;
                marketProgress.ArtworkValuationSequence=valuationSequence;
                CollectorLedger.Remove(progress,Game1.Date.TotalDays,snapshot.InstanceId);
            }
#endif
            );
            succeeded=true;
            return new(true,"manufacturing.success",snapshot,autoSave?.Saved);
        }
        catch(Exception ex)
        {
            monitor.Log(ContentText.Get("copy.ManufacturingService.34f97298fc","熨烫提交失败：")+ex,LogLevel.Error);
            return new(false,"manufacturing.failed");
        }
        finally{requests.Finish(plan.RequestId,succeeded);}
    }
    public void Clear()=>requests.Clear();
#if BEADS_LITE
    private static int? BaseMaterialPrice(string id)
        =>id.StartsWith("(O)",StringComparison.Ordinal)&&Game1.objectData is {} prices&&prices.TryGetValue(id[3..],out var data)?data.Price:null;
#endif
}
