namespace BetterBeads.Data;

// Session-local inventory descriptions, never serialized as a save or product identity.
public sealed record InventorySlot(string Identity,string ItemId,int Count,int MaxStack,bool CanReceiveBeads=false,
    string? RawMaterial=null,int Yield=0,int Quality=0);
public sealed record InventoryChange(int Slot,InventorySlot? Before,InventorySlot? After);

public static class BeadItems
{
    // Persisted item IDs must remain compatible with existing saves.
    public static string BeadId(string materialId)=>"(O)xinzh.BetterBeads.Bead."+materialId;
}

public sealed class TransactionRequests
{
    private readonly HashSet<string> completed=new(StringComparer.Ordinal);
    private string? active;
    public bool TryBegin(string id)
    {
        if(string.IsNullOrWhiteSpace(id) || active is not null || completed.Contains(id))return false;
        active=id;return true;
    }
    public void Finish(string id,bool succeeded)
    {
        if(active!=id)throw new InvalidOperationException("Mismatched transaction request.");
        if(succeeded)completed.Add(id);
        active=null;
    }
    public void Clear(){active=null;completed.Clear();}
}
