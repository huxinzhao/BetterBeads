namespace BetterBeads.Data;

// One detached picture and fingerprint for one serialized product revision.
internal sealed class ProductRenderData
{
    public ProductSnapshot Snapshot { get; }
    public string VisualKey { get; }
    private (int X,int Y,int Width,int Height)? visibleBounds;
    public (int X,int Y,int Width,int Height) VisibleBounds=>visibleBounds??=SimpleCrafting.OccupiedBounds(Snapshot.Design.Views["front"]);

    public ProductRenderData(ProductSnapshot source)
    {
        Snapshot=source.Copy();
        VisualKey=DesignStorage.VisualKey(Snapshot);
    }

    public string CacheKey(string view,int frameRevision)
        =>VisualKey+":"+view+(SimpleCrafting.Wall(Snapshot.Design.TemplateId)||FurnitureFinish.IsNew(Snapshot.FurnitureVariantId)?":"+frameRevision:"");
}
