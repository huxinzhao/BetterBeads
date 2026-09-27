namespace BetterBeads.Data;

public sealed record BlueprintEntry(string Id, string Raw, Blueprint? Design)
{
    public bool IsReadable => Design is not null;
}

/// <summary>Edits are detached from persisted records until Save is explicitly called.</summary>
public sealed class BlueprintRepository
{
    private readonly SaveProgress progress;
    public BlueprintRepository(SaveProgress progress) => this.progress = progress;

    public IReadOnlyList<BlueprintEntry> List(string search = "", ProductUse? use = null)
    {
        var entries = new List<BlueprintEntry>();
        foreach (var pair in progress.BlueprintRecords)
        {
            TryOpen(pair.Key, out var design);
            if (use is not null && design?.Use != use) continue;
            if (!string.IsNullOrEmpty(search) && !(design?.Name ?? pair.Key).Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            entries.Add(new(pair.Key, pair.Value, design));
        }
        return entries.OrderBy(e => e.Design?.Name ?? e.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
    }

    public bool SaveAs(Blueprint source, string name, out Blueprint? saved)
    {
        saved = null;
        if (!DesignStorage.IsStructurallyValid(source) || name is null) return false;
        var copy = source.Copy(true); copy.Name = name;
        if (!Save(copy)) return false;
        saved = copy;
        return true;
    }

    public bool Duplicate(string id, string name, out Blueprint? saved)
    {
        saved = null;
        return TryOpen(id, out var source) && SaveAs(source!, name, out saved);
    }

    public bool Rename(string id, int expectedRevision, string name)
    {
        if (name is null || !TryOpen(id, out var design) || design!.Revision != expectedRevision) return false;
        design.Name = name;
        return Save(design);
    }

    // UI retains the exact selected record through its confirmation dialog.
    // Even unreadable data can only be deleted if that same record is still present.
    public bool Delete(string id, string expectedRaw)
        => progress.BlueprintRecords.TryGetValue(id, out var current) && current == expectedRaw
            && progress.BlueprintRecords.Remove(id);

    public bool TryOpen(string id, out Blueprint? draft)
    {
        draft = null;
        if(progress.BlueprintRecords.TryGetValue(id, out var raw)
            && DesignStorage.TryReadBlueprint(raw, out var parsed) && parsed!.Id == id)
        {draft=parsed;return true;}
        return false;
    }

    public bool Save(Blueprint draft)
    {
        if (!DesignStorage.IsStructurallyValid(draft)) return false;
        int revision = 0;
        if (progress.BlueprintRecords.ContainsKey(draft.Id))
        {
            // Preserve corrupt/unknown records instead of silently overwriting them.
            if (!TryOpen(draft.Id, out var previous)) return false;
            revision = previous!.Revision;
            if (draft.Revision != revision || revision == int.MaxValue) return false;
        }
        else if (draft.Revision != 0) return false; // A stale editor cannot resurrect a deleted record.
        var stored = draft.Copy();
        stored.Revision = revision + 1;
        progress.BlueprintRecords[stored.Id] = DesignStorage.Serialize(stored);
        draft.Revision = stored.Revision;
        return true;
    }
}
