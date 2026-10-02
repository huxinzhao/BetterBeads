namespace BetterBeads.Data;

/// <summary>Convert a Lite draft without changing its identity or mutating its source.</summary>
public static class SimpleDesignConversion
{
    public static Blueprint Convert(Blueprint source,string templateId,out int clippedBeads)
    {
        if(!SimpleCrafting.Supported(source))throw new ArgumentException("Unsupported source design",nameof(source));
        var target=SimpleCrafting.Blank(templateId);
        if(!SimpleCrafting.Supported(target))throw new ArgumentException("Unsupported target template",nameof(templateId));
        var result=source.Copy();
        var oldGrid=source.Views["front"];
        var newGrid=target.Views["front"];
        int dx=(newGrid.Width-oldGrid.Width)/2,dy=(newGrid.Height-oldGrid.Height)/2;
        string metal=SimpleCrafting.IsWeapon(source.Use)?SimpleCrafting.Metal(source):"";
        if(!SimpleCrafting.Metals.Contains(metal))metal="copper";
        clippedBeads=0;
        for(int y=0;y<oldGrid.Height;y++)for(int x=0;x<oldGrid.Width;x++)
        {
            var bead=oldGrid.Cells[y*oldGrid.Width+x];
            if(bead is null)continue;
            int nx=x+dx,ny=y+dy;
            if(nx<0||ny<0||nx>=newGrid.Width||ny>=newGrid.Height){clippedBeads++;continue;}
            var copy=bead.Copy();
            copy.MaterialId=SimpleCrafting.IsWeapon(target.Use)?metal:"decoration";
            newGrid.Cells[ny*newGrid.Width+nx]=copy;
        }
        result.TemplateId=target.TemplateId;
        result.Use=target.Use;
        result.BackgroundRgba=SimpleCrafting.IsDecoration(target.Use)?source.BackgroundRgba:0xEADFC6FF;
        result.SwordOrientation=target.Use==ProductUse.Sword&&source.Use==ProductUse.Sword?source.SwordOrientation:SwordOrientation.Diagonal;
        result.Views=new(){["front"]=newGrid};
        result.SupplementaryMaterials=SimpleCrafting.IsWeapon(target.Use)?new(){{metal,0}}:new();
        result.WoolMaterials.Clear();
        result.SelectedEffects.Clear();
        return result;
    }
}
