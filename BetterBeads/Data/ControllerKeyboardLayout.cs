namespace BetterBeads.Data;

public sealed record ControllerKeyboardLayout(UiRect Box,UiRect[] Pages,UiRect[] Keys,UiRect[] Actions)
{
    public const string FirstPage="ABCDEFGHIJKLMNOPQRSTUVWX";
    public const string SecondPage="YZ0123456789";
    public static ControllerKeyboardLayout Calculate(UiRect frame)
    {
        int width=Math.Min(520,frame.Width-16),height=Math.Min(390,frame.Height-16);
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        int left=box.X+16,innerWidth=box.Width-32,gap=4;
        var pages=new[]{new UiRect(left,box.Y+78,120,40),new UiRect(left+128,box.Y+78,136,40)};
        int keyWidth=(innerWidth-gap*5)/6;
        var keys=Enumerable.Range(0,24).Select(i=>new UiRect(left+i%6*(keyWidth+gap),
            box.Y+124+i/6*48,keyWidth,44)).ToArray();
        int actionGap=8,actionWidth=(innerWidth-3*actionGap)/4;
        var actions=Enumerable.Range(0,4).Select(i=>new UiRect(left+i*(actionWidth+actionGap),
            box.Bottom-52,i==3?innerWidth-3*(actionWidth+actionGap):actionWidth,44)).ToArray();
        return new(box,pages,keys,actions);
    }
    public UiRect[] Targets(int page)=>Pages.Concat(Keys.Take(page==0?FirstPage.Length:SecondPage.Length)).Concat(Actions).ToArray();
}
