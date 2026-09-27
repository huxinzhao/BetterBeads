using BetterBeads.Data;
using StardewModdingAPI;
using StardewValley;

namespace BetterBeads.Runtime;

// Remove old vanilla bead recipes while preserving existing beads, designs and crafted counters outside this mod.
internal sealed class BeadCraftingContent
{
    private const string Prefix="xinzh.BetterBeads.Craft.";
    public void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested+=(_,e)=>
        {
            if(e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))e.Edit(asset=>
            {
                var data=asset.AsDictionary<string,string>().Data;
                foreach(string id in data.Keys.Where(id=>id.StartsWith(Prefix,StringComparison.Ordinal)).ToArray())data.Remove(id);
            },StardewModdingAPI.Events.AssetEditPriority.Late);
        };
    }
    public void Synchronize()
    {
        if(!Context.IsWorldReady)return;
        foreach(string id in Game1.player.craftingRecipes.Keys.Where(id=>id.StartsWith(Prefix,StringComparison.Ordinal)).ToArray())
            Game1.player.craftingRecipes.Remove(id);
    }
}
