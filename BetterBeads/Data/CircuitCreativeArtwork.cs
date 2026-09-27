namespace BetterBeads.Data;

public static class CircuitCreativeArtwork
{
    public static ProductSnapshot CreateController()
    {
        var design=SimpleCrafting.Blank(SimpleCrafting.Ornament);
        design.Name="拼豆音乐控制器";
        var grid=design.Views["front"];
        for(int y=2;y<14;y++)for(int x=2;x<14;x++)
        {
            bool fill=x==7&&y is >=3 and <=10||x==8&&y is >=3 and <=10
                ||y is >=10 and <=12&&x is >=4 and <=8||x is >=9 and <=11&&y==3;
            if(fill)grid.Cells[y*16+x]=new BeadCell{ColorId="circuit-gold",Rgba=0xF2C46FFFu,MaterialId="wood"};
        }
        return new ProductSnapshot{Design=design,RulesVersion="creative-circuit-1",CreatedInCreativeMode=true};
    }
}
