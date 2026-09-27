#if BEADS_LITE
namespace BetterBeads.Data;

/// <summary>Convert one game sprite into editable beads without storing game artwork in the mod.</summary>
public static class LiteTemplatePixels
{
    public static Blueprint Create(string name,string templateId,IReadOnlyList<uint> rgba,int width,int height,
        string weaponMaterial="iridium",SwordOrientation orientation=SwordOrientation.Diagonal)
    {
        if(width<=0||height<=0||rgba.Count!=width*height
            ||templateId is not (SimpleCrafting.Picture16 or SimpleCrafting.Picture32 or SimpleCrafting.Sword or SimpleCrafting.Sword24 or SimpleCrafting.Sword32)
            ||!SimpleCrafting.Metals.Contains(weaponMaterial)||!Enum.IsDefined(orientation))
            throw new ArgumentException("Invalid sprite template");
        var design=SimpleCrafting.Blank(templateId);design.Name=name;
        var grid=design.Views["front"];
        int ox=(grid.Width-width)/2,oy=(grid.Height-height)/2;
        if(ox<0||oy<0)throw new ArgumentException("Sprite exceeds the bead canvas");
        bool sword=design.Use==ProductUse.Sword;
        string material=sword?weaponMaterial:"decoration";
        if(sword){design.SupplementaryMaterials.Clear();design.SupplementaryMaterials[material]=0;design.SwordOrientation=orientation;}
        int placed=0;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            uint pixel=rgba[y*width+x];if((pixel&255)<128)continue;
            pixel=(pixel&0xFFFFFF00)|255;
            grid.Cells[(oy+y)*grid.Width+ox+x]=new BeadCell{ColorId=$"custom:{pixel>>8:X6}",Rgba=pixel,MaterialId=material};
            placed++;
        }
        if(placed==0)throw new ArgumentException("Sprite has no visible pixels");
        return design;
    }
}
#endif
