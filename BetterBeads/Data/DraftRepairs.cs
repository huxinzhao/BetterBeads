namespace BetterBeads.Data;

public static class DraftRepairs
{
    public static Blueprint AssignMaterial(Blueprint source,ProcessingCatalog catalog,string material)
    {
        if(ClothingTemplates.IsClothing(source.Use) || !catalog.Materials.Any(m=>m.Id==material && MaterialRules.CanUse(m,source.Use)))
            throw new ArgumentException("Illegal material assignment.");
        var result=source.Copy();
        foreach(var grid in result.Views.Values)foreach(var cell in grid.Cells)if(cell is not null)cell.MaterialId=material;
        return result;
    }
    public static Blueprint UnlockedColors(Blueprint source,ProcessingCatalog catalog,IReadOnlySet<string> unlocked)
    {
        var colors=catalog.Colors.Where(c=>c.InitiallyUnlocked || unlocked.Contains(c.Id)).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
        if(colors.Length==0)throw new ArgumentException("No unlocked colors.");
        var result=source.Copy();var ids=colors.Select(c=>c.Id).ToHashSet();
        foreach(var g in result.Views.Values)foreach(var cell in g.Cells)
        {
            if(cell is null || ids.Contains(cell.ColorId))continue;
            var closest=colors.MinBy(c=>Distance(cell.Rgba,c.Rgba))!;cell.ColorId=closest.Id;cell.Rgba=closest.Rgba;
        }
        return result;
    }
    private static int Distance(uint a,uint b)
    {int r=(byte)(a>>24)-(byte)(b>>24),g=(byte)(a>>16)-(byte)(b>>16),v=(byte)(a>>8)-(byte)(b>>8);return r*r+g*g+v*v;}
}
