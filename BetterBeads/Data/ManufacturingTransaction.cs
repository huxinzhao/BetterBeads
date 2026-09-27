namespace BetterBeads.Data;

// Values come from confirmed template settings; this module supplies no gameplay defaults.
public sealed record ManufacturingRecipe(TemplateSpec Template,string OutputItemId,int ExtraCost,WeaponStatRules? WeaponRules=null,bool SpecialEffectsEnabled=false,bool DirectItems=false);
public enum ManufacturingFailure { None, InvalidRequest, InvalidRecipe, InvalidInventory, InvalidDesign, InsufficientFunds, NoSpace, InvalidWeaponStats, NoSurplusSpace, FeatureLocked }
public sealed record ManufacturingPreview(ManufacturingFailure Failure,DraftReport? Report,ManufacturingPlan? Plan);
public sealed record ManufacturingAvailability(ManufacturingFailure Failure,DraftReport? Report)
{
    public int AvailableRaw {get;init;}
    public bool CanMake=>Failure==ManufacturingFailure.None;
}

public sealed class ManufacturingPlan
{
    private readonly ProductSnapshot product;
    private readonly string rulesState;
    public string RequestId { get; }
    public string OutputItemId { get; }
    public int OutputSlot { get; }
    public int MoneyBefore { get; }
    public int MoneyAfter { get; }
    public bool IsCreative=>product.CreatedInCreativeMode;
    public string InventoryScope { get; }
    public int BackpackSlots { get; }
    public IReadOnlyList<InventorySlot?> InventoryBefore { get; }
    public IReadOnlyList<InventoryChange> Changes { get; }
    public ProductSnapshot Product => product.Copy();
    internal ManufacturingPlan(string requestId,string outputItemId,int outputSlot,int money,int cost,
        IReadOnlyList<InventorySlot?> inventory,IEnumerable<InventoryChange> changes,ProductSnapshot product,
        ManufacturingRecipe recipe,ProcessingCatalog catalog,int backpackSlots,string inventoryScope)
    {
        RequestId=requestId;OutputItemId=outputItemId;OutputSlot=outputSlot;MoneyBefore=money;MoneyAfter=money-cost;
        InventoryBefore=Array.AsReadOnly(inventory.ToArray());Changes=Array.AsReadOnly(changes.ToArray());this.product=product.Copy();
        rulesState=DescribeRules(recipe,catalog);
        BackpackSlots=backpackSlots;InventoryScope=inventoryScope;
    }
    public bool Matches(IReadOnlyList<InventorySlot?> inventory,int money)
        => money==MoneyBefore && InventoryBefore.SequenceEqual(inventory);
    public bool MatchesRules(ManufacturingRecipe recipe,ProcessingCatalog catalog)=>rulesState==DescribeRules(recipe,catalog);
    private static string DescribeRules(ManufacturingRecipe recipe,ProcessingCatalog catalog)
        =>DesignStorage.Serialize(recipe)+"\n"+DesignStorage.Serialize(catalog);
}

public static class ManufacturingTransaction
{
    public static ManufacturingPreview Preview(Blueprint draft,ManufacturingRecipe recipe,ProcessingCatalog catalog,
        IReadOnlySet<string> unlocked,IReadOnlyList<InventorySlot?> inventory,int money,string requestId,bool creative=false,int backpackSlots=-1,string inventoryScope="")
        =>Evaluate(draft,recipe,catalog,unlocked,inventory,money,requestId,creative,true,backpackSlots,inventoryScope);

    // Check current inputs each frame without creating snapshots or serializing frozen rules.
    // Only an explicit confirmation preview can produce a submit-ready plan.
    public static ManufacturingAvailability CheckAvailability(Blueprint draft,ManufacturingRecipe recipe,ProcessingCatalog catalog,
        IReadOnlySet<string> unlocked,IReadOnlyList<InventorySlot?> inventory,int money,bool creative=false,int backpackSlots=-1)
    {
        var result=Evaluate(draft,recipe,catalog,unlocked,inventory,money,"availability",creative,false,backpackSlots,"");
        int owned=0;
        if(recipe.DirectItems){var cost=SimpleCrafting.Cost(draft);owned=(int)Math.Min(int.MaxValue,inventory.Where(s=>s?.ItemId==cost.Item&&s.RawMaterial is not null).Sum(s=>(long)s!.Count));}
        return new(result.Failure,result.Report){AvailableRaw=owned};
    }

    private static ManufacturingPreview Evaluate(Blueprint draft,ManufacturingRecipe recipe,ProcessingCatalog catalog,
        IReadOnlySet<string> unlocked,IReadOnlyList<InventorySlot?> inventory,int money,string requestId,bool creative,bool createPlan,int backpackSlots,string inventoryScope)
    {
        if(string.IsNullOrWhiteSpace(requestId))return Fail(ManufacturingFailure.InvalidRequest);
        if(recipe.DirectItems)return SimpleCrafting.Evaluate(draft,recipe,catalog,inventory,money,requestId,creative,createPlan,backpackSlots,inventoryScope);
        if(backpackSlots<0)backpackSlots=inventory.Count;
        if(backpackSlots>inventory.Count)return Fail(ManufacturingFailure.InvalidInventory);
        var template=recipe.Template;
        if(!ManufacturingCatalog.ValidRecipe(recipe) || string.IsNullOrWhiteSpace(catalog.RulesVersion))return Fail(ManufacturingFailure.InvalidRecipe);
        bool weapon=WeaponMaterials.IsWeapon(template.Use);
        if(money<0 || inventory.Any(s=>s is not null && (string.IsNullOrWhiteSpace(s.Identity) || string.IsNullOrWhiteSpace(s.ItemId)
            || s.Count<=0 || s.MaxStack<=0 || s.Yield<0 || s.RawMaterial is not null && (s.Yield<=0 || s.CanReceiveBeads))))return Fail(ManufacturingFailure.InvalidInventory);
        var identities=inventory.Where(s=>s is not null).Select(s=>s!.Identity).ToArray();
        if(identities.Distinct(StringComparer.Ordinal).Count()!=identities.Length)return Fail(ManufacturingFailure.InvalidInventory);
        var stock=MaterialSupply.Stock(inventory,catalog);
        var report=DraftAssessment.Evaluate(draft,template,catalog,unlocked,stock,creative);
        if(!report.CanMake)return new(ManufacturingFailure.InvalidDesign,report,null);
        var actualMaterials=report.Materials.ToDictionary(m=>m.Id,m=>m.Needed);
        WeaponStatValues? stats=null;
        SpecialEffectReport? effects=null;
        if(weapon)
        {
            if(recipe.SpecialEffectsEnabled)effects=SpecialEffects.Evaluate(template.Use,catalog,actualMaterials,draft.SelectedEffects);
            else if(draft.SelectedEffects.Count>0)return new(ManufacturingFailure.InvalidWeaponStats,report,null);
            var calculated=WeaponStats.Calculate(template.Use,recipe.WeaponRules!,catalog,actualMaterials,effects);
            if(calculated.Values is null)return new(ManufacturingFailure.InvalidWeaponStats,report,null);
            stats=calculated.Values;
        }
        int cost=creative?0:recipe.ExtraCost;
        if(money<cost)return new(ManufacturingFailure.InsufficientFunds,report,null);
        var after=inventory.ToArray();
        if(!MaterialSupply.Consume(after,creative?new Dictionary<string,int>():actualMaterials,catalog,out var surplus))
            return new(ManufacturingFailure.InvalidInventory,report,null);
        int outputSlot=Array.FindIndex(after,0,backpackSlots,s=>s is null);
        if(outputSlot<0)return new(ManufacturingFailure.NoSpace,report,null);
        after[outputSlot]=new("manufactured:"+requestId,recipe.OutputItemId,1,1);
        if(!MaterialSupply.PlaceBeads(after,surplus,requestId))return new(ManufacturingFailure.NoSurplusSpace,report,null);
        if(!createPlan)return new(ManufacturingFailure.None,report,null);
        var changes=Enumerable.Range(0,after.Length).Where(i=>after[i]!=inventory[i]).Select(i=>new InventoryChange(i,inventory[i],after[i]));
        var product=new ProductSnapshot{Design=draft.Copy(),RulesVersion=catalog.RulesVersion,CreatedInCreativeMode=creative,
            ActualMaterials=actualMaterials,WeaponRulesVersion=recipe.WeaponRules?.Version,
            FinalStats=stats?.ToSnapshot() ?? new(),Effects=effects?.Active.ToList() ?? new(),
            EffectRulesVersion=effects is null?null:SpecialEffects.RulesVersion,
            EffectParameters=effects is null?new():SpecialEffects.Parameters(effects)};
        return new(ManufacturingFailure.None,report,new(requestId,recipe.OutputItemId,outputSlot,money,cost,inventory,changes,product,recipe,catalog,backpackSlots,inventoryScope));
    }
    private static ManufacturingPreview Fail(ManufacturingFailure failure)=>new(failure,null,null);
}
