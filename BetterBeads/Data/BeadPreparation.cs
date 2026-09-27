namespace BetterBeads.Data;

public sealed record BeadPreparationPlan(string RequestId,string Scope,string Rules,IReadOnlyList<InventorySlot?> Before,
    IReadOnlyList<InventoryChange> Changes,string Material,int Produced,int SourceSlot,int Consumed);
public sealed record BeadPreparationPreview(string Message,BeadPreparationPlan? Plan);
public static class BeadPreparation
{
    public static BeadPreparationPreview Preview(IReadOnlyList<InventorySlot?> slots,int bagSlots,string scope,
        ProcessingCatalog catalog,string material,string request)
    {
        if(bagSlots<1 || bagSlots>slots.Count || string.IsNullOrWhiteSpace(request))return new("supply.no-space",null);
        var candidates=Enumerable.Range(0,slots.Count).Where(i=>slots[i] is {Count:>0,Yield:>0} s && s.RawMaterial==material)
            .OrderBy(i=>slots[i]!.Quality).ToArray();
        if(candidates.Length==0)return new("supply.no-raw",null);
        int source=candidates[0];var raw=slots[source]!;
        int count=Math.Min(raw.Count,(int)((10L+raw.Yield-1)/raw.Yield));
        int produced=checked(count*raw.Yield);
        var after=slots.ToArray();after[source]=count==raw.Count?null:raw with{Count=raw.Count-count};
        var bag=after.Take(bagSlots).ToArray();
        if(!MaterialSupply.PlaceBeads(bag,new Dictionary<string,int>{{material,produced}},request))return new("supply.no-space",null);
        Array.Copy(bag,after,bagSlots);
        var changes=Enumerable.Range(0,after.Length).Where(i=>after[i]!=slots[i]).Select(i=>new InventoryChange(i,slots[i],after[i])).ToArray();
        return new("",new(request,scope,DesignStorage.Serialize(catalog),Array.AsReadOnly(slots.ToArray()),Array.AsReadOnly(changes),material,produced,source,count));
    }
}
