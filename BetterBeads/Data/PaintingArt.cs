namespace BetterBeads.Data;

public static class PaintingArt
{
    public static BeadGrid Compact(BeadGrid source,uint[]? frame=null)
    {
        frame=frame is {Length:64}?frame:DefaultFrame;
        var result=source.Copy();
        for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
        {
            bool border=x==0||y==0||x==source.Width-1||y==source.Height-1;
            if(border)result.Cells[y*source.Width+x]=new(){ColorId="__frame",Rgba=frame[(y==0?2:y==source.Height-1?6:3)*8+(x==0?2:x==source.Width-1?6:3)]};
            else if(result.Cells[y*source.Width+x] is null)result.Cells[y*source.Width+x]=new(){ColorId="__canvas",Rgba=0xF1E5CCFF};
        }
        return result;
    }
    public static uint[] DefaultFrame=>Enumerable.Range(0,64).Select(i=>i%8 is 0 or 7 || i/8 is 0 or 7?0x513423FFu:i/8==1 || i%8==1?0xEAC38BFFu:i/8==6 || i%8==6?0x83532EFFu:0xA87541FFu).ToArray();
    public static BeadGrid Compose(BeadGrid source,uint[]? frame=null)
    {
        frame=frame is {Length:64}?frame:DefaultFrame;int size=source.Width+16,offset=8;
        var result=new BeadGrid{Width=size,Height=size,Cells=Enumerable.Repeat<BeadCell?>(null,size*size).ToList()};
        for(int y=offset-2;y<offset+source.Height+2;y++)for(int x=offset-2;x<offset+source.Width+2;x++)
        {
            bool inside=x>=offset&&x<offset+source.Width&&y>=offset&&y<offset+source.Height;
            int sx=x<offset?x-offset+2:x>=offset+source.Width?6+x-offset-source.Width:3;
            int sy=y<offset?y-offset+2:y>=offset+source.Height?6+y-offset-source.Height:3;
            uint rgba=inside?source.Cells[(y-offset)*source.Width+x-offset]?.Rgba??0xF1E5CCFFu:frame[sy*8+sx];
            result.Cells[y*size+x]=new(){ColorId="frame",Rgba=rgba};
        }
        return result;
    }
}
