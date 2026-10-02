using System.Globalization;

namespace BetterBeads.Data;

public sealed class MardColorEntry
{
    public string Code {get;set;}="";
    public string Hex {get;set;}="";
}

public sealed class MardPaletteDefinition
{
    public string Schema {get;set;}="";
    public string SourceId {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string SourceUrl {get;set;}="";
    public string License {get;set;}="";
    public List<MardColorEntry> Colors {get;set;}=new();
    public List<string> DefaultCodes {get;set;}=new();
}

public readonly record struct MardColorMatch(string Code,bool Exact);
internal readonly record struct MardColorSearchResult(string Code,uint Rgba);

public sealed class MardPaletteReference
{
    private sealed record Color(string Code,uint Rgba,LabColor Lab);
    private static readonly string[] fallbackCodes={"G8","G17","G13","G4","A14","A10","A6","A13","H5","H4","H23","H10","E13","E7","E12","E18","D22","D7","D6","D9"};
    private static readonly string[] fallbackHex={"713D2F","77544E","B2714B","DCB387","F75842","F47E38","FDA951","FEC667",
        "464648","878787","9B9C94","EBEBEB","B5026A","C53674","F693BF","F9C8DB","38389A","8758A9","B37BDC","D5B9F4"};
    private readonly Color[] colors;
    private readonly Dictionary<uint,MardColorMatch> matches=new();
    public uint[] DefaultColors {get;}
    public int ColorCount=>colors.Length;
    public int MatchCalculations {get;private set;}
    public static MardPaletteReference Current {get;private set;}=Fallback();

    private MardPaletteReference(Color[] colors,string[] defaultCodes)
    {
        this.colors=colors.OrderBy(c=>c.Code[0]).ThenBy(c=>int.Parse(c.Code[1..],CultureInfo.InvariantCulture)).ToArray();
        var byCode=colors.ToDictionary(c=>c.Code,StringComparer.Ordinal);
        DefaultColors=defaultCodes.Select(code=>byCode[code].Rgba).ToArray();
    }

    private static MardPaletteReference Fallback()
    {
        var colors=fallbackCodes.Zip(fallbackHex).Select(pair=>
        {
            uint rgba=(uint.Parse(pair.Second,NumberStyles.HexNumber,CultureInfo.InvariantCulture)<<8)|255;
            return new Color(pair.First,rgba,ColorDifference.FromRgba(rgba));
        }).ToArray();
        return new(colors,fallbackCodes);
    }

    public static bool TryCreate(MardPaletteDefinition? definition,out MardPaletteReference? palette)
    {
        palette=null;
        if(definition is null || definition.Schema!="mard221-v1" || definition.SourceId!="mard-221-alfonse-doudou"
            ||string.IsNullOrWhiteSpace(definition.SourceVersion)||string.IsNullOrWhiteSpace(definition.SourceUrl)||definition.License!="MIT"
            ||definition.Colors is null || definition.Colors.Count!=221 || definition.DefaultCodes is null || definition.DefaultCodes.Count!=20)return false;
        var colors=new List<Color>(221);var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in definition.Colors)
        {
            if(entry is null || !ValidCode(entry.Code) || !seen.Add(entry.Code) || entry.Hex is null || entry.Hex.Length!=7 || entry.Hex[0]!='#'
                ||!uint.TryParse(entry.Hex.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint rgb))return false;
            uint rgba=(rgb<<8)|255;
            colors.Add(new(entry.Code,rgba,ColorDifference.FromRgba(rgba)));
        }
        if(definition.DefaultCodes.Distinct(StringComparer.Ordinal).Count()!=20)return false;
        const string groups="GAHED";
        for(int row=0;row<5;row++)
        {
            double previous=-1;
            for(int column=0;column<4;column++)
            {
                string code=definition.DefaultCodes[row*4+column];
                if(!ValidCode(code)||code[0]!=groups[row])return false;
                var entry=colors.FirstOrDefault(c=>c.Code==code);
                if(entry is null || entry.Lab.L<=previous)return false;
                previous=entry.Lab.L;
            }
        }
        palette=new(colors.ToArray(),definition.DefaultCodes.ToArray());return true;
    }

    private static bool ValidCode(string? code)
        =>code is {Length:>=2 and <=3} && "ABCDEFGHM".Contains(code[0])
            && int.TryParse(code.AsSpan(1),NumberStyles.None,CultureInfo.InvariantCulture,out int number)&&number is >=1 and <=99;

    public static bool Configure(MardPaletteDefinition? definition)
    {
        bool valid=TryCreate(definition,out var configured);
        Current=valid?configured!:Fallback();
#if BEADS_LITE
        BeadPalette.SetDefaults(Current.DefaultColors);
#endif
        return valid;
    }

    public MardColorMatch Match(uint rgba)
    {
        rgba|=255;
        if(matches.TryGetValue(rgba,out var cached))return cached;
        MatchCalculations++;
        var exact=colors.FirstOrDefault(c=>c.Rgba==rgba);
        MardColorMatch result;
        if(exact is not null)result=new(exact.Code,true);
        else
        {
            var target=ColorDifference.FromRgba(rgba);
            double best=double.PositiveInfinity;string code="";
            foreach(var candidate in colors)
            {
                double distance=ColorDifference.Ciede2000(target,candidate.Lab);
                if(distance<best-1e-10){best=distance;code=candidate.Code;}
            }
            result=new(code,false);
        }
        if(matches.Count>=512)matches.Clear();
        matches[rgba]=result;return result;
    }

    public string Display(uint rgba)
    {
        var match=Match(rgba);
        return (match.Exact?"":"≈ ")+match.Code;
    }

    private static string NormalizeSearch(string? query)
    {
        string value=(query??"").Trim().ToUpperInvariant();
        if(value.Length>1 && "ABCDEFGHM".Contains(value[0])
            && value.Length>1 && int.TryParse(value.AsSpan(1),NumberStyles.None,CultureInfo.InvariantCulture,out int number))
            value=value[0]+number.ToString(CultureInfo.InvariantCulture);
        return value;
    }

    internal MardColorSearchResult? FindExact(string? query)
    {
        string code=NormalizeSearch(query);
        var color=colors.FirstOrDefault(c=>c.Code==code);
        return color is null?null:new MardColorSearchResult(color.Code,color.Rgba);
    }

    internal MardColorSearchResult[] Search(string? query)
    {
        string value=NormalizeSearch(query);
        return colors.Where(c=>c.Code.StartsWith(value,StringComparison.Ordinal))
            .OrderByDescending(c=>c.Code==value)
            .Select(c=>new MardColorSearchResult(c.Code,c.Rgba)).ToArray();
    }
}
