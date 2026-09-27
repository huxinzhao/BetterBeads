namespace BetterBeads.Data;

public static class WeaponReach
{
    public static (int X,int Y,int Width,int Height) Scale(int x,int y,int width,int height,int pivotX,int pivotY,double factor)
    {
        if(factor is not (1 or 1.5 or 2)||width<0||height<0)throw new ArgumentOutOfRangeException(nameof(factor));
        int Edge(int value,int pivot)=>pivot+(int)Math.Round((value-pivot)*factor,MidpointRounding.AwayFromZero);
        int left=Edge(x,pivotX),top=Edge(y,pivotY),right=Edge(x+width,pivotX),bottom=Edge(y+height,pivotY);
        return (left,top,right-left,bottom-top);
    }

    // Native sweep width and near edge stay unchanged; only the weapon's forward reach changes.
    public static (int X,int Y,int Width,int Height) ExtendForward(int x,int y,int width,int height,
        int playerLeft,int playerTop,int playerRight,int playerBottom,int facing,double factor)
    {
        if(!double.IsFinite(factor)||factor<.5||factor>2||width<=0||height<=0||facing is <0 or >3)
            throw new ArgumentOutOfRangeException(nameof(factor));
        int left=x,top=y,right=x+width,bottom=y+height;
        int Edge(int anchor,int far)=>anchor+(int)Math.Round((far-anchor)*factor,MidpointRounding.AwayFromZero);
        switch(facing)
        {
            case 0: top=Math.Min(bottom-1,Edge(playerTop,top));break;
            case 1: right=Math.Max(left+1,Edge(playerRight,right));break;
            case 2: bottom=Math.Max(top+1,Edge(playerBottom,bottom));break;
            case 3: left=Math.Min(right-1,Edge(playerLeft,left));break;
        }
        return(left,top,right-left,bottom-top);
    }

    public static (int X,int Y,int Width,int Height) ScaleCentered(int x,int y,int width,int height,double factor)
    {
        if(!double.IsFinite(factor)||factor<.5||factor>2||width<=0||height<=0)
            throw new ArgumentOutOfRangeException(nameof(factor));
        int newWidth=Math.Max(1,(int)Math.Round(width*factor,MidpointRounding.AwayFromZero));
        int newHeight=Math.Max(1,(int)Math.Round(height*factor,MidpointRounding.AwayFromZero));
        return(x+(width-newWidth)/2,y+(height-newHeight)/2,newWidth,newHeight);
    }
}
