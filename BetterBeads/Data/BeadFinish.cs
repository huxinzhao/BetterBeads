namespace BetterBeads.Data;

public sealed record FinishedPixels(int Width,int Height,uint[] Pixels);
public static class BeadFinish
{
    public const int Density=4;
    public static FinishedPixels Bake(BeadGrid grid,ArtDefinition art,bool subtle=false)
    {
        int width=grid.Width*Density,height=grid.Height*Density;var pixels=new uint[width*height];
        float strength=(float)(art.FusedBeadStrength*(subtle?0.35:1));
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)
        {
            var cell=grid.Cells[y*grid.Width+x];if(cell is null)continue;
            bool bead=cell.ColorId is not ("__frame" or "__canvas");
            for(int sy=0;sy<Density;sy++)for(int sx=0;sx<Density;sx++)
            {
                float shade=bead?1+(float)(art.FusedBeadMask[sy*Density+sx]-1)*strength:1;
                uint Channel(int shift)=>(uint)Math.Clamp((int)Math.Round(((cell.Rgba>>shift)&255)*shade),0,255);
                pixels[(y*Density+sy)*width+x*Density+sx]=(Channel(24)<<24)|(Channel(16)<<16)|(Channel(8)<<8)|(cell.Rgba&255);
            }
        }
        return new(width,height,pixels);
    }
}
