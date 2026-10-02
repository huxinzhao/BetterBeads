namespace BetterBeads.Data;

/// <summary>Tiles are anchored to a decorating area's upper-left corner; partial edges are cropped.</summary>
public static class DecorationPattern
{
    public static int Part(int size,int x,int y)
    {
        if(size is not (16 or 32)||x<0||y<0)throw new ArgumentOutOfRangeException();
        int span=size/16;
        return y%span*span+x%span;
    }
    public static FinishedPixels Atlas(Blueprint design,ArtDefinition art,bool fused)
    {
        var grid=design.Views["front"];
        int density=fused?BeadFinish.Density:1;
        var baked=fused?BeadFinish.Bake(grid,art):null;
        int width=256*density,height=16*density;
        var pixels=new uint[width*height];
        for(int partY=0;partY<grid.Width/16;partY++)for(int partX=0;partX<grid.Width/16;partX++)
        for(int y=0;y<16*density;y++)for(int x=0;x<16*density;x++)
        {
            int part=partY*(grid.Width/16)+partX;
            uint rgba=baked is null
                ?grid.Cells[(partY*16+y)*grid.Width+partX*16+x]?.Rgba??design.BackgroundRgba
                :baked.Pixels[(partY*16*density+y)*baked.Width+partX*16*density+x];
            if((rgba&255)==0)rgba=design.BackgroundRgba;
            pixels[y*width+part*16*density+x]=(rgba&0xFFFFFF00)|255;
        }
        return new(width,height,pixels);
    }
}
