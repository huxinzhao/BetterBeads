namespace BetterBeads.Data;

public sealed record MaterialPanelLayout(UiRect Title,UiRect Amount,UiRect Range,UiRect Inspect,UiRect View)
{
    public static MaterialPanelLayout Calculate(UiRect area,bool multipleViews)=>new(
        new(area.X+16,area.Y+4,area.Width-32,24),new(area.X+16,area.Y+32,area.Width-32,24),
        new(area.X+16,area.Y+60,area.Width-32,24),new(area.X+16,area.Y+96,multipleViews?112:136,44),
        multipleViews?new(area.Right-120,area.Y+96,104,44):default);
}
