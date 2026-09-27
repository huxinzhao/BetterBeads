using System.Buffers.Binary;

namespace BetterBeads.Data;

/// <summary>Read dimensions without allocating a decoded image. The decoder remains authoritative.</summary>
public static class ImageFileProbe
{
    public static bool TryDimensions(Stream stream,string extension,out int width,out int height)
    {
        width=height=0;
        if(!stream.CanRead||!stream.CanSeek)return false;
        long position=stream.Position;
        try
        {
            bool found=extension.ToLowerInvariant() switch
            {
                ".png"=>Png(stream,out width,out height),
                ".jpg" or ".jpeg"=>Jpeg(stream,out width,out height),
                _=>false
            };
            return found&&width>0&&height>0&&width<=ImageBlueprintImport.MaxSide&&height<=ImageBlueprintImport.MaxSide
                &&(long)width*height<=ImageBlueprintImport.MaxPixels;
        }
        catch(Exception){return false;}
        finally{stream.Position=position;}
    }
    private static bool Png(Stream stream,out int width,out int height)
    {
        width=height=0;Span<byte> header=stackalloc byte[24];
        if(stream.Read(header)!=24||!header[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
            ||!header.Slice(12,4).SequenceEqual(new byte[]{73,72,68,82}))return false;
        width=BinaryPrimitives.ReadInt32BigEndian(header.Slice(16,4));
        height=BinaryPrimitives.ReadInt32BigEndian(header.Slice(20,4));return true;
    }
    private static bool Jpeg(Stream stream,out int width,out int height)
    {
        width=height=0;
        if(stream.ReadByte()!=255||stream.ReadByte()!=216)return false;
        while(stream.Position<stream.Length)
        {
            int prefix=stream.ReadByte();if(prefix!=255)return false;
            int marker;do{marker=stream.ReadByte();}while(marker==255);
            if(marker<0||marker is 217 or 218)return false;
            if(marker is 1 or >=208 and <=215)continue;
            int high=stream.ReadByte(),low=stream.ReadByte();
            if(high<0||low<0)return false;
            int length=high*256+low;
            if(length<2||length-2>stream.Length-stream.Position)return false;
            if(marker is >=192 and <=195 or >=197 and <=199 or >=201 and <=203 or >=205 and <=207)
            {
                if(length<7)return false;
                stream.ReadByte();int h1=stream.ReadByte(),h2=stream.ReadByte(),w1=stream.ReadByte(),w2=stream.ReadByte();
                height=h1*256+h2;width=w1*256+w2;return true;
            }
            stream.Seek(length-2,SeekOrigin.Current);
        }
        return false;
    }
}
