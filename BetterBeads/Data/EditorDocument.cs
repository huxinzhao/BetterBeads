namespace BetterBeads.Data;

public enum BrushTool { Paint, Erase, Material }

/// <summary>Detached draft, bounded undo history, and tools with no inventory side effects.</summary>
public sealed class EditorDocument
{
    private Blueprint draft;
    private readonly ProcessingCatalog catalog;
    private readonly List<Blueprint> undo = new();
    private readonly List<Blueprint> redo = new();
    private Blueprint? strokeStart;
    private BeadCell? strokeBead;
    private BrushTool strokeTool;
    private int lastX, lastY;
    private string saved;
    private string? currentContent;
    private Blueprint savedDesign;
    private bool unsavedIdentity;
    private const int HistoryLimit = 40;
    public string View { get; private set; }
    public long ChangeVersion { get; private set; }
    public (int Width,int Height) ViewSize => (draft.Views[View].Width,draft.Views[View].Height);
    public bool IsClothing => draft.Use is ProductUse.Hat or ProductUse.Shirt or ProductUse.Pants;
    public bool IsDirty => unsavedIdentity || (currentContent ??= ContentKey(draft)) != saved;
    public bool IsStrokeActive => strokeStart is not null;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public Blueprint Snapshot() => draft.Copy();
    public BeadGrid ViewSnapshot() => draft.Views[View].Copy();
    public bool TrySampleColor(int x,int y,out string colorId)
    {
        int index=Index(x,y);
        var bead=index<0?null:draft.Views[View].Cells[index];
        colorId=bead is null?"":BeadPalette.Id(bead.Rgba);
        return bead is not null;
    }

    public EditorDocument(Blueprint design, ProcessingCatalog catalog, string view)
    {
        if (!DesignStorage.IsStructurallyValid(design) || !design.Views.ContainsKey(view))
            throw new ArgumentException("Invalid editor design or view.");
        draft = design.Copy(); this.catalog = catalog; View = view; saved = ContentKey(draft); currentContent=saved; savedDesign=draft.Copy();
    }

    public void SetView(string view)
    {
        if (!draft.Views.ContainsKey(view)) throw new ArgumentException("Unknown view.");
        EndStroke(); if(View!=view){View = view;ChangeVersion++;}
    }

    private bool LegalMaterial(string? id) => id is not null && catalog.Materials.Any(m => m.Id == id && MaterialRules.CanUse(m,draft.Use));
    private int Index(int x, int y) => x >= 0 && y >= 0 && x < draft.Views[View].Width && y < draft.Views[View].Height
        ? y * draft.Views[View].Width + x : -1;

    public bool BeginStroke(int x, int y, BrushTool tool, string colorId, string? materialId)
    {
        EndStroke();
        if (Index(x, y) < 0 || (tool == BrushTool.Material && (IsClothing || !LegalMaterial(materialId)))) return false;
        var color = BeadPalette.Resolve(catalog,colorId);
        if (tool == BrushTool.Paint && color is null) return false;
        strokeStart = draft.Copy(); strokeTool = tool;
        lastX = x; lastY = y;
        strokeBead = new() { ColorId = color?.Id ?? "", Rgba = color?.Rgba ?? 0, MaterialId = IsClothing ? null : tool!=BrushTool.Material && MaterialRules.IsDecoration(draft.Use)?"decoration":materialId };
        ContinueStroke(x, y);
        return true;
    }

    // Leaving the canvas ends the gesture. Re-entering without a new BeginStroke cannot paint.
    public void ContinueStroke(int x, int y)
    {
        if (strokeStart is null) return;
        int index = Index(x, y);
        if (index < 0) { EndStroke(); return; }
        int px = lastX, py = lastY, dx = Math.Abs(x - px), dy = -Math.Abs(y - py);
        int sx = px < x ? 1 : -1, sy = py < y ? 1 : -1, error = dx + dy;
        while (true)
        {
            ApplyBrush(Index(px, py));
            if (px == x && py == y) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; px += sx; }
            if (twice <= dx) { error += dx; py += sy; }
        }
        lastX = x; lastY = y;
    }

    private void ApplyBrush(int index)
    {
        var cells = draft.Views[View].Cells;
        var old = cells[index];
        if (strokeTool == BrushTool.Erase)
        {
            if(old is null)return;
            cells[index]=null;
        }
        else if (strokeTool == BrushTool.Material)
        {
            if(old is null || old.MaterialId==strokeBead!.MaterialId)return;
            old.MaterialId=strokeBead.MaterialId;
        }
        else if (old is not null)
        {
            if(old.ColorId==strokeBead!.ColorId && old.Rgba==strokeBead.Rgba)return;
            old.ColorId=strokeBead.ColorId;old.Rgba=strokeBead.Rgba;
        }
        else if (IsClothing || LegalMaterial(strokeBead!.MaterialId)) cells[index] = strokeBead!.Copy();
        else return;
        currentContent=null;ChangeVersion++;
    }

    public void EndStroke()
    {
        if (strokeStart is null) return;
        Remember(strokeStart); strokeStart = null; strokeBead = null;
    }

    public BeadCell? Pick(int x, int y)
    {
        int index = Index(x, y);
        return index < 0 ? null : draft.Views[View].Cells[index]?.Copy();
    }

    public bool Fill(int x, int y, string colorId, string? materialId)
    {
        EndStroke();
        int start = Index(x, y);
        var color = BeadPalette.Resolve(catalog,colorId);
        if (start < 0 || color is null) return false;
        var grid = draft.Views[View];
        var target = grid.Cells[start]?.Copy();
        if(MaterialRules.IsDecoration(draft.Use))materialId="decoration";
        if (target is null && !IsClothing && !LegalMaterial(materialId)) return false;
        return Change(() =>
        {
            var seen = new bool[grid.Cells.Count];
            var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (seen[i]) continue;
                seen[i] = true;
                var old = grid.Cells[i];
                if (target is null ? old is not null : old is null || old.Rgba != target.Rgba) continue;
                if (old is null) grid.Cells[i] = new() { ColorId = color.Id, Rgba = color.Rgba, MaterialId = IsClothing ? null : materialId };
                else { old.ColorId = color.Id; old.Rgba = color.Rgba; }
                int px = i % grid.Width, py = i / grid.Width;
                if (px > 0) queue.Enqueue(i - 1); if (px + 1 < grid.Width) queue.Enqueue(i + 1);
                if (py > 0) queue.Enqueue(i - grid.Width); if (py + 1 < grid.Height) queue.Enqueue(i + grid.Width);
            }
        });
    }

    public bool Mirror(bool horizontal) => Change(() =>
    {
        var grid = draft.Views[View]; var before = grid.Copy();
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
            grid.Cells[y * grid.Width + x] = before.Cells[(horizontal ? y : grid.Height - 1 - y) * grid.Width + (horizontal ? grid.Width - 1 - x : x)];
    });

    public int CountColor(string id) => draft.Views[View].Cells.Count(c => c is not null && (c.ColorId==id || c.Rgba==BeadPalette.Resolve(catalog,id)?.Rgba));
    public int CountMaterial(string? id) => draft.Views[View].Cells.Count(c => c is not null && c.MaterialId == id);
    public bool ReplaceColor(string from, string to)
    {
        var color = BeadPalette.Resolve(catalog,to);
        var original=BeadPalette.Resolve(catalog,from);
        return color is not null && Change(() => { foreach (var cell in draft.Views[View].Cells)
            if (cell is not null && (cell.ColorId==from || cell.Rgba==original?.Rgba)) { cell.ColorId = color.Id; cell.Rgba = color.Rgba; } });
    }
    public bool ReplaceMaterial(string? from, string to) => !IsClothing && LegalMaterial(to) && Change(() =>
    {
        foreach (var cell in draft.Views[View].Cells) if (cell is not null && cell.MaterialId == from) cell.MaterialId = to;
    });

    // Import/UI must preview the detached candidate before calling this. One application = one undo step.
    public bool ApplyCandidate(Blueprint candidate)
    {
        if (!DesignStorage.IsStructurallyValid(candidate) || candidate.Id != draft.Id || candidate.Revision != draft.Revision || candidate.Views.Count==0) return false;
        return Change(() => {draft = candidate.Copy();if(!draft.Views.ContainsKey(View))View=draft.Views.Keys.First();});
    }
    internal bool ReplaceExactColor(uint from,uint to)
        =>from!=to&&draft.Views[View].Cells.Any(c=>c?.Rgba==from)&&ApplyCandidate(ArtworkColors.Replace(draft,from,to));
    // Compatibility migration is a detached edit, not an undo step that can resurrect removed tools.
    public bool StageMigration(Blueprint candidate)
    {
        if(!DesignStorage.IsStructurallyValid(candidate)||candidate.Id!=draft.Id||candidate.Revision!=draft.Revision||candidate.Views.Count==0)return false;
        EndStroke();if(ContentKey(candidate)==ContentKey(draft))return false;
        draft=candidate.Copy();if(!draft.Views.ContainsKey(View))View=draft.Views.Keys.First();
        undo.Clear();redo.Clear();currentContent=null;ChangeVersion++;return true;
    }
    public void MarkSaved(int revision)
    { EndStroke(); draft.Revision = revision; saved = currentContent ??= ContentKey(draft); savedDesign=draft.Copy();unsavedIdentity=false; ChangeVersion++; }
    public void AdoptSaved(Blueprint design)
    {
        EndStroke();draft=design.Copy();saved=ContentKey(draft);currentContent=saved;savedDesign=draft.Copy();
        unsavedIdentity=false;undo.Clear();redo.Clear();ChangeVersion++;
        if(!draft.Views.ContainsKey(View))View=draft.Views.Keys.First();
    }

    public bool Rename(string name) => name is not null && Change(()=>draft.Name=name);
    public bool ClearCanvas()=>Change(()=>{foreach(var grid in draft.Views.Values)for(int i=0;i<grid.Cells.Count;i++)grid.Cells[i]=null;});
    public bool SetWholeMetal(string metal)=>SimpleCrafting.IsWeapon(draft.Use) && SimpleCrafting.Metals.Contains(metal) && Change(()=>
    {draft.SupplementaryMaterials=new(){{metal,0}};foreach(var c in draft.Views.Values.SelectMany(g=>g.Cells).Where(c=>c is not null))c!.MaterialId=metal;});
    public bool SetSwordOrientation(SwordOrientation orientation)=>draft.Use==ProductUse.Sword
        && Enum.IsDefined(orientation)&&Change(()=>draft.SwordOrientation=orientation);
    public bool SetSupplementaryMaterial(string id,int amount) => WeaponMaterials.IsWeapon(draft.Use)
        && amount>=0 && (amount==0 || LegalMaterial(id)) && Change(()=>
        {
            if(amount==0)draft.SupplementaryMaterials.Remove(id);
            else draft.SupplementaryMaterials[id]=amount;
        });
    public bool TryOpen(Blueprint design)
    {
        if(IsDirty || !DesignStorage.IsStructurallyValid(design) || design.Views.Count==0) return false;
        EndStroke();draft=design.Copy();View=draft.Views.Keys.First();
        saved=ContentKey(draft);currentContent=saved;savedDesign=draft.Copy();unsavedIdentity=false;undo.Clear();redo.Clear();ChangeVersion++;
        return true;
    }
    // Imported artwork is a new, unsaved draft. Its blank template is the discard baseline.
    public bool TryOpenImported(Blueprint design)
    {
        if(IsDirty || design.Revision!=0 || !SimpleCrafting.Supported(design))return false;
        EndStroke();
        var blank=SimpleCrafting.Blank(design.TemplateId);blank.Id=design.Id;
        draft=design.Copy();View="front";
        savedDesign=blank;saved=ContentKey(blank);currentContent=null;unsavedIdentity=true;
        undo.Clear();redo.Clear();ChangeVersion++;
        return true;
    }
    public bool SaveAs(BlueprintRepository repository,out Blueprint? copy)
    {
        EndStroke();
        if(!repository.SaveAs(draft,draft.Name,out copy))return false;
        // Adopt the saved copy only after persistence succeeded. The source record stays untouched.
        draft=copy!.Copy();saved=ContentKey(draft);currentContent=saved;savedDesign=draft.Copy();unsavedIdentity=false;undo.Clear();redo.Clear();ChangeVersion++;
        return true;
    }
    public void DiscardChanges()
    {
        EndStroke(); draft=savedDesign.Copy(); currentContent=saved;unsavedIdentity=false; undo.Clear(); redo.Clear();ChangeVersion++;
        if(!draft.Views.ContainsKey(View)) View=draft.Views.Keys.First();
    }

    private static string ContentKey(Blueprint design)
    { var copy = design.Copy(); copy.Revision = 0; return DesignStorage.Serialize(copy); }

    private bool Change(Action action)
    {
        EndStroke(); var before = draft.Copy(); action(); currentContent=null; bool changed=Remember(before);if(changed)ChangeVersion++;return changed;
    }
    private bool Remember(Blueprint before)
    {
        if (DesignStorage.Serialize(before) == DesignStorage.Serialize(draft)) return false;
        undo.Add(before); if (undo.Count > HistoryLimit) undo.RemoveAt(0);
        redo.Clear(); return true;
    }
    public bool Undo()
    {
        EndStroke(); if (undo.Count == 0) return false;
        redo.Add(draft.Copy()); int revision = draft.Revision; draft = undo[^1]; draft.Revision = revision;
        currentContent=null;ChangeVersion++;
        undo.RemoveAt(undo.Count - 1); if (!draft.Views.ContainsKey(View)) View = draft.Views.Keys.First(); return true;
    }
    public bool Redo()
    {
        EndStroke(); if (redo.Count == 0) return false;
        undo.Add(draft.Copy()); int revision = draft.Revision; draft = redo[^1]; draft.Revision = revision;
        currentContent=null;ChangeVersion++;
        redo.RemoveAt(redo.Count - 1); if (!draft.Views.ContainsKey(View)) View = draft.Views.Keys.First(); return true;
    }
}
