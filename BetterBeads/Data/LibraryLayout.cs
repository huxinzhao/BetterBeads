namespace BetterBeads.Data;

public sealed record LibraryLayout(UiRect Search, UiRect Filter, UiRect Rows, UiRect Status,
    UiRect Actions, UiRect Dialog, int RowsPerPage)
{
    public static UiRect LiteArea(EditorLayout bench)
    {
        int top=bench.EditorTab.Bottom+16;
        return new UiRect(bench.Body.X,top,bench.Body.Width,bench.Footer.Bottom-top);
    }
    public static UiRect LiteBack(UiRect area)=>new(area.Right-152,area.Y-56,152,40);
    public static LibraryLayout Calculate(UiRect area,UiRect? frame=null)
    {
        int count=Math.Max(1,(area.Height-164)/64);
        return new(new(area.X,area.Y,area.Width-156,44),new(area.Right-144,area.Y,144,40),
            new(area.X,area.Y+48,area.Width,(count-1)*64+58),
            new(area.X,area.Bottom-112,area.Width,28),new(area.X,area.Bottom-84,area.Width,84),
            frame is {} f?new(f.X+16,f.Y+20,f.Width-32,f.Height-40):new(area.X,area.Y-124,area.Width,area.Height+124),count);
    }
}
