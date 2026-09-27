namespace BetterBeads.Data;

public sealed record ImportLayout(UiRect Dialog,UiRect Image,UiRect Status,UiRect Footer,int SelectionRows)
{
    public static ImportLayout Calculate(UiRect frame)
    {
        var box=new UiRect(frame.X+16,frame.Y+20,frame.Width-32,frame.Height-40);
        return new(box,new(box.X+12,box.Y+44,box.Width-24,Math.Max(48,box.Height-184)),
            new(box.X+12,box.Bottom-132,box.Width-24,28),new(box.X+12,box.Bottom-96,box.Width-24,88),
            Math.Max(1,(box.Height-164)/42));
    }
}
