using System.Globalization;

namespace BetterBeads.Data;

/// <summary>Presentation-only text. Stable IDs, recipes and serialized data never use translated keys.</summary>
public static class ContentText
{
    public static Func<string,string?>? Resolve {get;set;}
    public static Action<string>? InvalidFormat {get;set;}
    private static readonly HashSet<string> warned=new();
    public static string Get(string key,string fallback)=>Resolve?.Invoke(key) is {Length:>0} value?value:fallback;
    public static string Format(string key,FormattableString fallback)
    {
        string format=Get(key,fallback.Format);
        try{return string.Format(CultureInfo.CurrentCulture,format,fallback.GetArguments());}
        catch(FormatException)
        {
            if(warned.Add(key))InvalidFormat?.Invoke(key);
            return fallback.ToString(CultureInfo.CurrentCulture);
        }
    }
}
