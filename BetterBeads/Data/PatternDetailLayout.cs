namespace BetterBeads.Data;
public sealed record PatternDetailLayout(UiRect Frame,UiRect Title,UiRect Preview,UiRect Directions,UiRect Details,UiRect Back,UiRect Copy,UiRect Mode,bool Compact)
{
    public static PatternDetailLayout Calculate(int width,int height)
    {
        bool compact=width<700 || height<580;
        int w=Math.Min(compact?520:650,width-32),h=Math.Min(compact?540:480,height-24);
        var frame=new UiRect((width-w)/2,(height-h)/2,w,h);
        int side=Math.Min(compact?190:280,Math.Max(96,h-(compact?280:200)));
        side=Math.Min(side,compact?w-48:w-300);
        var preview=new UiRect(compact?frame.X+(w-side)/2:frame.X+24,frame.Y+64,side,side);
        var directions=new UiRect(preview.X,preview.Bottom+8,side,40);
        var details=compact?new UiRect(frame.X+24,directions.Bottom+4,w-48,Math.Max(24,frame.Bottom-72-directions.Bottom))
            :new UiRect(preview.Right+24,frame.Y+68,Math.Max(1,frame.Right-preview.Right-48),h-152);
        return new(frame,new(frame.X+24,frame.Y+20,w-156,32),preview,directions,details,
            new(frame.X+24,frame.Bottom-60,100,40),new(frame.Right-188,frame.Bottom-60,164,40),new(frame.Right-120,frame.Y+16,96,40),compact);
    }
}
