using BetterBeads.Data;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;

namespace BetterBeads.Runtime;

internal sealed partial class ManufacturingService
{
    public BeadPreparationPreview PreviewPreparation(string material)
    {
        if(!inventory.IsAvailable)return new("manufacturing.unavailable",null);
        var scope=inventory.Capture();
        return BeadPreparation.Preview(scope.Slots,scope.BackpackSlots,scope.Scope,inventory.Catalog,material,Guid.NewGuid().ToString("N"));
    }
    public string SubmitPreparation(BeadPreparationPlan plan)
    {
        if(!inventory.IsAvailable)return "manufacturing.unavailable";
        if(!requests.TryBegin(plan.RequestId))return "manufacturing.duplicate";
        bool success=false,applying=false;InventorySnapshot? scope=null;
        Item?[] before=Array.Empty<Item?>();var replacements=new Dictionary<int,Item?>();var lengths=new Dictionary<IInventory,int>();
        try
        {
            scope=inventory.Capture();
            if(scope.Scope!=plan.Scope || !scope.Slots.SequenceEqual(plan.Before) || DesignStorage.Serialize(inventory.Catalog)!=plan.Rules)return "manufacturing.changed";
            var refreshed=BeadPreparation.Preview(scope.Slots,scope.BackpackSlots,scope.Scope,inventory.Catalog,plan.Material,plan.RequestId);
            if(refreshed.Plan is not {} current || !current.Changes.SequenceEqual(plan.Changes))return "manufacturing.changed";
            before=scope.Bindings.Select(b=>b.Read()).ToArray();
            foreach(var binding in scope.Bindings)lengths.TryAdd(binding.Items,binding.Items.Count);
            foreach(var change in plan.Changes)
            {
                if(change.After is null){replacements[change.Slot]=null;continue;}
                var item=change.Before?.Identity==change.After.Identity?before[change.Slot]!.getOne():ItemRegistry.Create(change.After.ItemId,change.After.Count);
                if(before.Any(old=>ReferenceEquals(old,item)))return "manufacturing.failed";
                item.Stack=change.After.Count;var described=inventory.Describe(item);
                if(described is null || described with{Identity=change.After.Identity}!=change.After)return "manufacturing.failed";
                replacements[change.Slot]=item;
            }
            var latest=inventory.Capture();
            if(!inventory.IsAvailable || latest.Scope!=plan.Scope || !latest.Slots.SequenceEqual(plan.Before)
                || DesignStorage.Serialize(inventory.Catalog)!=plan.Rules)return "manufacturing.changed";
            applying=true;
            foreach(var pair in replacements)scope.Bindings[pair.Key].Write(pair.Value);
            success=true;return "supply.prepared";
        }
        catch(Exception ex)
        {
            if(applying)
            {
                foreach(int slot in replacements.Keys)scope!.Bindings[slot].Write(before[slot]);
                foreach(var pair in lengths)while(pair.Key.Count>pair.Value)pair.Key.RemoveAt(pair.Key.Count-1);
            }
            monitor.Log(ContentText.Get("copy.MaterialPreparationService.b2cc1a7f40","拼豆备料失败：")+ex,LogLevel.Error);return "manufacturing.failed";
        }
        finally{requests.Finish(plan.RequestId,success);}
    }
}
