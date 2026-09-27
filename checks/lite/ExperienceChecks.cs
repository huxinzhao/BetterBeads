using BetterBeads.Data;
using System.Text.Json;

internal static class ExperienceChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var view=new CanvasTransform();var box=new UiRect(20,80,320,320);
        view.Relayout(box,32,32);view.ZoomAt(140,190,1);view.Pan(47,-29);
        int zoom=view.Zoom,dx=view.OffsetX,dy=view.OffsetY;
        view.Relayout(box,32,32);
        check(view.Zoom==zoom&&view.OffsetX==dx&&view.OffsetY==dy,"library roundtrip preserves view exactly");
        double cx=(box.Width/2d-dx)/zoom,cy=(box.Height/2d-dy)/zoom;
        view.Relayout(new(100,140,640,640),32,32);
        check(Math.Abs((320d-view.OffsetX)/view.Zoom-cx)<0.06&&Math.Abs((320d-view.OffsetY)/view.Zoom-cy)<0.06&&view.Zoom==zoom*2,"resize preserves center bead and relative zoom");
        double atX=(230d-view.Viewport.X-view.OffsetX)/view.Zoom,atY=(250d-view.Viewport.Y-view.OffsetY)/view.Zoom;
        view.ZoomAt(230,250,1);
        check(Math.Abs((230d-view.Viewport.X-view.OffsetX)/view.Zoom-atX)<0.06&&Math.Abs((250d-view.Viewport.Y-view.OffsetY)/view.Zoom-atY)<0.06,"wheel zoom anchored to cursor");
        view.Pan(20000,-20000);view.Center(32,32);
        check(view.CellRect(0,0).X==100&&view.CellRect(31,31).Right==740,"fit restores full board after pan");
        var defaults=new WorkbenchSettings();
        check(!defaults.ReducedMotion&&defaults.HoverSounds&&defaults.ClickSounds&&defaults.BeadSounds&&defaults.SuccessSounds,"feedback defaults preserve motion and all sounds");
        foreach(int height in new[]{38,40,42,44,48})
        {
            var motion=new ButtonMotion(1,1,1.25f);var bounds=new UiRect(10,10,80,height);
            var normal=motion.Face(bounds,minimumHeight:36);
            var reduced=motion.Face(bounds,minimumHeight:36,reducedMotion:true);
            check(normal.Height>=Math.Min(height-4,36)&&reduced.Width==bounds.Width&&reduced.Height==height-4&&reduced.Y==bounds.Y+3,"short button padding and reduced motion "+height);
        }
        var delay=new HoverDelay();
        check(!delay.Ready("editor/save",0)&&!delay.Ready("editor/save",0.349)&&delay.Ready("editor/save",0.35),"tooltip waits 350ms");
        check(!delay.Ready("dye/save",0.36)&&!delay.Ready(null,2)&&!delay.Ready("editor/save",3),"page changes and leaving reset tooltip dwell");
        var feedback=new UiFeedbackState();
        feedback.Observe(box,25,85,0,"editor/color");
        check(feedback.Observe(box,26,85,1,"dye/color")==HoverFeedback.Enter,"same rectangle different control has separate feedback identity");
        check(feedback.Observe(box,26,85,2,"library/row")==HoverFeedback.None,"stationary page replacement stays silent");

        int reads=0;string[] records={"草莓","银河剑"};
        var search=new SearchIndex<string>(()=>{reads++;return records.ToArray();},s=>s);
        var all=search.Find("");for(int i=0;i<600;i++)search.Find("");search.Find("剑");search.Find("草");
        check(reads==1&&search.Find("剑").SequenceEqual(new[]{"银河剑"}),"stationary library and search reuse parsed records");
        records=new[]{"草莓","银河剑","南瓜"};search.Invalidate();
        check(search.Find("").Length==3&&reads==2,"mutation or entering refreshes library once");

        var cache=new DeferredCache<(Blueprint,int,bool,bool),object>(128);
        var design=SimpleCrafting.Blank(SimpleCrafting.Picture32);int creates=0,disposed=0;
        object Create(){creates++;return new object();}
        Func<object> create=Create;var first=cache.Get((design,0,true,true),create);long before=GC.GetAllocatedBytesForCurrentThread();bool same=true;
        for(int i=0;i<600;i++)same&=ReferenceEquals(first,cache.Get((design,0,true,true),create));
        long reuseBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        check(creates==1&&same,"600 frames compose and scan once");
        cache.Get((design,1,true,true),Create);cache.Get((design,1,false,true),Create);cache.Get((design,1,false,false),Create);cache.Get((design.Copy(),1,true,true),Create);
        check(creates==5,"frame revision, trim, framing and content snapshots invalidate preview independently");
        for(int i=0;i<130;i++)cache.Get((design,i+2,true,true),Create);
        check(disposed==0,"eviction defers release until draw batch ends");
        cache.ReleaseRetired(_=>disposed++);check(disposed==7,"preview capacity is 128");
        cache.Clear();cache.ReleaseRetired(_=>disposed++);check(disposed==135,"title cleanup releases every preview once");

        using var worker=new LatestWork<string>();int workCount=0;
        for(int i=0;i<30;i++){int value=i;worker.Request(_=>{Interlocked.Increment(ref workCount);return value.ToString();});}
        string? result=null;Exception? error=null;
        await Until(()=>worker.TryTake(out result,out error));
        check(result=="29"&&workCount==1&&error is null,"150ms debounce executes latest settings only");
        using var started=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        worker.Request(_=>{started.Set();release.Wait();return "obsolete";},0);
        await Until(()=>started.IsSet);worker.Request(_=>"latest",0);release.Set();
        await Until(()=>worker.TryTake(out result,out error));
        check(result=="latest","late noncooperative result cannot replace latest preview");
        worker.Request(_=>"closed");worker.Cancel();await Task.Delay(170);
        check(!worker.Pending&&!worker.TryTake(out result,out error),"closing import cancels publication");
        worker.Request(_=>throw new InvalidOperationException("bad source"),0);
        await Until(()=>worker.TryTake(out result,out error));
        check(error is InvalidOperationException&&result is null,"worker failure is observed and reported");
        using var canceled=new CancellationTokenSource();canceled.Cancel();bool stopped=false;
        try{ImageBlueprintImport.Prepare(new uint[16],4,4,false,24,canceled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"pixel preparation cooperatively cancels");
        var prepared=ImageBlueprintImport.Prepare(new uint[64],8,8,false,24);stopped=false;
        try{ImageBlueprintImport.Create(prepared,new(SimpleCrafting.Picture16),canceled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"pixel conversion cooperatively cancels");

        var catalog=SimpleCrafting.Catalog();var recipe=SimpleCrafting.Recipes().First(r=>r.Template.Id==SimpleCrafting.Picture16);
        var draft=SimpleCrafting.Blank(SimpleCrafting.Picture16);
        InventorySlot Wood(int count)=>new("wood","(O)388",count,999,RawMaterial:"decoration",Yield:1);
        var empty=ManufacturingTransaction.CheckAvailability(draft,recipe,catalog,new HashSet<string>(),new InventorySlot?[]{Wood(20),null},0);
        check(!empty.CanMake&&empty.AvailableRaw==20,"empty canvas still reports stock from same inventory snapshot");
        draft.Views["front"].Cells[0]=new(){Rgba=0xFFFFFFFF,ColorId="#FFFFFFFF",MaterialId="decoration"};
        var full=ManufacturingTransaction.CheckAvailability(draft,recipe,catalog,new HashSet<string>(),new InventorySlot?[]{Wood(20)},0);
        var fits=ManufacturingTransaction.CheckAvailability(draft,recipe,catalog,new HashSet<string>(),new InventorySlot?[]{Wood(1)},0);
        var lacks=ManufacturingTransaction.CheckAvailability(draft,recipe,catalog,new HashSet<string>(),new InventorySlot?[]{null},0);
        check(full.Failure==ManufacturingFailure.NoSpace&&full.AvailableRaw==20,"full bag reason shares stock check");
        check(fits.CanMake&&lacks.Report?.Materials.First().Needed==1&&lacks.AvailableRaw==0,"space freed by consuming materials and shortage match submission rules");

        var layouts=new List<object>();
        foreach(var size in new[]{(480,420),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(size.Item1,size.Item2,32);var layout=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
            var picker=ColorPickerLayout.Calculate(layout.Frame);
            check(!picker.Compact||picker.AfterCard.Bottom+8<=picker.Previous.Y,"compact dye preview has eight pixel footer gap");
            var import=ImageImportLayout.Calculate(LibraryLayout.LiteArea(layout));
            check(!import.Compact||import.LeftOptions[3].Bottom+8+24<=import.Apply.Y,"compact import busy and transparency note clear footer");
            layouts.Add(new{Width=size.Item1,Height=size.Item2,Scale=scale,Layout=layout,Picker=picker,Import=import});
        }
        var frames=new List<object>();ButtonMotion state=default;var rect=new UiRect(100,100,160,40);
        for(int i=0;i<180;i++)
        {
            double t=i/60d;bool hover=t>=0.5&&t<2.2,down=t>=1.2&&t<1.5;
            if(i==72)state=state with{Press=1,Spring=1,Velocity=0};state=state.Step(hover,down,true,1d/60);
            frames.Add(new{Time=t,Hover=state.Hover,Press=state.Press,Spring=state.Spring,Face=state.Face(rect,minimumHeight:36),Reduced=state.Face(rect,minimumHeight:36,reducedMotion:true),Stage=!hover?"移开":down?"按下":t<1.2?"进入":"松开回弹"});
        }
        Directory.CreateDirectory("art/experience-0.16.20");
        File.WriteAllText("art/experience-0.16.20/layouts.json",JsonSerializer.Serialize(layouts));
        File.WriteAllText("art/experience-0.16.20/motion.json",JsonSerializer.Serialize(frames));
        File.WriteAllText("art/experience-0.16.20/measurements.json",JsonSerializer.Serialize(new{StaticPreviewFrames=600,StaticPreviewBuilds=1,LibraryReads=reads,DebouncedConversions=workCount,CacheLoopAllocatedBytes=reuseBytes}));
    }
    private static async Task Until(Func<bool> condition)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(!condition())await Task.Delay(5,timeout.Token);
    }
}
