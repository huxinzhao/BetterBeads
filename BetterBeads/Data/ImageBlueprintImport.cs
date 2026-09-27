namespace BetterBeads.Data;

public sealed record ImageImportSettings(string TemplateId, bool RestorePixels = true,
    bool RemoveBackground = false, int BackgroundTolerance = 24, bool Outline = true);
public sealed record ImageImportResult(Blueprint Design, int SourceWidth, int SourceHeight,
    int WorkingWidth, int WorkingHeight, bool Restored, bool Reduced, bool HasHalfAlpha, int Beads);
internal sealed record ImageImportPrepared(uint[] Pixels,int Width,int Height,int ExactScale,bool HasHalfAlpha,bool HasTransparency);

/// <summary>Pure image-to-bead conversion. Source pixels are straight-alpha RGBA.</summary>
public static class ImageBlueprintImport
{
    public const int MaxFileBytes=20*1024*1024, MaxSide=4096, MaxPixels=16_000_000;
    public static int RestorableScale(uint[] pixels,int width,int height)=>RestorableScale(pixels,width,height,CancellationToken.None);
    private static int RestorableScale(uint[] pixels,int width,int height,CancellationToken token)
    {
        Validate(pixels,width,height);
        for(int factor=Math.Min(width,height);factor>=2;factor--)
        {
            if(width%factor!=0||height%factor!=0)continue;
            bool uniform=true;
            for(int y=0;y<height&&uniform;y++)for(int x=0;x<width;x++)
                {if(x==0)token.ThrowIfCancellationRequested();if(pixels[y*width+x]!=pixels[(y/factor*factor)*width+x/factor*factor]){uniform=false;break;}}
            if(uniform)return factor;
        }
        return 1;
    }
    public static ImageImportResult Create(uint[] source,int width,int height,ImageImportSettings settings)
    {
        return Create(Prepare(source,width,height,settings.RemoveBackground,settings.BackgroundTolerance),settings);
    }
    internal static ImageImportPrepared Prepare(uint[] source,int width,int height,bool removeBackground,int tolerance,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        Validate(source,width,height);
        if(tolerance is <0 or >255)throw new ArgumentOutOfRangeException(nameof(tolerance));
        var pixels=removeBackground?(uint[])source.Clone():source;
        if(removeBackground)RemoveConnectedBackground(pixels,width,height,tolerance,token);
        bool half=false,transparent=false;
        for(int i=0;i<pixels.Length;i++){if(i%4096==0)token.ThrowIfCancellationRequested();byte alpha=(byte)pixels[i];half|=alpha is >0 and <255;transparent|=alpha<128;}
        return new(pixels,width,height,RestorableScale(pixels,width,height,token),half,transparent);
    }
    internal static ImageImportResult Create(ImageImportPrepared prepared,ImageImportSettings settings,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        int width=prepared.Width,height=prepared.Height;
        int sourceWidth=width,sourceHeight=height;
        var design=SimpleCrafting.Blank(settings.TemplateId);
        if(!SimpleCrafting.Supported(design))throw new ArgumentException("Unsupported product category.");
        var grid=design.Views["front"];
        var pixels=prepared.Pixels;
        bool halfAlpha=prepared.HasHalfAlpha;
        int scale=settings.RestorePixels&& (width>grid.Width||height>grid.Height)?prepared.ExactScale:1;
        bool restored=scale>1;
        if(restored)
        {
            int w=width/scale,h=height/scale;
            var reduced=new uint[w*h];
            for(int y=0;y<h;y++){token.ThrowIfCancellationRequested();for(int x=0;x<w;x++)reduced[y*w+x]=pixels[(y*scale)*width+x*scale];}
            pixels=reduced;width=w;height=h;
        }
        bool shrink=width>grid.Width||height>grid.Height;
        // An opaque image has no exterior contour; don't draw a rectangle around it.
        bool contour=shrink&&settings.Outline&&prepared.HasTransparency;
        int usable=contour?grid.Width-2:grid.Width,usableHeight=contour?grid.Height-2:grid.Height;
        int outWidth=width,outHeight=height;
        if(shrink)
        {
            double ratio=Math.Min(usable/(double)width,usableHeight/(double)height);
            outWidth=Math.Max(1,(int)Math.Floor(width*ratio));
            outHeight=Math.Max(1,(int)Math.Floor(height*ratio));
        }
        int ox=(grid.Width-outWidth)/2,oy=(grid.Height-outHeight)/2;
        int beads=0;
        for(int y=0;y<outHeight;y++)for(int x=0;x<outWidth;x++)
        {
            token.ThrowIfCancellationRequested();
            uint rgba=shrink?AreaSample(pixels,width,height,x,y,outWidth,outHeight,token):pixels[y*width+x];
            if((byte)rgba<128)continue;
            rgba|=255u;
            grid.Cells[(oy+y)*grid.Width+ox+x]=Cell(rgba,design.Use);
            beads++;
        }
        if(contour)
        {
            // Flood-fill exterior empty cells, so enclosed holes remain untouched.
            var exterior=new bool[grid.Cells.Count];var queue=new Queue<int>();
            for(int x=0;x<grid.Width;x++){queue.Enqueue(x);queue.Enqueue((grid.Height-1)*grid.Width+x);}
            for(int y=0;y<grid.Height;y++){queue.Enqueue(y*grid.Width);queue.Enqueue(y*grid.Width+grid.Width-1);}
            while(queue.Count>0)
            {
                int i=queue.Dequeue();if(exterior[i]||grid.Cells[i] is not null)continue;exterior[i]=true;
                int x=i%grid.Width,y=i/grid.Width;
                if(x>0)queue.Enqueue(i-1);if(x+1<grid.Width)queue.Enqueue(i+1);
                if(y>0)queue.Enqueue(i-grid.Width);if(y+1<grid.Height)queue.Enqueue(i+grid.Width);
            }
            var rim=new List<(int Index,uint Rgba)>();
            for(int i=0;i<grid.Cells.Count;i++)if(exterior[i])
            {
                int x=i%grid.Width,y=i/grid.Width;
                var edge=(x>0?grid.Cells[i-1]:null)??(x+1<grid.Width?grid.Cells[i+1]:null)
                    ??(y>0?grid.Cells[i-grid.Width]:null)??(y+1<grid.Height?grid.Cells[i+grid.Width]:null);
                if(edge is not null)rim.Add((i,Darker(edge.Rgba)));
            }
            foreach(var (i,color) in rim){grid.Cells[i]=Cell(color,design.Use);beads++;}
        }
        return new(design,sourceWidth,sourceHeight,width,height,restored,shrink,halfAlpha,beads);
    }
    private static BeadCell Cell(uint rgba,ProductUse use)=>new(){Rgba=rgba,ColorId=BeadPalette.Id(rgba),MaterialId=SimpleCrafting.IsWeapon(use)?"copper":"decoration"};
    private static void Validate(uint[] pixels,int width,int height)
    {
        if(width<1||height<1||width>MaxSide||height>MaxSide||(long)width*height>MaxPixels||pixels.Length!=(long)width*height)
            throw new ArgumentException("Image dimensions are invalid or exceed the import limit.");
    }
    private static void RemoveConnectedBackground(uint[] pixels,int width,int height,int tolerance,CancellationToken token)
    {
        uint background=pixels[0];
        bool Match(uint p)=>Math.Abs((byte)(p>>24)-(byte)(background>>24))<=tolerance
            &&Math.Abs((byte)(p>>16)-(byte)(background>>16))<=tolerance
            &&Math.Abs((byte)(p>>8)-(byte)(background>>8))<=tolerance;
        var seen=new bool[pixels.Length];var queue=new Queue<int>();
        void Visit(int i)
        {
            if(seen[i])return;seen[i]=true;
            if(!Match(pixels[i]))return;
            pixels[i]=0;queue.Enqueue(i);
        }
        for(int x=0;x<width;x++){Visit(x);Visit((height-1)*width+x);}
        for(int y=0;y<height;y++){Visit(y*width);Visit(y*width+width-1);}
        int processed=0;
        while(queue.Count>0)
        {
            if((processed++&4095)==0)token.ThrowIfCancellationRequested();
            int i=queue.Dequeue(),x=i%width,y=i/width;
            if(x>0)Visit(i-1);if(x+1<width)Visit(i+1);
            if(y>0)Visit(i-width);if(y+1<height)Visit(i+width);
        }
    }
    private static uint AreaSample(uint[] pixels,int width,int height,int x,int y,int outWidth,int outHeight,CancellationToken token)
    {
        double left=x*width/(double)outWidth,right=(x+1)*width/(double)outWidth;
        double top=y*height/(double)outHeight,bottom=(y+1)*height/(double)outHeight;
        double cover=0,alpha=0,r=0,g=0,b=0;
        int firstX=(int)left,lastX=(int)Math.Ceiling(right),lastY=(int)Math.Ceiling(bottom);
        for(int sy=(int)top;sy<lastY;sy++)
        {
            token.ThrowIfCancellationRequested();
            double rowArea=Math.Min(bottom,sy+1)-Math.Max(top,sy);
            int row=sy*width;
            for(int sx=firstX;sx<lastX;sx++)
            {
                double area=(Math.Min(right,sx+1)-Math.Max(left,sx))*rowArea;
                if(area<=0)continue;uint p=pixels[row+sx];double a=(byte)p/255d,w=area*a;
                cover+=area;alpha+=w;r+=(byte)(p>>24)*w;g+=(byte)(p>>16)*w;b+=(byte)(p>>8)*w;
            }
        }
        if(alpha<=0)return 0;
        return ((uint)Math.Round(r/alpha)<<24)|((uint)Math.Round(g/alpha)<<16)|((uint)Math.Round(b/alpha)<<8)|(uint)Math.Round(alpha/cover*255);
    }
    private static uint Darker(uint rgba)
    {
        var lab=ColorDifference.FromRgba(rgba);double l=Math.Max(0,lab.L-8);
        static double Axis(double value)=>value>6d/29d?value*value*value:3*(6d/29d)*(6d/29d)*(value-4d/29d);
        static uint Channel(double value)
        {
            value=Math.Clamp(value,0,1);double encoded=value<=0.0031308?12.92*value:1.055*Math.Pow(value,1/2.4)-0.055;
            return (uint)Math.Clamp((int)Math.Round(encoded*255),0,255);
        }
        double fy=(l+16)/116,fx=fy+lab.A/500,fz=fy-lab.B/200;
        double x=Axis(fx)*0.95047,y=Axis(fy),z=Axis(fz)*1.08883;
        return (Channel(3.2404542*x-1.5371385*y-0.4985314*z)<<24)
            |(Channel(-0.969266*x+1.8760108*y+0.041556*z)<<16)
            |(Channel(0.0556434*x-0.2040259*y+1.0572252*z)<<8)|255;
    }
}
