#if BEADS_LITE
using System.Collections;
using System.Reflection;
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BetterBeads.Runtime;

/// <summary>Owns the decoder's single straight-alpha buffer. No full-size Color/uint copy or GPU image.</summary>
internal sealed class RgbaImagePixels:IReadOnlyList<uint>
{
    private readonly byte[] bytes;
    internal RgbaImagePixels(byte[] bytes)=>this.bytes=bytes;
    public int Count=>bytes.Length/4;
    public uint this[int index]{get{int i=checked(index*4);return (uint)bytes[i]<<24|(uint)bytes[i+1]<<16|(uint)bytes[i+2]<<8|bytes[i+3];}}
    public IEnumerator<uint> GetEnumerator(){for(int i=0;i<Count;i++)yield return this[i];}
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}
internal sealed record CpuImportedImage(RgbaImagePixels Pixels,int Width,int Height,Color[] Preview,int PreviewWidth,int PreviewHeight);
internal static class ImageCpuDecoder
{
    internal const int PreviewMaxSide=512;
    private static readonly SemaphoreSlim decodeGate=new(1,1);
    // Use the game's bundled CPU decoder, without a new binary or platform-specific dependency.
    private static readonly Type Result=typeof(Texture2D).Assembly.GetType("StbImageSharp.ImageResult",true)!;
    private static readonly Type Components=typeof(Texture2D).Assembly.GetType("StbImageSharp.ColorComponents",true)!;
    private static readonly MethodInfo Decode=Result.GetMethod("FromMemory",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(byte[]),Components},null)!;
    internal static CpuImportedImage Load(string path,CancellationToken token)
    {
        // A cancelled native decode cannot be interrupted; never run another full decode alongside it.
        decodeGate.Wait(token);
        try{return LoadCore(path,token);}finally{decodeGate.Release();}
    }
    private static CpuImportedImage LoadCore(string path,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var stream=File.OpenRead(path);
        if(stream.Length>ImageBlueprintImport.MaxFileBytes||!ImageFileProbe.TryDimensions(stream,Path.GetExtension(path),out int w,out int h))
            throw new InvalidDataException("Image exceeds the import limits or has an invalid header.");
        var packed=new byte[(int)stream.Length];int offset=0;
        while(offset<packed.Length){token.ThrowIfCancellationRequested();int read=stream.Read(packed,offset,Math.Min(65536,packed.Length-offset));if(read==0)throw new EndOfStreamException();offset+=read;}
        token.ThrowIfCancellationRequested();
        var result=Decode.Invoke(null,new[]{(object)packed,Enum.ToObject(Components,4)})!;
        token.ThrowIfCancellationRequested();
        int width=(int)Result.GetProperty("Width")!.GetValue(result)!,height=(int)Result.GetProperty("Height")!.GetValue(result)!;
        var raw=(byte[])Result.GetProperty("Data")!.GetValue(result)!;
        if(width!=w||height!=h||raw.Length!=(long)w*h*4)throw new InvalidDataException("Decoded image differs from the validated dimensions.");
        var pixels=new RgbaImagePixels(raw);
        double ratio=Math.Min(1,PreviewMaxSide/(double)Math.Max(w,h));
        int pw=Math.Max(1,(int)(w*ratio)),ph=Math.Max(1,(int)(h*ratio));var preview=new Color[pw*ph];
        for(int y=0;y<ph;y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x=0;x<pw;x++){uint c=pixels[(int)((y+.5)*h/ph)*w+(int)((x+.5)*w/pw)];preview[y*pw+x]=Color.FromNonPremultiplied((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);}
        }
        return new(pixels,w,h,preview,pw,ph);
    }
}
#endif
