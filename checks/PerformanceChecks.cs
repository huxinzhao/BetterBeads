using System.Diagnostics;
using BetterBeads.Data;

internal static class PerformanceChecks
{
    // Optional, deterministic fixtures. This measures data work, not game FPS or GPU rendering.
    public static void Run()
    {
        const int iterations=500;
        var catalog=DefaultProcessing.Create();var unlocked=new HashSet<string>();
        foreach(var recipe in new[]{DefaultManufacturing.Recipes().First(),DefaultManufacturing.Recipes().Last(),ClothingTemplates.Recipes().First()})
        {
            var t=recipe.Template;bool clothing=ClothingTemplates.IsClothing(t.Use);
            var design=new Blueprint{Name="Benchmark",TemplateId=t.Id,Use=t.Use,
                Views=t.Views.ToDictionary(v=>v,v=>new BeadGrid{Width=t.Width,Height=t.Height,
                    Cells=Enumerable.Range(0,t.Width*t.Height).Select(_=>(BeadCell?)new BeadCell
                        {ColorId="white",Rgba=0xFFF7E8FF,MaterialId=clothing?null:"wood"}).ToList()})};
            if(clothing)design.WoolMaterials["wool"]=t.WoolBudget!.Value;
            var inventory=new InventorySlot?[]{new("stock",BeadItems.BeadId(clothing?"wool":"wood"),9999,9999,CanReceiveBeads:true),null};
            var full=Measure(()=>ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,100,"benchmark"),iterations);
            var light=Measure(()=>ManufacturingTransaction.CheckAvailability(design,recipe,catalog,unlocked,inventory,100),iterations);
            Console.WriteLine($"{t.Id}: full preview {full.Bytes:N0} B/op, {full.Microseconds:F1} us/op; availability {light.Bytes:N0} B/op, {light.Microseconds:F1} us/op; allocations reduced {100*(1-light.Bytes/(double)full.Bytes):F1}%");
        }
        var draft=ProductTemplates.DebugSample(true,false,"Dirty benchmark").Design;
        draft.Reference=new(){Width=256,Height=256,Pixels=Enumerable.Repeat(0xFFFFFFFFu,256*256).ToArray()};
        var editor=new EditorDocument(draft,catalog,"front");
        var saved=DesignStorage.Serialize(editor.Snapshot());
        var oldDirty=Measure(()=>{var copy=editor.Snapshot();copy.Revision=0;return DesignStorage.Serialize(copy)!=saved;},iterations);
        var newDirty=Measure(()=>editor.IsDirty,iterations);
        Console.WriteLine($"Idle dirty status (4 views + 256x256 reference): previous {oldDirty.Bytes:N0} B/op, {oldDirty.Microseconds:F1} us/op; cached {newDirty.Bytes:N0} B/op, {newDirty.Microseconds:F1} us/op");
        Console.WriteLine($"{iterations} measured iterations after warmup; offline data benchmark, not in-game FPS.");
    }
    private static (long Bytes,double Microseconds) Measure<T>(Func<T> action,int count)
    {
        for(int i=0;i<50;i++)action();
        long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
        for(int i=0;i<count;i++)action();
        timer.Stop();
        return ((GC.GetAllocatedBytesForCurrentThread()-before)/count,timer.Elapsed.TotalMilliseconds*1000/count);
    }
}
