namespace BetterBeads.Data;

/// <summary>One reversible commit for inventory and the saved design. Preparation is side-effect free.</summary>
public static class AtomicCraftCommit
{
    public static void Apply(Action deliver,Action restoreInventory,BlueprintAutoSave? blueprint,SaveProgress progress,
        Action? afterSave=null,Action? restoreProgress=null)
    {
        bool saved=false,progressTouched=false;
        if(blueprint is not null && !blueprint.Matches(progress))throw new InvalidOperationException("Blueprint changed");
        try
        {
            deliver();
            if(blueprint is not null){blueprint.Apply(progress);saved=true;}
            if(afterSave is not null){progressTouched=true;afterSave();}
        }
        catch
        {
            try{if(progressTouched)restoreProgress?.Invoke();}
            finally{try{if(saved)blueprint!.Restore(progress);}finally{restoreInventory();}}
            throw;
        }
    }
}
