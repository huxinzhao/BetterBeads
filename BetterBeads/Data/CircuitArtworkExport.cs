using System.Text.Json;

namespace BetterBeads.Data;

public static class CircuitArtworkExport
{
    public static bool Supported(Blueprint d)=>d.Use is ProductUse.Picture or ProductUse.WoodFurniture
        &&SimpleCrafting.Supported(d)&&d.Views.TryGetValue("front",out var grid)&&grid.Cells.Count==grid.Width*grid.Height;
    public static string? Serialize(Blueprint d)
    {
        if(!Supported(d))return null;
        var grid=d.Views["front"];
        return JsonSerializer.Serialize(new{Id=d.Id,Name=d.Name,TemplateId=d.TemplateId,
            Width=grid.Width,Height=grid.Height,Pixels=grid.Cells.Select(c=>c?.Rgba??0u).ToArray()});
    }
}
