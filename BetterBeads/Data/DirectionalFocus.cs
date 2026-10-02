namespace BetterBeads.Data;

public static class DirectionalFocus
{
    public static int Move(IReadOnlyList<UiRect> targets,int current,int dx,int dy)
    {
        if(targets.Count==0)return -1;
        current=Math.Clamp(current,0,targets.Count-1);
        if(dx==0&&dy==0)return current;
        if(dx!=0&&dy!=0)dy=0;
        var from=targets[current];float cx=from.X+from.Width/2f,cy=from.Y+from.Height/2f;
        int best=current;float score=float.MaxValue;
        for(int i=0;i<targets.Count;i++)
        {
            if(i==current)continue;
            var target=targets[i];
            float tx=target.X+target.Width/2f-cx,ty=target.Y+target.Height/2f-cy;
            float along=tx*dx+ty*dy;if(along<=1)continue;
            float cross=Math.Abs(tx*dy-ty*dx);
            float candidate=along+cross*2;
            if(candidate<score){score=candidate;best=i;}
        }
        return best;
    }
}
