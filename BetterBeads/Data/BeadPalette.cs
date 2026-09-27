using System.Globalization;

namespace BetterBeads.Data;

public static class BeadPalette
{
#if BEADS_LITE
    // MARD 221 reference colors: brown, orange, gray, pink, purple; dark to light.
    public static uint[] Defaults {get;private set;}=new uint[]{0x713D2FFF,0x77544EFF,0xB2714BFF,0xDCB387FF,
        0xF75842FF,0xF47E38FF,0xFDA951FF,0xFEC667FF,
        0x464648FF,0x878787FF,0x9B9C94FF,0xEBEBEBFF,
        0xB5026AFF,0xC53674FF,0xF693BFFF,0xF9C8DBFF,
        0x38389AFF,0x8758A9FF,0xB37BDCFF,0xD5B9F4FF};
    public static void SetDefaults(IReadOnlyList<uint> values)
    {
        if(values.Count!=20)throw new ArgumentException("The Lite tray needs twenty colors.");
        Defaults=values.Select(v=>v|255u).ToArray();
    }
#else
    public static readonly uint[] Defaults={0x25242AFF,0xFFF7E8FF,0x969BA5FF,0x674833FF,
        0xD84E63FF,0xF19EB7FF,0xEAA344FF,0xF1C447FF,0x41AD6DFF,0x84BA8AFF,0x70D5E5FF,0xABBCCBFF,
        0x986BD4FF,0xBC76D0FF,0xAA734AFF,0xEBD7B3FF};
#endif
    public static string Id(uint rgba)=>"rgb."+(rgba>>8).ToString("X6");
    public static PaletteColor? Resolve(ProcessingCatalog catalog,string id)
    {
        if(id.StartsWith("rgb.",StringComparison.Ordinal) && id.Length==10
            && uint.TryParse(id.AsSpan(4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint rgb))
            return new(){Id=id,Rgba=(rgb<<8)|255,NameKey="palette.custom",InitiallyUnlocked=true};
        return catalog.Colors.FirstOrDefault(c=>c.Id==id);
    }
    public static List<uint> Normalize(IEnumerable<uint>? colors)
    {
        var result=colors?.Take(Defaults.Length).Select(c=>c|255u).ToList()??new();
        while(result.Count<Defaults.Length)result.Add(Defaults[result.Count]);
        return result;
    }
    public static uint FromHsv(double hue,double saturation,double value)
    {
        hue=((hue%360)+360)%360;saturation=Math.Clamp(saturation,0,1);value=Math.Clamp(value,0,1);
        double c=value*saturation,x=c*(1-Math.Abs(hue/60%2-1)),m=value-c;
        var (r,g,b)=hue switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};
        uint Channel(double n)=>(uint)Math.Round((n+m)*255);
        return (Channel(r)<<24)|(Channel(g)<<16)|(Channel(b)<<8)|255;
    }
    public static (double H,double S,double V) ToHsv(uint rgba)
    {
        double r=(byte)(rgba>>24)/255d,g=(byte)(rgba>>16)/255d,b=(byte)(rgba>>8)/255d;
        double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
        double h=d==0?0:max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);
        return ((h+360)%360,max==0?0:d/max,max);
    }
}
