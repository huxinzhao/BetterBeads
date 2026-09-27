namespace BetterBeads.Data;

public sealed record TemplateConversionPreview(Blueprint Candidate,int UnassignedCells,int IncompatibleCells,int IncompatibleSupplements=0);

public static class TemplateConversion
{
    public static bool IsSingleViewUse(ProductUse use)=>use is ProductUse.Picture or ProductUse.WoodFurniture or ProductUse.Statue
        || WeaponMaterials.IsWeapon(use);
    public static TemplateConversionPreview Preview(Blueprint source,TemplateSpec target,ProcessingCatalog catalog,
        ImportSizing sizing=ImportSizing.Original,int cropX=0,int cropY=0,string sourceView="front")
    {
        if(ClothingTemplates.IsClothing(source.Use) || ClothingTemplates.IsClothing(target.Use))
            return ClothingPreview(source,target,catalog,sizing,cropX,cropY,sourceView);
        if(!DesignStorage.IsStructurallyValid(source) || !IsSingleViewUse(source.Use) || !IsSingleViewUse(target.Use)
            || source.Views.Count!=1 || target.Views is not {Length:1} || !source.Views.ContainsKey(target.Views[0])
            || target.BillingView!=target.Views[0] || string.IsNullOrWhiteSpace(target.Id))
            throw new ArgumentException("Unsupported template transition.");
        string view=target.Views[0];var old=source.Views[view];
        var map=RasterMapping.Create(old.Width,old.Height,target.Width,target.Height,sizing,cropX,cropY);
        var grid=new BeadGrid{Width=target.Width,Height=target.Height,Cells=Enumerable.Repeat<BeadCell?>(null,target.Width*target.Height).ToList()};
        int unassigned=0,incompatible=0;
        var allowed=catalog.Materials.Where(m=>MaterialRules.CanUse(m,target.Use)).Select(m=>m.Id).ToHashSet(StringComparer.Ordinal);
        for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
        {
            var (sx,sy)=map.SourceAt(x,y);var cell=old.Cells[sy*old.Width+sx]?.Copy();
            grid.Cells[(y+map.OffsetY)*grid.Width+x+map.OffsetX]=cell;
            if(cell is null)continue;
            if(cell.MaterialId is null)unassigned++;
            else if(!allowed.Contains(cell.MaterialId))incompatible++;
        }
        var candidate=source.Copy();candidate.Use=target.Use;candidate.TemplateId=target.Id;candidate.Views[view]=grid;
        if(MaterialRules.IsDecoration(target.Use))
        {
            foreach(var cell in grid.Cells)if(cell is not null)cell.MaterialId="decoration";
            candidate.SupplementaryMaterials.Clear();candidate.SelectedEffects.Clear();candidate.WoolMaterials.Clear();
            return new(candidate,0,0,0);
        }
        int incompatibleSupplements=source.SupplementaryMaterials.Count(p=>p.Value>0
            && (!WeaponMaterials.IsWeapon(target.Use) || !allowed.Contains(p.Key)));
        return new(candidate,unassigned,incompatible,incompatibleSupplements);
    }

    private static TemplateConversionPreview ClothingPreview(Blueprint source,TemplateSpec target,ProcessingCatalog catalog,
        ImportSizing sizing,int cropX,int cropY,string sourceView)
    {
        if(!DesignStorage.IsStructurallyValid(source) || !source.Views.ContainsKey(sourceView) || target.Views.Length==0
            || target.Width<1 || target.Height<1 || target.Width>DesignStorage.MaxDimension || target.Height>DesignStorage.MaxDimension)
            throw new ArgumentException("Unsupported clothing transition.");
        var candidate=source.Copy();candidate.Views.Clear();candidate.TemplateId=target.Id;candidate.Use=target.Use;
        bool clothing=ClothingTemplates.IsClothing(target.Use);int unassigned=0,incompatible=0;
        foreach(var view in target.Views)
        {
            var grid=new BeadGrid{Width=target.Width,Height=target.Height,Cells=Enumerable.Repeat<BeadCell?>(null,target.Width*target.Height).ToList()};
            // Match named directions when possible. A single picture enters only the selected target view.
            BeadGrid? old=source.Views.GetValueOrDefault(view);
            if(target.Views.Length==1)old=source.Views[sourceView];
            if(old is not null)
            {
                var map=RasterMapping.Create(old.Width,old.Height,target.Width,target.Height,sizing,cropX,cropY);
                for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
                {
                    var (sx,sy)=map.SourceAt(x,y);var cell=old.Cells[sy*old.Width+sx]?.Copy();
                    if(cell is not null)
                    {
                        if(clothing)cell.MaterialId=null;
                        else if(MaterialRules.IsDecoration(target.Use))cell.MaterialId="decoration";
                        else if(cell.MaterialId is null)unassigned++;
                        else if(!catalog.Materials.Any(m=>m.Id==cell.MaterialId && MaterialRules.CanUse(m,target.Use)))incompatible++;
                    }
                    grid.Cells[(y+map.OffsetY)*grid.Width+x+map.OffsetX]=cell;
                }
            }
            candidate.Views[view]=grid;
        }
        candidate.WoolMaterials=clothing?new(){{"wool",target.WoolBudget??0}}:new();
        if(!WeaponMaterials.IsWeapon(target.Use)){candidate.SupplementaryMaterials.Clear();candidate.SelectedEffects.Clear();}
        return new(candidate,unassigned,incompatible,source.SupplementaryMaterials.Count);
    }
}
