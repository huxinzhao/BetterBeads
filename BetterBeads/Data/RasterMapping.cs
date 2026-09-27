namespace BetterBeads.Data;

public sealed record RasterMapping(int Width,int Height,int OffsetX,int OffsetY,int SourceX,int SourceY,
    int SourceWidth,int SourceHeight,bool Scaled)
{
    public (int X,int Y) SourceAt(int x,int y)=>Scaled?(x*SourceWidth/Width,y*SourceHeight/Height):(x+SourceX,y+SourceY);
    public static RasterMapping Create(int sourceWidth,int sourceHeight,int targetWidth,int targetHeight,
        ImportSizing sizing,int cropX=0,int cropY=0)
    {
        if(new[]{sourceWidth,sourceHeight,targetWidth,targetHeight}.Any(n=>n<=0 || n>DesignStorage.MaxDimension)
            || !Enum.IsDefined(typeof(ImportSizing),sizing))throw new ArgumentException("Invalid raster size.");
        int width=sourceWidth,height=sourceHeight,sx=0,sy=0;
        switch(sizing)
        {
            case ImportSizing.Original:
                if(width>targetWidth || height>targetHeight)throw new ArgumentException("Choose explicit cropping or scaling.");
                break;
            case ImportSizing.Crop:
                if(cropX<0 || cropY<0 || cropX>=sourceWidth || cropY>=sourceHeight)throw new ArgumentException("Invalid crop origin.");
                sx=cropX;sy=cropY;width=Math.Min(targetWidth,sourceWidth-sx);height=Math.Min(targetHeight,sourceHeight-sy);
                break;
            case ImportSizing.FitNearest:
                double scale=Math.Min(targetWidth/(double)sourceWidth,targetHeight/(double)sourceHeight);
                width=Math.Max(1,(int)Math.Floor(sourceWidth*scale));height=Math.Max(1,(int)Math.Floor(sourceHeight*scale));
                break;
        }
        return new(width,height,(targetWidth-width)/2,(targetHeight-height)/2,sx,sy,sourceWidth,sourceHeight,sizing==ImportSizing.FitNearest);
    }
}
