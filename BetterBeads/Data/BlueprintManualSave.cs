namespace BetterBeads.Data;

public enum BlueprintSaveKind { Created, Updated, Unchanged, Reused, NeedsName, Conflict }
public sealed record BlueprintSaveDecision(BlueprintSaveKind Kind,Blueprint? Existing=null,BlueprintAutoSave? Change=null);

/// <summary>Resolves manual saves before any progress record is changed.</summary>
public static class BlueprintManualSave
{
    public static BlueprintSaveDecision Prepare(SaveProgress progress,Blueprint source)
    {
        if(!DesignStorage.IsStructurallyValid(source))return new(BlueprintSaveKind.Conflict);
        bool present=progress.BlueprintRecords.ContainsKey(source.Id);
        if(present&&(!new BlueprintRepository(progress).TryOpen(source.Id,out var current)
            ||current!.Revision!=source.Revision))return new(BlueprintSaveKind.Conflict);
        var equivalent=BlueprintContent.Find(progress,source);
        if(equivalent is not null)
        {
            if(equivalent.Id!=source.Id)return new(BlueprintSaveKind.Reused,equivalent);
            if(equivalent.Name==source.Name)return new(BlueprintSaveKind.Unchanged,equivalent);
        }
        if(!present&&string.IsNullOrWhiteSpace(source.Name))return new(BlueprintSaveKind.NeedsName);
        var change=BlueprintAutoSave.Prepare(progress,source);
        return change is null?new(BlueprintSaveKind.Conflict)
            :new(present?BlueprintSaveKind.Updated:BlueprintSaveKind.Created,Change:change);
    }
}
