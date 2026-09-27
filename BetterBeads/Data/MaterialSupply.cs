namespace BetterBeads.Data;

public static class MaterialSupply
{
    public static bool WithinRange(double x,double y,double originX,double originY,int radius)
        =>radius>=0 && Math.Abs(x-originX)<=radius && Math.Abs(y-originY)<=radius;
    public static string? BeadMaterial(InventorySlot? slot,ProcessingCatalog catalog)
        =>slot is {CanReceiveBeads:true}?catalog.Materials.FirstOrDefault(m=>BeadItems.BeadId(m.Id)==slot.ItemId) is {} m?MaterialRules.Canonical(m):null:null;
    public static IReadOnlyDictionary<string,int> Stock(IReadOnlyList<InventorySlot?> slots,ProcessingCatalog catalog)
    {
        var stock=catalog.Materials.Select(MaterialRules.Canonical).Distinct().ToDictionary(id=>id,_=>0);
        foreach(var slot in slots)
        {
            if(slot is null)continue;
            string? id=BeadMaterial(slot,catalog)??slot.RawMaterial;
            if(id is null || !stock.ContainsKey(id))continue;
            long amount=(long)slot.Count*(slot.RawMaterial is not null?slot.Yield:1);
            stock[id]=(int)Math.Clamp((long)stock[id]+amount,0,int.MaxValue);
        }
        return stock;
    }
    // Existing beads first. Common ordinary sources before hardwood; then stable bag/chest slot order.
    private static int Priority(InventorySlot slot)=>slot.RawMaterial=="wool"?(slot.ItemId=="(O)428"?1:0):slot.RawMaterial!="decoration"?0:slot.ItemId switch
        {"(O)388"=>0,"(O)390"=>1,"(O)771"=>2,"(O)709"=>4,_=>3};
    public static bool Consume(InventorySlot?[] after,IReadOnlyDictionary<string,int> demand,ProcessingCatalog catalog,
        out Dictionary<string,int> surplus)
    {
        surplus=new();
        foreach(var pair in demand)
        {
            int remaining=pair.Value;
            foreach(int i in Enumerable.Range(0,after.Length).Where(i=>BeadMaterial(after[i],catalog)==pair.Key))
            {
                if(remaining==0)break;
                var slot=after[i]!;int take=Math.Min(slot.Count,remaining);remaining-=take;
                after[i]=take==slot.Count?null:slot with{Count=slot.Count-take};
            }
            foreach(int i in Enumerable.Range(0,after.Length).Where(i=>after[i] is {} slot && slot.RawMaterial==pair.Key && slot.Yield>0)
                .OrderBy(i=>Priority(after[i]!)).ThenBy(i=>after[i]!.Quality).ToArray())
            {
                if(remaining==0)break;
                var slot=after[i]!;
                int take=(int)Math.Min(slot.Count,((long)remaining+slot.Yield-1)/slot.Yield);
                long produced=(long)take*slot.Yield;
                if(produced>remaining){surplus[pair.Key]=(int)(produced-remaining);remaining=0;}
                else remaining-=(int)produced;
                after[i]=take==slot.Count?null:slot with{Count=slot.Count-take};
            }
            if(remaining>0)return false;
        }
        return true;
    }
    public static bool PlaceBeads(InventorySlot?[] after,IReadOnlyDictionary<string,int> beads,string requestId)
    {
        foreach(var pair in beads.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            int remaining=pair.Value;string id=BeadItems.BeadId(pair.Key);
            for(int i=0;i<after.Length && remaining>0;i++)
            {
                if(after[i] is not {CanReceiveBeads:true} slot || slot.ItemId!=id)continue;
                int add=Math.Min(remaining,Math.Max(0,slot.MaxStack-slot.Count));
                after[i]=slot with{Count=slot.Count+add};remaining-=add;
            }
            for(int i=0;i<after.Length && remaining>0;i++)if(after[i] is null)
            {
                int add=Math.Min(999,remaining);
                after[i]=new("surplus:"+requestId+":"+i,id,add,999,CanReceiveBeads:true);remaining-=add;
            }
            if(remaining>0)return false;
        }
        return true;
    }
}
