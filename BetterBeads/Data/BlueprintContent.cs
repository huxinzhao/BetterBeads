using System.Text.Json;

namespace BetterBeads.Data;

/// <summary>Compares the craftable design, independent of its library identity and reference image.</summary>
public static class BlueprintContent
{
    public static string Key(Blueprint source)
    {
        var design=source.Copy();
        design.Id="";design.Name="";design.Revision=0;design.Reference=null;
        design.Views=design.Views.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
        design.WoolMaterials=design.WoolMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
        design.SupplementaryMaterials=design.SupplementaryMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
        design.SelectedEffects.Sort(StringComparer.Ordinal);
        foreach(var cell in design.Views.Values.SelectMany(grid=>grid.Cells))
            if(cell is not null)cell.ColorId="rgba:"+cell.Rgba.ToString("X8");
        return JsonSerializer.Serialize(design);
    }

    public static Blueprint? Find(SaveProgress progress,Blueprint source)
    {
        string key=Key(source);
        if(progress.BlueprintRecords.TryGetValue(source.Id,out var raw)
            &&DesignStorage.TryReadBlueprint(raw,out var own)&&own!.Id==source.Id&&Key(own)==key)return own;
        foreach(var pair in progress.BlueprintRecords.Where(p=>p.Key!=source.Id).OrderBy(p=>p.Key,StringComparer.Ordinal))
            if(DesignStorage.TryReadBlueprint(pair.Value,out var current)&&current!.Id==pair.Key&&Key(current)==key)return current;
        return null;
    }
}
