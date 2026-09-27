using System.Text.Json;
using BetterBeads.Data;
using StardewValley;

namespace BetterBeads.Runtime;

/// <summary>Small opt-in bridge for the separate circuit mod. Never exposes mutable save records.</summary>
public sealed class CircuitBridge
{
    private readonly Func<SaveProgress?> progress;
    private readonly ProductItems products;
    internal CircuitBridge(Func<SaveProgress?> progress,ProductItems products){this.progress=progress;this.products=products;}
    public string ListArtworks()
    {
        var source=progress();if(source is null)return "[]";
        var entries=new BlueprintRepository(source).List()
            .Where(e=>e.Design is {} d&&CircuitArtworkExport.Supported(d))
            .Select(e=>new{Id=e.Id,Name=e.Design!.Name,TemplateId=e.Design.TemplateId,
                Width=e.Design.Views["front"].Width,Height=e.Design.Views["front"].Height}).ToArray();
        return JsonSerializer.Serialize(entries);
    }
    public string? GetArtwork(string id)
    {
        var source=progress();
        if(source is null||string.IsNullOrWhiteSpace(id)||!new BlueprintRepository(source).TryOpen(id,out var design)
            ||design is null)return null;
        return CircuitArtworkExport.Serialize(design);
    }

    public Item? CreateCreativeController()
    {
        if(progress() is null)return null;
        return products.Create(CircuitCreativeArtwork.CreateController());
    }
}
