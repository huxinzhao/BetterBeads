namespace BetterBeads.Data;

public sealed record PickerLayout(IReadOnlyList<(int Index,UiRect Rect)> Cells,UiRect Previous,UiRect Next,UiRect Caption,int Page,int Pages)
{
    public UiRect Source {get;init;}
    public static PickerLayout Palette(UiRect area,int count,int page)
    {
        int cell=Math.Clamp((area.Width-34)/4,24,36),pitch=cell+6;
        int columns=Math.Max(1,(area.Width-10)/pitch),rows=Math.Max(1,(area.Height-92)/pitch);
        int perPage=columns*rows,pages=Math.Max(1,(count+perPage-1)/perPage);page=Math.Clamp(page,0,pages-1);
        int left=area.X+(area.Width-(columns*cell+(columns-1)*6))/2;
        var cells=Enumerable.Range(page*perPage,Math.Max(0,Math.Min(perPage,count-page*perPage)))
            .Select((i,n)=>(i,new UiRect(left+n%columns*pitch,area.Y+30+n/columns*pitch,cell,cell))).ToArray();
        int half=(area.Width-20)/2;
        return new(cells,new(area.X+8,area.Bottom-34,half,28),new(area.X+12+half,area.Bottom-34,half,28),
            new(area.X+8,area.Bottom-62,area.Width-16,24),page,pages);
    }
    public static PickerLayout Materials(UiRect area,int count,int page,bool replacing=false)
    {
        int top=replacing?88:32;
        int rows=Math.Max(1,(area.Height-top-56)/48),pages=Math.Max(1,(count+rows-1)/rows);page=Math.Clamp(page,0,pages-1);
        var cells=Enumerable.Range(page*rows,Math.Max(0,Math.Min(rows,count-page*rows)))
            .Select((i,n)=>(i,new UiRect(area.X+12,area.Y+top+n*48,area.Width-24,44))).ToArray();
        int half=(area.Width-32)/2;
        return new(cells,new(area.X+12,area.Bottom-44,half,44),new(area.X+20+half,area.Bottom-44,half,44),
            new(area.X+12,area.Y+4,area.Width-24,24),page,pages){Source=replacing?new(area.X+12,area.Y+32,area.Width-24,44):default};
    }
}
