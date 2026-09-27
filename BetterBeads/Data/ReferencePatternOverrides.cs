namespace BetterBeads.Data;

public sealed record PatternPixels(int Width,int Height,uint[] Pixels);

/// <summary>Artist-supplied pixels, separate from identity, unlocks and material rules.</summary>
public static class ReferencePatternOverrides
{
    private static readonly Dictionary<string,Dictionary<string,PatternPixels>> patterns=new();
    public static string FileName(string id,string view)=>id.Replace('.','-')+(view=="front"?"":"-"+view)+".png";
    public static void Clear()=>patterns.Clear();
    public static bool TrySet(string id,IReadOnlyDictionary<string,PatternPixels> views)
    {
        var reference=ReferencePatterns.All.FirstOrDefault(p=>p.Id==id);
        var spec=reference is null?null:ReferencePatterns.Template(reference.TemplateId);
        if(spec is null || views.Count!=spec.Views.Length)return false;
        foreach(string view in spec.Views)
        {
            if(!views.TryGetValue(view,out var image) || image is null || image.Width!=spec.Width || image.Height!=spec.Height
                || image.Pixels is null || image.Pixels.Length!=spec.Width*spec.Height
                || image.Pixels.Any(c=>(c&255) is not (0 or 255)) || !image.Pixels.Any(c=>(c&255)==255))return false;
        }
        patterns[id]=views.ToDictionary(p=>p.Key,p=>p.Value with{Pixels=(uint[])p.Value.Pixels.Clone()});
        return true;
    }
    public static PatternPixels? Get(string id,string view)=>patterns.TryGetValue(id,out var views) && views.TryGetValue(view,out var image)
        ?image with{Pixels=(uint[])image.Pixels.Clone()}:null;
}
