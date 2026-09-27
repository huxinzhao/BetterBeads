namespace BetterBeads.Data;

public sealed record BlueprintAutoSave(string Id,string? Before,string After,Blueprint Saved)
{
    public static BlueprintAutoSave? Prepare(SaveProgress progress,Blueprint source)
    {
        progress.BlueprintRecords.TryGetValue(source.Id,out var before);
        var copy=source.Copy();
        if(before is not null)
        {
            if(!DesignStorage.TryReadBlueprint(before,out var old)||old!.Revision!=source.Revision)return null;
            if(DesignStorage.Serialize(old)==DesignStorage.Serialize(source))return new(source.Id,before,before,copy);
        }
        var detached=new SaveProgress{BlueprintRecords=new(progress.BlueprintRecords)};
        if(!new BlueprintRepository(detached).Save(copy))return null;
        return new(copy.Id,before,detached.BlueprintRecords[copy.Id],copy);
    }
    public bool Matches(SaveProgress p)=>p.BlueprintRecords.GetValueOrDefault(Id)==Before;
    public void Apply(SaveProgress p)
    {if(!Matches(p))throw new InvalidOperationException("Blueprint changed");p.BlueprintRecords[Id]=After;}
    public void Restore(SaveProgress p){if(Before is null)p.BlueprintRecords.Remove(Id);else p.BlueprintRecords[Id]=Before;}
}
