using BetterBeads.Data;

internal static class ImageImportChecks
{
    public static void Run(Action<bool,string> check)
    {
        uint red=0xEE4444FF,blue=0x4466DDFF;
        uint[] exact={red,0,0,blue};
        var direct=ImageBlueprintImport.Create(exact,2,2,new(SimpleCrafting.Picture16));
        var grid=direct.Design.Views["front"];
        check(!direct.Reduced&&!direct.Restored&&grid.Cells[7*16+7]?.Rgba==red&&grid.Cells[8*16+8]?.Rgba==blue
            &&grid.Cells[7*16+8] is null&&direct.Design.Reference is null,"exact pixels and transparency remain unchanged");
        var enlarged=new uint[64*64];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)enlarged[y*64+x]=x<32&&y<32?red:0;
        check(ImageBlueprintImport.RestorableScale(enlarged,64,64)==32,"detect exact integer enlargement");
        var restored=ImageBlueprintImport.Create(enlarged,64,64,new(SimpleCrafting.Picture16));
        check(restored.Restored&&!restored.Reduced&&restored.WorkingWidth==2&&restored.Design.Views["front"].Cells.Count(c=>c is not null)==1,
            "restore enlarged sprite before drawing");
        var raw=ImageBlueprintImport.Create(enlarged,64,64,new(SimpleCrafting.Picture16,RestorePixels:false,Outline:false));
        check(raw.Reduced&&!raw.Restored&&raw.Design.Views["front"].Cells.Count(c=>c is not null)>1,"manual original-resolution option scales instead");
        var image=new uint[64*32];
        for(int y=8;y<24;y++)for(int x=12;x<52;x++)image[y*64+x]=x<32?red:blue;
        var outlined=ImageBlueprintImport.Create(image,64,32,new(SimpleCrafting.Picture32,RestorePixels:false));
        var bare=ImageBlueprintImport.Create(image,64,32,new(SimpleCrafting.Picture32,RestorePixels:false,Outline:false));
        var colors=outlined.Design.Views["front"].Cells.Where(c=>c is not null).Select(c=>c!.Rgba).Distinct().ToArray();
        check(outlined.Reduced&&outlined.Beads>bare.Beads&&colors.Any(c=>c!=red&&c!=blue),"lossy reduction adds an exterior colored rim");
        check(colors.Where(c=>c!=red&&c!=blue).All(c=>ColorDifference.FromRgba(c).L<ColorDifference.FromRgba(red).L+5),"rim is derived from nearby subject colors");
        var pale=new uint[64*64];for(int y=8;y<56;y++)for(int x=8;x<56;x++)pale[y*64+x]=0xEEE9E1FF;
        var paleResult=ImageBlueprintImport.Create(pale,64,64,new(SimpleCrafting.Picture32,RestorePixels:false));
        check(paleResult.Design.Views["front"].Cells.Where(c=>c is not null).All(c=>ColorDifference.FromRgba(c!.Rgba).L>70),"light subject has a light outline");
        uint white=0xFFFFFFFF;var backing=Enumerable.Repeat(white,20*20).ToArray();
        for(int y=4;y<16;y++)for(int x=4;x<16;x++)backing[y*20+x]=red;
        backing[10*20+10]=white;
        var removed=ImageBlueprintImport.Create(backing,20,20,new(SimpleCrafting.Picture32,RemoveBackground:true));
        check(removed.Design.Views["front"].Cells[6*32+6] is null&&removed.Design.Views["front"].Cells[16*32+16]?.Rgba==white,
            "remove only boundary-connected plain background");
        foreach(string id in new[]{SimpleCrafting.Ornament,SimpleCrafting.LargeOrnament,SimpleCrafting.Sword,SimpleCrafting.Dagger,SimpleCrafting.Hammer})
        {
            var result=ImageBlueprintImport.Create(new[]{red},1,1,new(id));
            check(SimpleCrafting.Supported(result.Design)&&result.Design.Views["front"].Cells.Single(c=>c is not null)?.MaterialId
                ==(SimpleCrafting.IsWeapon(result.Design.Use)?"copper":"decoration"),"supported imported template and material "+id);
        }
        var old=direct.Design.Copy();var copy=direct.Design.Copy(true);
        check(old.Id!=copy.Id&&copy.Revision==0&&copy.Name.Length==0,"each import opens a new unnamed design");
        var doc=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),SimpleCrafting.Catalog(),"front");
        check(doc.TryOpenImported(copy)&&doc.IsDirty&&!doc.TryOpen(SimpleCrafting.Blank(SimpleCrafting.Picture16)),
            "import remains unsaved and requires save/discard before switching or closing");
        doc.DiscardChanges();
        check(!doc.IsDirty&&doc.Snapshot().Id==copy.Id&&doc.Snapshot().Views["front"].Cells.All(c=>c is null),
            "discarded import returns to blank baseline");
        check(doc.TryOpen(old)&&!doc.IsDirty,"opening a saved design remains clean");
        var staged=ImageBlueprintImport.Prepare(enlarged,64,64,false,24);
        check(ReferenceEquals(staged.Pixels,enlarged)&&staged.ExactScale==32
            &&ImageBlueprintImport.Create(staged,new(SimpleCrafting.Picture16)).Restored,
            "unchanged source shares prepared pixels across preview settings");
        var removedBackground=ImageBlueprintImport.Prepare(backing,20,20,true,24);
        check(!ReferenceEquals(removedBackground.Pixels,backing)&&backing[0]==white,
            "background preparation does not mutate source pixels");
        var png=new byte[24];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(png,0);
        new byte[]{73,72,68,82}.CopyTo(png,12);png[19]=32;png[23]=16;
        using(var stream=new MemoryStream(png))check(ImageFileProbe.TryDimensions(stream,".png",out int w,out int h)&&w==32&&h==16&&stream.Position==0,
            "PNG dimensions checked before decoding");
        png[16]=1;
        using(var stream=new MemoryStream(png))check(!ImageFileProbe.TryDimensions(stream,".png",out _,out _),"oversized PNG rejected before decoding");
        byte[] jpeg={255,216,255,192,0,7,8,0,16,0,32,255,217};
        using(var stream=new MemoryStream(jpeg))check(ImageFileProbe.TryDimensions(stream,".jpg",out int w,out int h)&&w==32&&h==16,
            "JPEG dimensions checked before decoding");
        foreach(var (w,h) in new[]{(480,420),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(w,h,32);
            var bench=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
            var area=LibraryLayout.LiteArea(bench);var layout=ImageImportLayout.Calculate(area);
            check(bench.Frame.Contains(layout.Original)&&bench.Frame.Contains(layout.Cancel)&&bench.Frame.Contains(layout.Apply)
                &&!layout.Original.Overlaps(layout.Status)&&!layout.Status.Overlaps(layout.Cancel)
                &&layout.LeftOptions.Zip(layout.RightOptions).All(pair=>!pair.First.Overlaps(pair.Second)),
                "import preview and buttons fit "+w+"x"+h);
        }
    }
}
