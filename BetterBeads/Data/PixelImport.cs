namespace BetterBeads.Data;

public enum ImportSizing { Original, Crop, FitNearest }
public sealed record ImportOptions(ImportSizing Sizing, byte AlphaThreshold = 128, bool UnlockedOnly = false,
    string? MaterialId = null, int CropX = 0, int CropY = 0, bool PreserveColors = false);
public sealed record ImportPreview(Blueprint Candidate, int EffectiveCells, IReadOnlyList<string> LockedColors);

/// <summary>Consumes straight-alpha RGBA pixels. Never changes a source, editor or inventory.</summary>
public static class PixelImport
{
    public static uint FromPremultiplied(byte red,byte green,byte blue,byte alpha)
    {
        if(alpha==0)return 0;
        uint r=(uint)Math.Min(255,(red*255+alpha/2)/alpha);
        uint g=(uint)Math.Min(255,(green*255+alpha/2)/alpha);
        uint b=(uint)Math.Min(255,(blue*255+alpha/2)/alpha);
        return (r<<24)|(g<<16)|(b<<8)|alpha;
    }
    public static ReferencePixels Capture(uint[] atlas,int width,int height,UiRect source,string? itemId)
    {
        if(width<=0 || height<=0 || (long)width*height!=atlas.LongLength
            || source.Width<=0 || source.Height<=0 || source.Width>DesignStorage.MaxDimension || source.Height>DesignStorage.MaxDimension
            || source.X<0 || source.Y<0 || (long)source.X+source.Width>width || (long)source.Y+source.Height>height)
            throw new ArgumentException("Invalid source rectangle.");
        var pixels=new uint[source.Width*source.Height];
        for(int y=0;y<source.Height;y++)Array.Copy(atlas,(source.Y+y)*width+source.X,pixels,y*source.Width,source.Width);
        return new(){SourceItemId=itemId,Width=source.Width,Height=source.Height,Pixels=pixels};
    }

    public static ImportPreview Create(Blueprint design,string view,ReferencePixels source,ImportOptions options,
        ProcessingCatalog catalog,IReadOnlySet<string> unlocked)
    {
        if(!DesignStorage.IsStructurallyValid(design) || !design.Views.TryGetValue(view,out var target)
            || source.Width<=0 || source.Height<=0 || source.Width>DesignStorage.MaxDimension || source.Height>DesignStorage.MaxDimension
            || source.Pixels is null || source.Pixels.Length!=(long)source.Width*source.Height
            || !Enum.IsDefined(typeof(ImportSizing),options.Sizing))throw new ArgumentException("Invalid import data.");
        bool clothing=design.Use is ProductUse.Hat or ProductUse.Shirt or ProductUse.Pants;
        if(WeaponMaterials.IsWeapon(design.Use) && options.MaterialId is not null && !catalog.Materials.Any(m=>m.Id==options.MaterialId && MaterialRules.CanUse(m,design.Use)))
            throw new ArgumentException("Material is incompatible with this design.");
        var palette=catalog.Colors
            .OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
        if(palette.Length==0)throw new ArgumentException("No available palette colors.");
        var map=RasterMapping.Create(source.Width,source.Height,target.Width,target.Height,options.Sizing,options.CropX,options.CropY);
        var grid=new BeadGrid{Width=target.Width,Height=target.Height,Cells=Enumerable.Repeat<BeadCell?>(null,target.Cells.Count).ToList()};
        var missing=new HashSet<string>(StringComparer.Ordinal);int effective=0;
        for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
        {
            var (sx,sy)=map.SourceAt(x,y);
            uint rgba=source.Pixels[sy*source.Width+sx];int alpha=(byte)rgba;
            if(alpha==0 || alpha<options.AlphaThreshold)continue;
            var nearest=options.PreserveColors?new PaletteColor{Id=BeadPalette.Id(rgba),Rgba=rgba|255u}:palette.MinBy(c=>Distance(rgba,c.Rgba))!;
            grid.Cells[(y+map.OffsetY)*grid.Width+x+map.OffsetX]=new(){ColorId=nearest.Id,Rgba=nearest.Rgba,MaterialId=clothing?null:MaterialRules.IsDecoration(design.Use)?"decoration":options.MaterialId};
            effective++;
        }
        var candidate=design.Copy();candidate.Views[view]=grid;candidate.Reference=source.Copy();
        return new(candidate,effective,missing.OrderBy(id=>id,StringComparer.Ordinal).ToArray());
    }
    private static int Distance(uint a,uint b)
    {
        int r=(byte)(a>>24)-(byte)(b>>24),g=(byte)(a>>16)-(byte)(b>>16),bl=(byte)(a>>8)-(byte)(b>>8);
        return r*r+g*g+bl*bl;
    }
}
