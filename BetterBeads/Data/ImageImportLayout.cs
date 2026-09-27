namespace BetterBeads.Data;

/// <summary>Import page geometry shared by drawing, hit targets and offline checks.</summary>
public sealed record ImageImportLayout(bool Compact,bool Wide,UiRect File,UiRect Folder,
    UiRect Original,UiRect Converted,UiRect Status,UiRect[] LeftOptions,UiRect[] RightOptions,
    UiRect Cancel,UiRect Apply,UiRect Back,UiRect FolderHint,UiRect Previous,UiRect Next,
    int FileRows)
{
    public static ImageImportLayout Calculate(UiRect area)
    {
        int gap=8,half=(area.Width-gap)/2;
        bool compact=area.Height<360,wide=!compact&&area.Width>=620&&area.Height>=480;
        int bottom=area.Bottom-(compact?44:52);
        int optionY=compact?area.Bottom-102:bottom-144;
        int previewTop=area.Y+(compact?50:52);
        int previewHeight=compact?Math.Max(44,area.Height-184):Math.Max(40,optionY-previewTop-40);
        int settingTop=compact?area.Y+8:optionY;
        int spacing=compact?42:48,buttonHeight=compact?38:42;
        var left=Enumerable.Range(0,compact?4:3).Select(i=>new UiRect(area.X,settingTop+i*spacing,half,buttonHeight)).ToArray();
        var right=left.Select(r=>new UiRect(area.X+half+gap,r.Y,half,r.Height)).ToArray();
        var first=new UiRect(area.X,previewTop,wide?half:area.Width,previewHeight);
        var second=wide?new UiRect(area.X+half+gap,previewTop,half,previewHeight):first;
        var cancel=new UiRect(area.X,bottom,half,compact?40:44);
        return new(compact,wide,new(area.X,area.Y,half,42),new(area.X+half+gap,area.Y,half,42),
            first,second,new(area.X,optionY-24,area.Width,24),left,right,cancel,
            new(area.X+half+gap,bottom,half,cancel.Height),LibraryLayout.LiteBack(area),
            new(area.X,area.Bottom-108,area.Width,34),new(area.X,area.Bottom-52,72,44),
            new(area.Right-72,area.Bottom-52,72,44),Math.Max(1,(area.Height-172)/48));
    }
}
