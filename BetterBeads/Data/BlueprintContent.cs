using System.Text.Json;

namespace BetterBeads.Data;

/// <summary>Compares the craftable design, independent of its library identity and reference image.</summary>
public static class BlueprintContent
{
    public static string Key(Blueprint source)
    {
        // Copy only the data used by this key. Reference pixels and library identity are ignored;
        // cloning the full blueprint first needlessly duplicates the reference image on every comparison.
        var design=new Blueprint
        {
            Id="",Name="",Revision=0,Reference=null,
            Use=source.Use,TemplateId=source.TemplateId,SwordOrientation=source.SwordOrientation,BackgroundRgba=source.BackgroundRgba,
            Views=source.Views.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value.Copy()),
            WoolMaterials=source.WoolMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value),
            SupplementaryMaterials=source.SupplementaryMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value),
            SelectedEffects=source.SelectedEffects.OrderBy(id=>id,StringComparer.Ordinal).ToList()
        };
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
