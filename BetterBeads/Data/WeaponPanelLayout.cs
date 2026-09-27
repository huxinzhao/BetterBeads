namespace BetterBeads.Data;

public sealed record WeaponPanelLayout(UiRect Dialog,UiRect Title,UiRect Tabs,UiRect Body,UiRect Footer)
{
    public static WeaponPanelLayout Calculate(UiRect frame)
    {
        var box=new UiRect(frame.X+16,frame.Y+20,frame.Width-32,frame.Height-40);
        return new(box,new(box.X+12,box.Y+8,box.Width-24,28),new(box.X+12,box.Y+44,box.Width-24,36),
            new(box.X+12,box.Y+88,box.Width-24,box.Height-148),new(box.X+12,box.Bottom-48,box.Width-24,36));
    }
}
