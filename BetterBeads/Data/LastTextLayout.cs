namespace BetterBeads.Data;

/// <summary>A bounded single-entry cache for text repeatedly laid out by a native menu.</summary>
internal sealed class LastTextLayout<TFont> where TFont:class
{
    private readonly Func<string,TFont,int,string> layout;
    private string? text;
    private TFont? font;
    private int width;
    private string result="";
    internal LastTextLayout(Func<string,TFont,int,string> layout)=>this.layout=layout;
    internal string Get(string value,TFont currentFont,int availableWidth)
    {
        if(value==text && ReferenceEquals(font,currentFont) && width==availableWidth)return result;
        // Publish cache keys only after successful layout; failed rendering must remain retryable.
        string next=layout(value,currentFont,availableWidth);
        text=value;font=currentFont;width=availableWidth;result=next;
        return result;
    }
    internal void Clear(){text=null;font=null;result="";width=0;}
}
