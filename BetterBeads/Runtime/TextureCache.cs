using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace BetterBeads.Runtime;

/// <summary>Disposable, bounded GPU cache. Serialized snapshots are the source of truth.</summary>
internal sealed class TextureCache : IDisposable
{
    private sealed record ScaleInfo(int Density);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Texture2D,ScaleInfo> densities=new();
    public static int Density(Texture2D texture)=>densities.TryGetValue(texture,out var info)?info.Density:1;
    public static void MarkDense(Texture2D texture)=>densities.Add(texture,new(BeadFinish.Density));
    private const int Capacity = 128;
    private readonly Dictionary<string, Texture2D> textures = new();
    private readonly Queue<string> insertionOrder = new();
    private readonly List<Texture2D> retired = new();

    public Texture2D Get(GraphicsDevice device, ProductRenderData render, string view)
    {
        var snapshot=render.Snapshot;
        var key = render.CacheKey(view,ArtResources.FrameRevision);
        if (textures.TryGetValue(key, out var cached)) return cached;
        bool decoration=SimpleCrafting.IsDecoration(snapshot.Design.Use);
        bool fused=FurnitureFinish.IsNew(snapshot.FurnitureVariantId)||decoration;
        bool atlas=view is "atlas" or "chair-front" or "furniture-menu";
        var grid = atlas ? ProductTemplates.CreateAtlas(snapshot)
            : snapshot.Design.Views[view is "weapon-icon" or "furniture-icon"?"front":view];
        if(atlas && SimpleCrafting.Wall(snapshot.Design.TemplateId))grid=fused?PaintingArt.Compact(grid,ArtResources.PaintingFrame()):PaintingArt.Compose(grid,ArtResources.PaintingFrame());
        var pixels = fused?Array.Empty<Color>():grid.Cells.Select(cell => cell is null ? Color.Transparent : new Color(
            (byte)(cell.Rgba >> 24), (byte)(cell.Rgba >> 16), (byte)(cell.Rgba >> 8), (byte)cell.Rgba)).ToArray();
        int width=grid.Width,height=grid.Height;
        if(fused)
        {
            var finished=BeadFinish.Bake(grid,ArtResources.Definition,view is "furniture-menu" or "furniture-icon");
            width=finished.Width;height=finished.Height;
            uint background=snapshot.Design.BackgroundRgba;
            pixels=finished.Pixels.Select(c=>
            {
                if(decoration&&(c&255)==0)c=background;
                return new Color((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);
            }).ToArray();
        }
        if(view=="weapon-icon" && width>16)
        {
            var source=pixels;int side=width;pixels=new Color[256];
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            {
                int sx=Math.Min(side-1,(int)((x+0.5)*side/16)),sy=Math.Min(side-1,(int)((y+0.5)*height/16));
                pixels[y*16+x]=source[sy*side+sx];
            }
            width=height=16;
        }
        if(view=="chair-front")for(int y=0;y<Math.Max(0,height-8);y++)Array.Clear(pixels,y*width,width);
        if(view=="atlas" && snapshot.Design.Use==ProductUse.Pants)
        {
            // Native index 0 contains both body types and every animation plus its inventory icon.
            // The native alpha mask preserves leg silhouettes; the design supplies the print color.
            var native=Game1.content.Load<Texture2D>("Characters/Farmer/pants");
            width=192;height=688;
            var mask=new Color[width*height];native.GetData(0,new Rectangle(0,0,width,height),mask,0,mask.Length);
            var print=pixels;pixels=new Color[mask.Length];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var c=print[(y%16)*16+x%16];
                // Empty print cells leave a neutral cloth base instead of holes in the legs.
                if(c.A==0)c=new Color(220,216,205);
                var m=mask[y*width+x];float shade=(m.R+m.G+m.B)/(3f*255f);
                pixels[y*width+x]=new Color((byte)(c.R*shade),(byte)(c.G*shade),(byte)(c.B*shade),m.A);
            }
        }
        var texture = new Texture2D(device, width, height);
        try { texture.SetData(pixels); }
        catch { texture.Dispose(); throw; }
        if(fused)MarkDense(texture);
        while (textures.Count >= Capacity)
        {
            var expired = insertionOrder.Dequeue();
            retired.Add(textures[expired]);
            textures.Remove(expired);
        }
        textures.Add(key, texture);
        insertionOrder.Enqueue(key);
        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in textures.Values) texture.Dispose();
        textures.Clear();
        insertionOrder.Clear();
        ReleaseRetired();
    }

    // SpriteBatch may still reference evicted textures until the frame finishes.
    public void ReleaseRetired()
    {
        foreach (var texture in retired) texture.Dispose();
        retired.Clear();
    }
}
