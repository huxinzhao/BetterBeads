namespace BetterBeads.Data;

/// <summary>New physical variants keep pre-RC5 furniture definitions intact.</summary>
public static class FurnitureFinish
{
    private const string Prefix="xinzh.BetterBeads.Fused";
    public const string Picture16=Prefix+"Picture16",Picture32=Prefix+"Picture32",Small=Prefix+"Ornament16";
    public const string Wide=Prefix+"Ornament32_2x1",Tall=Prefix+"Ornament32_1x2",Large=Prefix+"Ornament32_2x2",Compact=Prefix+"Ornament32_1x1";
    public static bool IsNew(string? id)=>id is Picture16 or Picture32 or Small or Wide or Tall or Large or Compact;
    public static string Variant(Blueprint d)
    {
        if(d.TemplateId==SimpleCrafting.Picture16)return Picture16;
        if(d.TemplateId==SimpleCrafting.Picture32)return Picture32;
        if(d.TemplateId==SimpleCrafting.Ornament)return Small;
        if(d.TemplateId!=SimpleCrafting.LargeOrnament)return "";
        var b=SimpleCrafting.OccupiedBounds(d.Views["front"]);
        return (b.Width>16,b.Height>16) switch{(true,true)=>Large,(true,false)=>Wide,(false,true)=>Tall,_=>Compact};
    }
    public static IEnumerable<FurnitureTemplateDefinition> Definitions()
    {
        foreach(string id in new[]{Picture16,Picture32,Small,Compact,Wide,Tall,Large})
        {
            bool painting=id is Picture16 or Picture32;
            int width=id is Picture32 or Wide or Large?32:16,height=id is Picture32 or Tall or Large?32:16;
            int canvas=id is Picture16 or Small?16:32;
            yield return new(id,painting?ProductUse.Picture:ProductUse.WoodFurniture,canvas,canvas,width/16,painting?height/16:1,
                painting?canvas==16?"product.picture":"product.picture-detailed":id==Small?"product.ornament":"product.ornament-large",
                "Mods/xinzh.BetterBeads/"+id.Split('.').Last(),painting?"painting":"other",width,height);
        }
    }
    public static BeadGrid Atlas(ProductSnapshot snapshot)
    {
        var d=snapshot.Design;var source=d.Views["front"];
        if(d.Use==ProductUse.Picture||snapshot.FurnitureVariantId==Small)return source.Copy();
        var bounds=SimpleCrafting.OccupiedBounds(source);
        int width=snapshot.FurnitureVariantId is Wide or Large?32:16,height=snapshot.FurnitureVariantId is Tall or Large?32:16;
        var result=new BeadGrid{Width=width,Height=height,Cells=Enumerable.Repeat<BeadCell?>(null,width*height).ToList()};
        int ox=(width-bounds.Width)/2,oy=height-bounds.Height;
        for(int y=0;y<bounds.Height;y++)for(int x=0;x<bounds.Width;x++)
            result.Cells[(oy+y)*width+ox+x]=source.Cells[(bounds.Y+y)*source.Width+bounds.X+x]?.Copy();
        return result;
    }
    public static BeadGrid Compose(ProductSnapshot snapshot,uint[]? frame=null)
    {
        var grid=ProductTemplates.CreateAtlas(snapshot);
        if(snapshot.Design.Use!=ProductUse.Picture)return grid;
        return IsNew(snapshot.FurnitureVariantId)?PaintingArt.Compact(grid,frame):PaintingArt.Compose(grid,frame);
    }
}
