namespace BetterBeads.Data;

internal sealed record ArtworkColor(uint Rgba,int Count);
internal sealed class ArtworkPaletteCache
{
    private long version=long.MinValue;
    private Blueprint? source,preview;
    private ArtworkColor[] colors=Array.Empty<ArtworkColor>();
    private (uint From,uint To) pair;
    private void Refresh(long current,Func<Blueprint> read)
    {if(version==current&&source is not null)return;source=read();colors=ArtworkColors.Read(source);preview=null;version=current;}
    internal ArtworkColor[] Colors(long current,Func<Blueprint> read){Refresh(current,read);return colors;}
    internal Blueprint Preview(long current,Func<Blueprint> read,uint from,uint to)
    {Refresh(current,read);if(preview is null||pair!=(from,to)){preview=ArtworkColors.Replace(source!,from,to);pair=(from,to);}return preview;}
    internal void Clear(){source=preview=null;colors=Array.Empty<ArtworkColor>();version=long.MinValue;}
}
internal static class ArtworkColors
{
    internal static ArtworkColor[] Read(Blueprint design)=>design.Views["front"].Cells.Where(c=>c is not null)
        .GroupBy(c=>c!.Rgba).Select(g=>new ArtworkColor(g.Key,g.Count()))
        .OrderByDescending(c=>c.Count).ThenBy(c=>c.Rgba).ToArray();
    internal static Blueprint Replace(Blueprint source,uint from,uint to)
    {
        var result=source.Copy();
        if(from!=to)foreach(var cell in result.Views["front"].Cells)
            if(cell is not null&&cell.Rgba==from){cell.Rgba=to;cell.ColorId=BeadPalette.Id(to);}
        return result;
    }
    internal static Blueprint CopyDraft(Blueprint source)
    {var copy=source.Copy(true);copy.Name="";return copy;}
}

#if BEADS_LITE
internal static class LibraryPreferences
{
    internal static void Normalize(SaveProgress progress)
    {
        progress.FavoriteBlueprintIds??=new();progress.RecentBlueprintIds??=new();
        progress.FavoriteBlueprintIds.RemoveWhere(id=>string.IsNullOrWhiteSpace(id));
        progress.RecentBlueprintIds=progress.RecentBlueprintIds.Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(100).ToList();
    }
    internal static void Touch(SaveProgress progress,string id)
    {progress.RecentBlueprintIds.RemoveAll(v=>v==id);progress.RecentBlueprintIds.Insert(0,id);if(progress.RecentBlueprintIds.Count>100)progress.RecentBlueprintIds.RemoveRange(100,progress.RecentBlueprintIds.Count-100);}
    internal static void Remove(SaveProgress progress,string id)
    {progress.FavoriteBlueprintIds.Remove(id);progress.RecentBlueprintIds.RemoveAll(v=>v==id);}
    internal static IEnumerable<T> Query<T>(IEnumerable<T> source,SaveProgress progress,Func<T,string> id,Func<T,Blueprint> design,
        ProductUse? use,bool favorites,bool recent)
    {
        var ranks=progress.RecentBlueprintIds.Select((value,index)=>(value,index)).ToDictionary(p=>p.value,p=>p.index,StringComparer.Ordinal);
        return source.Where(e=>(use is null||design(e).Use==use)&&(!favorites||progress.FavoriteBlueprintIds.Contains(id(e))))
            .OrderBy(e=>recent?ranks.GetValueOrDefault(id(e),int.MaxValue):0)
            .ThenBy(e=>design(e).Name,StringComparer.OrdinalIgnoreCase).ThenBy(id,StringComparer.Ordinal);
    }
}
#endif

// Uses the same atlas transform as placed furniture, including the dynamic ornament footprint.
internal static class SceneProduct
{
    internal static ProductSnapshot Snapshot(Blueprint source)
    {
        var design=SimpleCrafting.Normalize(source);
        string variant=FurnitureFinish.Variant(design);
        return new(){Design=design,FurnitureVariantId=variant.Length>0?variant:null};
    }
    internal static BeadGrid Compose(Blueprint source,uint[]? frame=null)
    {
        var snapshot=Snapshot(source);
        if(SimpleCrafting.IsWeapon(source.Use))return snapshot.Design.Views["front"].Copy();
        return FurnitureFinish.Compose(snapshot,frame);
    }
    internal static (int Width,int Height) Footprint(Blueprint source)
    {
        var snapshot=Snapshot(source);
        return FurnitureTemplates.TryGet(snapshot.FurnitureVariantId??snapshot.Design.TemplateId,out var furniture)
            ?(furniture.FootprintWidth,furniture.FootprintHeight):(0,0);
    }
}
