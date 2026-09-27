namespace BetterBeads.Data;

/// <summary>Versioned visual-only interface. Never changes recipes, material properties or saved pixels.</summary>
public sealed class ArtDefinition
{
    public int Schema { get; set; } = 1;
    public int BeadColumns { get; set; } = 8;
    public Dictionary<string,int> Beads { get; set; } = new();
    public Dictionary<string,int> Ui { get; set; } = new();
    public Dictionary<string,int> Icons { get; set; } = new();
    public Dictionary<string,int> Status { get; set; } = new();
    public string Ink { get; set; } = "392D32";
    public string MutedInk { get; set; } = "55493D";
    public string Canvas { get; set; } = "4D5158";
    public string CheckerLight { get; set; } = "E5DFD0";
    public string CheckerDark { get; set; } = "CFC8B8";
    public bool ShowBeadHoles { get; set; } = true;
    public bool ShowIcons { get; set; } = true;
    public bool UseNativeUi { get; set; } = true;
    public double FusedBeadStrength {get;set;}=0.65;
    public double[] FusedBeadMask {get;set;}={0.98,1.08,1.06,0.96,1.04,1.02,0.96,0.94,1.02,0.96,0.91,0.93,0.94,0.93,0.92,0.89};

    public bool IsValid() => Schema==1 && BeadColumns is >=1 and <=128
        &&double.IsFinite(FusedBeadStrength)&&FusedBeadStrength is >=0 and <=1
        &&FusedBeadMask is {Length:16}&&FusedBeadMask.All(v=>double.IsFinite(v)&&v is >=0.5 and <=1.5)
        && new[]{Ink,MutedInk,Canvas,CheckerLight,CheckerDark}.All(c=>c is {Length:6} && c.All(Uri.IsHexDigit))
        && new[]{Beads,Ui,Icons,Status}.All(m=>m is not null && m.Count<=4096
            && m.All(p=>!string.IsNullOrWhiteSpace(p.Key) && p.Key.Length<=128 && p.Value is >=0 and <4096));

    public static bool Fits(int index,int columns,int cell,int width,int height)
        => index>=0 && columns>0 && cell>0 && width>0 && height>0
        && (long)columns*cell<=width && ((long)index/columns+1)*cell<=height;

    public int BeadIndex(string id,int width,int height)
    {
        int index=Beads.GetValueOrDefault(id,-1);
        if(Fits(index,BeadColumns,16,width,height))return index;
        index=Beads.GetValueOrDefault("unknown",-1);
        return Fits(index,BeadColumns,16,width,height)?index:-1;
    }
}
