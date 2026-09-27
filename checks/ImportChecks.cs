using BetterBeads.Data;

internal static class ImportChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();var unlocked=new HashSet<string>();
        check(PixelImport.FromPremultiplied(128,0,0,128)==0xFF000080 && PixelImport.FromPremultiplied(255,255,255,0)==0,
            "预乘透明像素还原颜色，完全透明像素不泄露隐藏颜色");
        var design=new Blueprint{Use=ProductUse.Picture,TemplateId=ProductTemplates.Picture,Views=new(){["front"]=new(){Width=4,Height=4,Cells=Enumerable.Repeat<BeadCell?>(null,16).ToList()}}};
        var atlas=new uint[]{0,0,0,0,0,0x000000FF,0xFFF7E8FF,0,0,0xD84E637F,0xD84E6380,0};
        var source=PixelImport.Capture(atlas,4,3,new(1,1,2,2),"(O)test");atlas[5]=0;
        check(source.Pixels[0]==0x000000FF && source.Pixels.Length==4,"读取指定源矩形且原图快照不共享图集数组");
        bool rejected=false;try{PixelImport.Capture(atlas,4,3,new(int.MaxValue,0,2,2),null);}catch(ArgumentException){rejected=true;}
        check(rejected,"源矩形越界和加法溢出拒绝");
        var preview=PixelImport.Create(design,"front",source,new(ImportSizing.Original),catalog,unlocked);
        check(preview.EffectiveCells==3 && preview.Candidate.Views["front"].Cells[5]?.ColorId=="black"
            && preview.Candidate.Views["front"].Cells[9] is null && preview.Candidate.Views["front"].Cells[10]?.ColorId=="red",
            "黑色不是透明，阈值127/128边界一致，原比例居中保留空白");
        check(preview.LockedColors.Count==0 && preview.Candidate.Views["front"].Cells.Where(c=>c is not null).All(c=>c!.MaterialId=="decoration"),
            "装饰导入自动普通豆，不要求颜色解锁");
        var accessible=PixelImport.Create(design,"front",source,new(ImportSizing.Original,UnlockedOnly:true),catalog,unlocked);
        check(accessible.LockedColors.Count==0 && accessible.Candidate.Reference!.Pixels.SequenceEqual(source.Pixels)
            && preview.Candidate.Views["front"].Cells[10]?.ColorId=="red","已解锁近似色预览不破坏原参考或另一份候选");
        preview.Candidate.Reference!.Pixels[0]=0;
        check(source.Pixels[0]!=0 && design.Reference is null && design.Views["front"].Cells.All(c=>c is null),"候选、原参考和编辑设计完全隔离");
        var wide=new ReferencePixels{Width=8,Height=2,Pixels=Enumerable.Repeat(0xFFF7E8FFu,16).ToArray()};
        rejected=false;try{PixelImport.Create(design,"front",wide,new(ImportSizing.Original),catalog,unlocked);}catch(ArgumentException){rejected=true;}
        check(rejected,"过大原图必须明确选择裁剪或缩放，不静默硬切");
        var scaled=PixelImport.Create(design,"front",wide,new(ImportSizing.FitNearest),catalog,unlocked);
        check(scaled.EffectiveCells==4 && scaled.Candidate.Views["front"].Cells.Skip(4).Take(4).All(c=>c is not null),"最近邻按比例缩放宽图，不拉伸成正方形");
        var crop=PixelImport.Create(design,"front",wide,new(ImportSizing.Crop,CropX:6,CropY:1,MaterialId:"decoration"),catalog,unlocked);
        check(crop.EffectiveCells==2 && crop.Candidate.Views["front"].Cells.Where(c=>c is not null).All(c=>c!.MaterialId=="decoration"),"显式裁剪偏移与合法材质填入一致");
        rejected=false;try{PixelImport.Create(design,"front",source,new(ImportSizing.Original,MaterialId:"unknown"),catalog,unlocked);}catch(ArgumentException){rejected=true;}
        check(!rejected,"装饰导入忽略旧手动配料，统一普通豆");
        var editor=new EditorDocument(design,catalog,"front");
        check(!editor.IsDirty && editor.ApplyCandidate(accessible.Candidate) && editor.Undo() && !editor.IsDirty && editor.Snapshot().Reference is null,
            "预览不改草稿，确认一次应用且撤销恢复图案和参考");
        var hat=ProductTemplates.DebugSample(true,false,"hat").Design;
        var back=DesignStorage.Serialize(hat.Views["back"]);
        var clothing=PixelImport.Create(hat,"front",source,new(ImportSizing.Original),catalog,unlocked);
        check(DesignStorage.Serialize(clothing.Candidate.Views["back"])==back && clothing.Candidate.WoolMaterials["wool"]==80,
            "单图导入仅替换选定服装方向，不改变其他方向和羊毛预算");
    }
}
