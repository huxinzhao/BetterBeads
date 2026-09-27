using BetterBeads.Data;

internal static class DraftChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        var picture=ProductTemplates.DebugSample(false,false,"saved").Design;
        foreach(var c in picture.Views["front"].Cells)if(c is not null){c.ColorId="red";c.Rgba=0xD84E63FF;}
        var template=new TemplateSpec(ProductTemplates.Picture,ProductUse.Picture,16,16,new[]{"front"},"front",ManufacturingAvailable:true);
        var report=DraftAssessment.Evaluate(picture,template,catalog,new HashSet<string>(),new Dictionary<string,int>());
        check(!report.Issues.Any(i=>i.Code=="locked-color") && report.Issues.Any(i=>i.Code=="insufficient-material")
            && report.Materials.Single().Needed==100,"颜色自由使用，用料报告按有效格计数");
        report=DraftAssessment.Evaluate(picture,template,catalog,new HashSet<string>{"red"},new Dictionary<string,int>{{"decoration",100}});
        check(report.CanMake,"库存和解锁恢复后不保留过期缺项标记");
        picture.Views["front"].Cells.First(c=>c is not null)!.MaterialId="wood";
        check(DraftAssessment.Evaluate(picture,template,catalog,new HashSet<string>{"red"},new Dictionary<string,int>{{"decoration",100},{"wood",100}})
            .CanMake,"装饰品允许混用足量的木豆和装饰豆");
        var hat=ProductTemplates.DebugSample(true,false,"hat").Design;
        foreach(var view in hat.Views.Values)foreach(var c in view.Cells)if(c is not null)c.ColorId="white";
        var hatTemplate=new TemplateSpec(ProductTemplates.Hat,ProductUse.Hat,20,20,ProductTemplates.HatViews,"front",WoolBudget:80,ManufacturingAvailable:true);
        report=DraftAssessment.Evaluate(hat,hatTemplate,catalog,new HashSet<string>(),new Dictionary<string,int>{{"wool",80}});
        check(report.CanMake && report.Materials.Single().Needed==80,"服装四方向只计一次固定羊毛预算");
        hat.Views["back"].Cells.First(c=>c is not null)!.ColorId="red";
        check(DraftAssessment.Evaluate(hat,hatTemplate,catalog,new HashSet<string>(),new Dictionary<string,int>{{"wool",80}})
            .CanMake,"不计费的背面同样可用任意颜色");
        var document=new EditorDocument(picture,catalog,"front");
        document.Rename("未保存名称");document.DiscardChanges();
        check(document.Snapshot().Name=="saved" && !document.IsDirty,"放弃修改恢复打开时状态");
        var progress=new SaveProgress();var library=new BlueprintRepository(progress);
        document.Rename("已保存名称");var copy=document.Snapshot();check(library.Save(copy),"草稿保存到当前存档图纸库");
        document.MarkSaved(copy.Revision);document.Rename("再次修改");document.DiscardChanges();
        check(document.Snapshot().Name=="已保存名称" && document.Snapshot().Revision==1 && !document.IsDirty,
            "保存后放弃只回滚后续修改，不丢失已保存内容");
    }
}
