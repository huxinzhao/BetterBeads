using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetterBeads.Data;

// Share codes contain artwork only. Save identity, revision, names and progress never cross saves.
public static class BlueprintSharing
{
    private const string Prefix="BB1.";
    private const string VerticalPrefix="BB2.";
    private const string DecorationPrefix="BB3.";
    private sealed class Payload
    {
        public string Template {get;set;}="";
        public string Metal {get;set;}="";
        public uint[] Colors {get;set;}=Array.Empty<uint>();
        public int[] Cells {get;set;}=Array.Empty<int>();
        public bool VerticalSword {get;set;}
        public uint BackgroundRgba {get;set;}=0xEADFC6FF;
    }
    public static string Encode(Blueprint design)
    {
        if(!SimpleCrafting.Supported(design))throw new ArgumentException("Unsupported design",nameof(design));
        var grid=design.Views["front"];
        string metal=SimpleCrafting.IsWeapon(design.Use)?SimpleCrafting.Metal(design):"";
        if(SimpleCrafting.IsWeapon(design.Use)&&!SimpleCrafting.Metals.Contains(metal))throw new ArgumentException("Unsupported weapon material",nameof(design));
        var colors=new List<uint>();var lookup=new Dictionary<uint,int>();var cells=new int[grid.Cells.Count];
        for(int i=0;i<cells.Length;i++)
        {
            var bead=grid.Cells[i];if(bead is null)continue;
            if((bead.Rgba&255)!=255)throw new ArgumentException("A bead must be opaque",nameof(design));
            if(!lookup.TryGetValue(bead.Rgba,out int id)){id=colors.Count+1;lookup[bead.Rgba]=id;colors.Add(bead.Rgba);}
            cells[i]=id;
        }
        bool vertical=design.Use==ProductUse.Sword&&design.SwordOrientation==SwordOrientation.Vertical;
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(new Payload{Template=design.TemplateId,Metal=metal,Colors=colors.ToArray(),Cells=cells,VerticalSword=vertical,BackgroundRgba=design.BackgroundRgba});
        using var compressed=new MemoryStream();
        compressed.Write(SHA256.HashData(data),0,8);
        using(var zipper=new BrotliStream(compressed,CompressionLevel.Optimal,true))zipper.Write(data);
        return (SimpleCrafting.IsDecoration(design.Use)?DecorationPrefix:vertical?VerticalPrefix:Prefix)+Convert.ToBase64String(compressed.ToArray()).TrimEnd('=').Replace('+','-').Replace('/','_');
    }
    public static bool TryDecode(string? code,out Blueprint? design)
    {
        design=null;
        if(code is null)return false;
        code=code.Trim();bool verticalCode=code.StartsWith(VerticalPrefix,StringComparison.Ordinal);
        bool decorationCode=code.StartsWith(DecorationPrefix,StringComparison.Ordinal);
        if(!verticalCode&&!decorationCode&&!code.StartsWith(Prefix,StringComparison.Ordinal)||code.Length>20000)return false;
        try
        {
            string body=code[(verticalCode?VerticalPrefix.Length:decorationCode?DecorationPrefix.Length:Prefix.Length)..].Replace('-','+').Replace('_','/');
            byte[] packed=Convert.FromBase64String(body.PadRight((body.Length+3)/4*4,'='));
            if(packed.Length<10||packed.Length>15000)return false;
            using var input=new MemoryStream(packed,8,packed.Length-8);
            using var zipper=new BrotliStream(input,CompressionMode.Decompress);
            using var data=new MemoryStream();
            byte[] buffer=new byte[4096];int count;
            while((count=zipper.Read(buffer))>0){if(data.Length+count>30000)return false;data.Write(buffer,0,count);}
            byte[] raw=data.ToArray();
            if(!SHA256.HashData(raw).AsSpan(0,8).SequenceEqual(packed.AsSpan(0,8)))return false;
            var payload=JsonSerializer.Deserialize<Payload>(raw);
            if(payload is null||payload.Template is null||payload.Metal is null||payload.Colors is null||payload.Cells is null||payload.Colors.Length>1024
                ||payload.VerticalSword!=verticalCode
                ||decorationCode&&(payload.BackgroundRgba&255)!=255
                ||payload.Colors.Any(c=>(c&255)!=255)||payload.Colors.Distinct().Count()!=payload.Colors.Length
                ||payload.Template is not (SimpleCrafting.Picture16 or SimpleCrafting.Picture32 or SimpleCrafting.Ornament or SimpleCrafting.LargeOrnament
                    or SimpleCrafting.Wallpaper16 or SimpleCrafting.Wallpaper32 or SimpleCrafting.Flooring16 or SimpleCrafting.Flooring32
                    or SimpleCrafting.Sword or SimpleCrafting.Sword24 or SimpleCrafting.Sword32
                    or SimpleCrafting.Dagger or SimpleCrafting.Dagger24 or SimpleCrafting.Dagger32
                    or SimpleCrafting.Hammer or SimpleCrafting.Hammer24 or SimpleCrafting.Hammer32)
                ||payload.Cells.Any(i=>i<0||i>payload.Colors.Length))return false;
            var result=SimpleCrafting.Blank(payload.Template);
            if(SimpleCrafting.IsDecoration(result.Use)!=decorationCode)return false;
            if(decorationCode)result.BackgroundRgba=payload.BackgroundRgba;
            if(verticalCode)
            {
                if(result.Use!=ProductUse.Sword)return false;
                result.SwordOrientation=SwordOrientation.Vertical;
            }
            if(payload.Cells.Length!=result.Views["front"].Cells.Count)return false;
            if(SimpleCrafting.IsWeapon(result.Use))
            {
                if(!SimpleCrafting.Metals.Contains(payload.Metal))return false;
                result.SupplementaryMaterials.Clear();result.SupplementaryMaterials[payload.Metal]=0;
            }
            else if(payload.Metal.Length>0)return false;
            string material=SimpleCrafting.IsWeapon(result.Use)?payload.Metal:"decoration";
            var cells=result.Views["front"].Cells;
            for(int i=0;i<cells.Count;i++)if(payload.Cells[i]>0)
            {uint color=payload.Colors[payload.Cells[i]-1];cells[i]=new BeadCell{Rgba=color,ColorId=BeadPalette.Id(color),MaterialId=material};}
            if(!SimpleCrafting.Supported(result))return false;
            design=result;return true;
        }
        catch(Exception e) when(e is FormatException or IOException or InvalidDataException or InvalidOperationException or JsonException or ArgumentException or OverflowException){return false;}
    }
}

// SVG keeps every bead sharp at any print size and includes exact RGB plus optional MARD reference.
public static class BlueprintColorChart
{
    public static string Create(Blueprint design,MardPaletteReference reference)
    {
        if(!SimpleCrafting.Supported(design))throw new ArgumentException("Unsupported design",nameof(design));
        var grid=design.Views["front"];
        var colors=grid.Cells.Where(c=>c is not null).Select(c=>c!.Rgba).Distinct().ToArray();
        var ids=colors.Select((color,index)=>(color,index:index+1)).ToDictionary(p=>p.color,p=>p.index);
        int columns=Math.Min(4,Math.Max(1,(colors.Length+31)/32));
        int legendRows=(colors.Length+columns-1)/columns;
        int board=grid.Width*40,legendWidth=columns*264,width=Math.Max(board+32,legendWidth+32);
        int height=96+board+Math.Max(80,legendRows*28+56);
        var svg=new StringBuilder(30000);
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\"><rect width=\"100%\" height=\"100%\" fill=\"#F7E8C4\"/>");
        svg.Append($"<text x=\"16\" y=\"34\" font-family=\"sans-serif\" font-size=\"21\" font-weight=\"bold\" fill=\"#49301D\">{Escape(design.Name.Length==0?"Better Beads":design.Name)}</text>");
        svg.Append($"<text x=\"16\" y=\"65\" font-family=\"sans-serif\" font-size=\"16\" fill=\"#684729\">{grid.Width} × {grid.Height} · {Escape(design.Use.ToString())} · MARD 221 reference</text>");
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)
        {
            int px=16+x*40,py=80+y*40;
            svg.Append($"<rect x=\"{px}\" y=\"{py}\" width=\"40\" height=\"40\" fill=\"#FFF9E9\" stroke=\"#C7AE85\"/>");
            var bead=grid.Cells[y*grid.Width+x];if(bead is null)continue;
            string hex=Rgb(bead.Rgba);int id=ids[bead.Rgba];
            svg.Append($"<circle cx=\"{px+20}\" cy=\"{py+17}\" r=\"14\" fill=\"{hex}\" stroke=\"#725C45\" stroke-width=\"1.3\"/>");
            svg.Append($"<circle cx=\"{px+20}\" cy=\"{py+17}\" r=\"5\" fill=\"#FFFFFF\" fill-opacity=\".32\"/>");
            svg.Append($"<text x=\"{px+20}\" y=\"{py+36}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"10\" font-weight=\"bold\" fill=\"#49301D\">{id}</text>");
        }
        int start=96+board;
        svg.Append($"<text x=\"16\" y=\"{start+4}\" font-family=\"sans-serif\" font-size=\"16\" font-weight=\"bold\" fill=\"#49301D\">Colors · exact RGB · nearest MARD 221</text>");
        for(int i=0;i<colors.Length;i++)
        {
            int x=16+(i/Math.Max(1,legendRows))*264,y=start+24+(i%Math.Max(1,legendRows))*28;
            string hex=Rgb(colors[i]);var match=reference.Match(colors[i]);
            svg.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"22\" height=\"22\" rx=\"4\" fill=\"{hex}\" stroke=\"#725C45\"/>");
            svg.Append($"<text x=\"{x+30}\" y=\"{y+17}\" font-family=\"sans-serif\" font-size=\"15\" fill=\"#49301D\">{i+1}  {hex}  {(!match.Exact?"≈ ":"")}{Escape(match.Code)}</text>");
        }
        svg.Append("</svg>");return svg.ToString();
    }
    private static string Rgb(uint rgba)=>$"#{rgba>>8:X6}";
    private static string Escape(string value)=>System.Security.SecurityElement.Escape(value)??"";
}
