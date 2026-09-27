using BetterBeads.Data;
using System.Text.Json;

internal static class OptimizationChecks
{
    public static void Run(Action<bool,string> check)
    {
        int calls=0;var cache=new DeferredCache<int,int>(2);
        Func<int,int> factory=k=>{calls++;return k+1;};
        for(int i=0;i<20;i++)cache.Get(1,factory);
        long before=GC.GetAllocatedBytesForCurrentThread();bool same=true;
        for(int i=0;i<600;i++)same&=cache.Get(1,factory)==2;
        long newBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        check(same&&calls==1,"keyed preview factory runs once across stationary frames");
        for(int i=0;i<20;i++)LegacyGet(cache,1);
        before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<600;i++)LegacyGet(cache,1);
        long legacyBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        check(newBytes<legacyBytes,"keyed factory avoids old per-call captured delegates");
        cache.Get(2,factory);cache.Get(3,factory);int disposed=0;cache.ReleaseRetired(_=>disposed++);
        check(disposed==1,"keyed cache still defers evicted resources");
        bool failed=false;try{cache.Get(4,_=>throw new Exception("expected"));}catch{failed=true;}
        check(failed&&cache.Get(3,factory)==4&&calls==3,"failed creation does not evict an existing preview");
        var memo=new BoundedMemo<string,int>(2);int reads=0;Func<string,int> measure=s=>{reads++;return s.Length;};
        for(int i=0;i<600;i++)memo.Get("中文按钮",measure);
        check(reads==1,"text and color memo hit paths do not recalculate");
        memo.Get("B",measure);memo.Get("C",measure);memo.Get("中文按钮",measure);
        check(reads==4,"derived-value memo stays bounded");
        memo.Clear();memo.Get("中文按钮",measure);check(reads==5,"asset invalidation recomputes derived values");

        var rng=new Random(21621);int compared=0;
        // The frozen pre-optimization flood fill and area sampler are the compatibility oracle.
        foreach(var dims in new[]{(1,1),(1,64),(64,1),(17,21),(73,49),(127,95)})
        foreach(int tolerance in new[]{0,24,128,255})
        {
            int w=dims.Item1,h=dims.Item2;var source=new uint[w*h];
            for(int i=0;i<source.Length;i++)source[i]=i%4==0?0xEEEEEEFFu:((uint)rng.NextInt64(1L<<32));
            source[0]=0xEEEEEEFFu;var beforeSource=(uint[])source.Clone();
            var expected=LegacyBackground(source,w,h,tolerance);
            var prepared=ImageBlueprintImport.Prepare(source,w,h,true,tolerance);
            check(prepared.Pixels.SequenceEqual(expected)&&source.SequenceEqual(beforeSource),"background equality and source immutability "+dims+"/"+tolerance);
            var result=ImageBlueprintImport.Create(prepared,new(SimpleCrafting.Picture16,RestorePixels:false,Outline:false));
            check(MatchesLegacySampling(result.Design,expected,w,h),"weighted sampling exact equality "+dims+"/"+tolerance);
            compared++;
        }
        // Closed holes, a single connected passage, and the maximum accepted tolerance.
        var ring=Enumerable.Repeat(0xFFFFFFFFu,64*64).ToArray();
        for(int y=10;y<54;y++)for(int x=10;x<54;x++)if(x is 10 or 53||y is 10 or 53)ring[y*64+x]=0xBB3344FF;
        foreach(bool passage in new[]{false,true})
        {
            if(passage)ring[10*64+32]=0xFFFFFFFF;
            check(ImageBlueprintImport.Prepare(ring,64,64,true,24).Pixels.SequenceEqual(LegacyBackground(ring,64,64,24)),"flood fill enclosed region / connected passage "+passage);
        }
        var white=Enumerable.Repeat(0xFFFFFFFFu,512*512).ToArray();
        LegacyBackground(white,512,512,24);ImageBlueprintImport.Prepare(white,512,512,true,24);
        before=GC.GetAllocatedBytesForCurrentThread();LegacyBackground(white,512,512,24);long oldFillBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        before=GC.GetAllocatedBytesForCurrentThread();ImageBlueprintImport.Prepare(white,512,512,true,24);long newFillBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        check(newFillBytes<oldFillBytes,"one queue entry per background pixel lowers allocation even including new preparation analysis");
        Directory.CreateDirectory("art/optimization-0.16.21");
        File.WriteAllText("art/optimization-0.16.21/measurements.json",JsonSerializer.Serialize(new{CacheFrames=600,OldCapturedFactoryBytes=legacyBytes,NewKeyedFactoryBytes=newBytes,BackgroundSize="512x512",OldBackgroundBytes=oldFillBytes,NewFullPreparationBytes=newFillBytes,ExactImageCases=compared}));
    }
    private static int LegacyGet(DeferredCache<int,int> cache,int key)=>cache.Get(key,()=>key+1);
    private static uint[] LegacyBackground(uint[] source,int width,int height,int tolerance)
    {
        var pixels=(uint[])source.Clone();uint background=pixels[0];
        bool Match(uint p)=>Math.Abs((byte)(p>>24)-(byte)(background>>24))<=tolerance&&Math.Abs((byte)(p>>16)-(byte)(background>>16))<=tolerance&&Math.Abs((byte)(p>>8)-(byte)(background>>8))<=tolerance;
        var seen=new bool[pixels.Length];var queue=new Queue<int>();
        for(int x=0;x<width;x++){queue.Enqueue(x);queue.Enqueue((height-1)*width+x);}
        for(int y=0;y<height;y++){queue.Enqueue(y*width);queue.Enqueue(y*width+width-1);}
        while(queue.Count>0)
        {
            int i=queue.Dequeue();if(seen[i])continue;seen[i]=true;if(!Match(pixels[i]))continue;
            pixels[i]=0;int x=i%width,y=i/width;
            if(x>0)queue.Enqueue(i-1);if(x+1<width)queue.Enqueue(i+1);if(y>0)queue.Enqueue(i-width);if(y+1<height)queue.Enqueue(i+width);
        }
        return pixels;
    }
    private static bool MatchesLegacySampling(Blueprint design,uint[] source,int width,int height)
    {
        bool shrink=width>16||height>16;double ratio=Math.Min(16d/width,16d/height);
        int w=shrink?Math.Max(1,(int)Math.Floor(width*ratio)):width,h=shrink?Math.Max(1,(int)Math.Floor(height*ratio)):height;
        var expected=new uint[256];int ox=(16-w)/2,oy=(16-h)/2;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            uint rgba=shrink?LegacySample(source,width,height,x,y,w,h):source[y*width+x];
            if((byte)rgba>=128)expected[(y+oy)*16+x+ox]=rgba|255u;
        }
        return design.Views["front"].Cells.Select(c=>c?.Rgba??0u).SequenceEqual(expected);
    }
    private static uint LegacySample(uint[] pixels,int width,int height,int x,int y,int outWidth,int outHeight)
    {
        double left=x*width/(double)outWidth,right=(x+1)*width/(double)outWidth,top=y*height/(double)outHeight,bottom=(y+1)*height/(double)outHeight;
        double cover=0,alpha=0,r=0,g=0,b=0;
        for(int sy=(int)top;sy<Math.Ceiling(bottom);sy++)for(int sx=(int)left;sx<Math.Ceiling(right);sx++)
        {
            double area=(Math.Min(right,sx+1)-Math.Max(left,sx))*(Math.Min(bottom,sy+1)-Math.Max(top,sy));
            if(area<=0)continue;uint p=pixels[sy*width+sx];double a=(byte)p/255d,w=area*a;
            cover+=area;alpha+=w;r+=(byte)(p>>24)*w;g+=(byte)(p>>16)*w;b+=(byte)(p>>8)*w;
        }
        return alpha<=0?0:((uint)Math.Round(r/alpha)<<24)|((uint)Math.Round(g/alpha)<<16)|((uint)Math.Round(b/alpha)<<8)|(uint)Math.Round(alpha/cover*255);
    }
}
