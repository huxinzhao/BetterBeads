namespace BetterBeads.Data;

/// <summary>Pure pixel-art geometry and swing-only angle correction.</summary>
public static class WeaponShape
{
    private const double RootTwo=1.4142135623730951;

    public static int SpeedPenalty(BeadGrid grid)
    {
        int count=grid.Cells.Count(cell=>cell is not null);
        return count<=64?0:count<=256?1:count<=576?2:3;
    }

    public static double ReachScale(BeadGrid grid)
    {
        int width=grid.Width,height=grid.Height;
        var seen=new bool[grid.Cells.Count];
        List<(int X,int Y)> largest=new();
        double largestSpan=-1;
        for(int start=0;start<grid.Cells.Count;start++)
        {
            if(seen[start]||grid.Cells[start] is null)continue;
            var component=new List<(int X,int Y)>();
            var pending=new Queue<int>();pending.Enqueue(start);seen[start]=true;
            while(pending.Count>0)
            {
                int i=pending.Dequeue(),x=i%width,y=i/width;
                component.Add((x,y));
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0)continue;
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||ny<0||nx>=width||ny>=height)continue;
                    int next=ny*width+nx;
                    if(!seen[next]&&grid.Cells[next] is not null){seen[next]=true;pending.Enqueue(next);}
                }
            }
            double span=SpanSquared(component);
            if(component.Count>largest.Count||component.Count==largest.Count&&span>largestSpan)
            {largest=component;largestSpan=span;}
        }
        if(largest.Count==0)return .5;
        return Math.Clamp((Math.Sqrt(largestSpan)+RootTwo)/(16*RootTwo),.5,2);
    }

    private static double SpanSquared(List<(int X,int Y)> points)
    {
        if(points.Count<2)return 0;
        var sorted=points.OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
        static int Cross((int X,int Y) a,(int X,int Y) b,(int X,int Y) c)
            =>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        var lower=new List<(int X,int Y)>();
        foreach(var point in sorted)
        {
            while(lower.Count>=2&&Cross(lower[^2],lower[^1],point)<=0)lower.RemoveAt(lower.Count-1);
            lower.Add(point);
        }
        var upper=new List<(int X,int Y)>();
        foreach(var point in sorted.Reverse())
        {
            while(upper.Count>=2&&Cross(upper[^2],upper[^1],point)<=0)upper.RemoveAt(upper.Count-1);
            upper.Add(point);
        }
        lower.RemoveAt(lower.Count-1);upper.RemoveAt(upper.Count-1);
        var hull=lower.Concat(upper).ToArray();
        if(hull.Length==1)return 0;
        int farthest=0,j=1;
        static int Distance((int X,int Y) a,(int X,int Y) b)
        {int dx=a.X-b.X,dy=a.Y-b.Y;return dx*dx+dy*dy;}
        for(int i=0;i<hull.Length;i++)
        {
            int next=(i+1)%hull.Length;
            while(Math.Abs(Cross(hull[i],hull[next],hull[(j+1)%hull.Length]))
                >Math.Abs(Cross(hull[i],hull[next],hull[j])))j=(j+1)%hull.Length;
            farthest=Math.Max(farthest,Math.Max(Distance(hull[i],hull[j]),Distance(hull[next],hull[j])));
        }
        return farthest;
    }

    public static float SwingCorrection()=>MathF.PI/4;

    // Native sword grip is (1,15) in its 16px sprite. Preserve that point in world space,
    // including frames whose Draw origin isn't the grip (e.g. special attacks).
    public static (float OffsetX,float OffsetY,float OriginX,float OriginY,float Rotation) VerticalSwing(
        float nativeOriginX,float nativeOriginY,float rotation,float scale,int size,bool flipHorizontal,bool flipVertical)
    {
        float dx=((flipHorizontal?15:1)-nativeOriginX)*scale;
        float dy=((flipVertical?1:15)-nativeOriginY)*scale;
        float cos=MathF.Cos(rotation),sin=MathF.Sin(rotation);
        return(dx*cos-dy*sin,dx*sin+dy*cos,size/2f,flipVertical?0:size,rotation+SwingCorrection());
    }
}
