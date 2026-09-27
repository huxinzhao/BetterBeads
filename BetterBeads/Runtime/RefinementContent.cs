using BetterBeads.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Machines;

namespace BetterBeads.Runtime;

internal sealed class RefinementContent
{
    private const string Furnace="(BC)13";
    private readonly ProcessingCatalog catalog;
    private readonly IMonitor monitor;
    private static IReadOnlyDictionary<string,RefinementRecipe> inputs=new Dictionary<string,RefinementRecipe>();
    public RefinementContent(ProcessingCatalog catalog,IMonitor monitor){this.catalog=catalog;this.monitor=monitor;}
    public void Register(IModHelper helper)
    {
        inputs=DefaultRefinement.Recipes(catalog).ToDictionary(r=>BeadItems.BeadId(r.SourceMaterial));
        helper.Events.Content.AssetRequested+=OnAssetRequested;
    }
    private void OnAssetRequested(object? sender,AssetRequestedEventArgs e)
    {
        if(!e.NameWithoutLocale.IsEquivalentTo("Data/Machines"))return;
        e.Edit(asset=>
        {
            var machines=asset.AsDictionary<string,MachineData>().Data;
            if(!machines.TryGetValue(Furnace,out var furnace) || furnace.AdditionalConsumedItems is not {Count:1}
                || furnace.AdditionalConsumedItems[0].ItemId is not ("382" or "(O)382") || furnace.AdditionalConsumedItems[0].RequiredCount!=1)
            {
                monitor.Log(ContentText.Get("copy.RefinementContent.f607114b69","熔炉的额外耗材已变更，未添加拼豆精炼配方，以免按错误成本扣料。"),LogLevel.Warn);return;
            }
            furnace.OutputRules??=new();
            foreach(var recipe in inputs.Values.Reverse())
            {
                string id="xinzh.BetterBeads.Refine."+recipe.SourceMaterial;
                furnace.OutputRules.RemoveAll(r=>r.Id==id);
                furnace.OutputRules.Insert(0,new(){Id=id,MinutesUntilReady=recipe.Minutes,
                    Triggers=new(){new(){Id="input",Trigger=MachineOutputTrigger.ItemPlacedInMachine,
                        RequiredItemId=BeadItems.BeadId(recipe.SourceMaterial),RequiredCount=recipe.InputCount}},
                    OutputItem=new(){new(){Id="refined",OutputMethod="BetterBeads.Runtime.RefinementContent, BetterBeads: Output"}}});
            }
        },AssetEditPriority.Late);
    }
    // Native machinery owns all input/coal deduction and saved timers; this callback only prepares an output.
    public static Item? Output(StardewValley.Object machine,Item inputItem,bool probe,MachineItemOutput outputData,Farmer player,out int? overrideMinutesUntilReady)
    {
        overrideMinutesUntilReady=null;
        if(!FeatureAccess.Has("refinement"))
        {if(!probe)Game1.showRedMessage(ContentText.Get("copy.RefinementContent.8b41723d1a","完成克林特的「耐热试验」后可精炼拼豆。"));return null;}
        if(Context.IsMultiplayer || machine.QualifiedItemId!=Furnace || inputItem is not StardewValley.Object input
            || input.GetType()!=typeof(StardewValley.Object) || input.Quality!=0 || !InventoryService.IsPlainObject(input)
            || !inputs.TryGetValue(input.QualifiedItemId,out var recipe))return null;
        overrideMinutesUntilReady=recipe.Minutes;
        return ItemRegistry.Create(BeadItems.BeadId(recipe.OutputMaterial),recipe.OutputCount);
    }
}
