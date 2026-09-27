namespace BetterBeads.Data;

/// <summary>One scale for drawing, layout and hit testing; does not change game options.</summary>
public readonly record struct WorkbenchScale(float Factor,int Width,int Height)
{
    public int Logical(int coordinate)=>(int)Math.Floor(coordinate/Factor);
    public static WorkbenchScale Calculate(int width,int height,float nativeFontHeight)
    {
        float native=Math.Max(1,nativeFontHeight/20f);
        float responsive=Math.Min(width/1000f,height/700f);
        float fit=Math.Max(1,Math.Min(width/480f,height/420f));
        float factor=Math.Min(fit,Math.Max(native,responsive));
        return new(factor,(int)Math.Floor(width/factor+0.001f),(int)Math.Floor(height/factor+0.001f));
    }
}
