using BetterBeads.Data;

internal static class TemplateChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        var source=new Blueprint{Name="source",Use=ProductUse.Picture,TemplateId="check.picture",
            Reference=new(){Width=1,Height=1,Pixels=new uint[]{0x123456FF}},
            Views=new(){["front"]=new(){Width=2,Height=1,Cells=new(){
                new(){ColorId="unknown-color",Rgba=0x123456FF,MaterialId="decoration"},new(){ColorId="white",Rgba=0xFFF7E8FF}}}}};
        var wood=new TemplateSpec("check.wood",ProductUse.WoodFurniture,2,1,new[]{"front"},"front");
        var preview=TemplateConversion.Preview(source,wood,catalog);
        check(preview.IncompatibleCells==0 && preview.UnassignedCells==0 && preview.Candidate.Id==source.Id
            && preview.Candidate.Revision==source.Revision && preview.Candidate.Views["front"].Cells[0]!.MaterialId=="decoration",
            "装饰模板之间切换保留原配料且不再误报材质不兼容，自动配齐普通豆");
        check(preview.Candidate.Views["front"].Cells[0]!.ColorId=="unknown-color"
            && preview.Candidate.Views["front"].Cells[0]!.Rgba==0x123456FF && source.Use==ProductUse.Picture,
            "用途候选不改原草稿，不偷偷替换未知颜色");
        preview.Candidate.Reference!.Pixels[0]=0;
        check(source.Reference!.Pixels[0]==0x123456FF,"用途切换保留独立原参考快照");
        var tall=TemplateConversion.Preview(source,wood with{Height=3},catalog);
        check(tall.Candidate.Views["front"].Cells.Take(2).All(c=>c is null)
            && tall.Candidate.Views["front"].Cells[2]!.Rgba==0x123456FF,"扩大模板居中保留图案比例和透明格");
        bool rejected=false;try{TemplateConversion.Preview(source,wood with{Width=1},catalog);}catch(ArgumentException){rejected=true;}
        check(rejected,"缩小模板不能静默裁图，需明确裁剪或缩放");
        var crop=TemplateConversion.Preview(source,wood with{Width=1},catalog,ImportSizing.Crop,1,0);
        check(crop.UnassignedCells==0 && crop.Candidate.Views["front"].Cells[0]!.ColorId=="white","显式裁剪用途候选保留相应格的颜色和配料状态");
        var document=new EditorDocument(source,catalog,"front");
        check(document.ApplyCandidate(preview.Candidate) && document.Snapshot().Use==ProductUse.WoodFurniture
            && document.Undo() && !document.IsDirty && document.Snapshot().TemplateId==source.TemplateId,
            "确认用途转换为一次撤销，恢复模板、图案和参考");
    }
}
