namespace BetterBeads.Data;

/// <summary>Wrap at the existing font size, preferring word boundaries. Long tokens and Chinese can wrap at characters.</summary>
internal static class UiParagraph
{
    internal static string[][] Pages(IEnumerable<string[]> paragraphs,int capacity)
    {
        if(capacity<1)return Array.Empty<string[]>();
        var pages=new List<string[]>();var current=new List<string>();
        foreach(var lines in paragraphs)
        {
            if(lines.Length<=capacity&&current.Count+lines.Length>capacity)
            {pages.Add(current.ToArray());current.Clear();}
            foreach(var line in lines)
            {
                if(current.Count==capacity){pages.Add(current.ToArray());current.Clear();}
                current.Add(line);
            }
        }
        if(current.Count>0)pages.Add(current.ToArray());
        return pages.ToArray();
    }
    internal static string[] Wrap(string text,double width,Func<string,double> measure)
    {
        if(width<=0)return Array.Empty<string>();
        var result=new List<string>();
        foreach(var paragraph in text.Replace("\r","").Split('\n'))
        {
            string remaining=paragraph.Trim();
            if(remaining.Length==0){result.Add("");continue;}
            while(remaining.Length>0)
            {
                if(measure(remaining)<=width){result.Add(remaining);break;}
                int fit=0;
                for(int i=1;i<=remaining.Length;i++)
                {
                    if(i<remaining.Length&&char.IsHighSurrogate(remaining[i-1])&&char.IsLowSurrogate(remaining[i]))continue;
                    if(measure(remaining[..i])>width)break;
                    fit=i;
                }
                if(fit==0)fit=char.IsHighSurrogate(remaining[0])&&remaining.Length>1&&char.IsLowSurrogate(remaining[1])?2:1;
                int space=remaining.LastIndexOf(' ',fit-1,fit);
                // Keep a label with its numeric value instead of leaving a bare number on the next line.
                if(space>0&&space+1<remaining.Length&&(char.IsDigit(remaining[space+1])||remaining[space+1]=='×'))
                {int earlier=remaining.LastIndexOf(' ',space-1);if(earlier>0)space=earlier;}
                int end=space>0?space:fit;
                result.Add(remaining[..end].TrimEnd());remaining=remaining[end..].TrimStart();
            }
        }
        return result.ToArray();
    }
}
