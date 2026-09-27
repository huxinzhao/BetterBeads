using BetterBeads.Data;

internal static class WeaponPreviewChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();foreach(var material in catalog.Materials){material.Hardness=4;material.Weight=5;}
        var source=new Blueprint{Name="preview",Use=ProductUse.Sword,TemplateId="check.preview",
            Views=new(){["front"]=new(){Width=1,Height=1,Cells=new(){new(){ColorId="red",Rgba=0xD84E63FF,MaterialId="ruby"}}}},
            SupplementaryMaterials=new(){{"copper",3}}};
        var template=new TemplateSpec(source.TemplateId,source.Use,1,1,new[]{"front"},"front",ManufacturingAvailable:true,MinimumMaterials:4);
        var recipe=new ManufacturingRecipe(template,"(W)check.preview",0,WeaponStatsChecks.SyntheticRules());
        var empty=new Dictionary<string,int>();
        var preview=WeaponDraftPreview.Evaluate(source,recipe,catalog,new HashSet<string>(),empty);
        check(!preview.Report.CanMake && !preview.Report.Issues.Any(i=>i.Code=="locked-color")
            && preview.Report.Issues.Any(i=>i.Code=="insufficient-material") && preview.Stats?.Values is not null,
            "武器缺库存仍可预览属性，颜色不限制制造");
        var slots=new InventorySlot?[]{new("r",BeadItems.BeadId("ruby"),1,999,CanReceiveBeads:true),
            new("c",BeadItems.BeadId("copper"),3,999,CanReceiveBeads:true)};
        var plan=ManufacturingTransaction.Preview(source,recipe,catalog,new HashSet<string>{"red"},slots,0,"preview").Plan!;
        check(WeaponStatValues.TryRead(plan.Product.FinalStats,out var values) && preview.Stats!.Values==values,
            "界面预计属性与备齐材料后的实际制造快照一致");
        var missing=WeaponDraftPreview.Evaluate(source,null,catalog,new HashSet<string>(),empty);
        check(missing.Stats is null,"待确认模板不显示占位或猜测属性");
        source.SupplementaryMaterials["wood"]=1;
        check(WeaponDraftPreview.Evaluate(source,recipe,catalog,new HashSet<string>(),empty).Stats is null,
            "错误底料时不展示看似有效的武器属性");
        source.SupplementaryMaterials.Remove("wood");
        var picture=TemplateConversion.Preview(source,template with{Id="check.picture",Use=ProductUse.Picture},catalog);
        check(picture.IncompatibleCells==0 && picture.IncompatibleSupplements==0
            && picture.Candidate.SupplementaryMaterials.Count==0 && source.Use==ProductUse.Sword,
            "武器转装饰保留图案，预览自动普通豆并清除底料，不影响原图");
        var dagger=TemplateConversion.Preview(source,template with{Id="check.dagger",Use=ProductUse.Dagger},catalog);
        check(dagger.IncompatibleCells==0 && dagger.IncompatibleSupplements==0 && dagger.Candidate.SupplementaryMaterials["copper"]==3,
            "武器间转换保留合法图案与底料，不重复补料");
        var doc=new EditorDocument(source,catalog,"front");var staged=doc.Snapshot();staged.SupplementaryMaterials["copper"]=8;
        check(doc.Snapshot().SupplementaryMaterials["copper"]==3 && doc.ApplyCandidate(staged) && doc.Undo()
            && doc.Snapshot().SupplementaryMaterials["copper"]==3 && !doc.IsDirty,
            "底料弹窗候选与原草稿隔离，整次确认只需一次撤销");
        foreach(var size in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
        {
            var frame=EditorLayout.Calculate(size.Item1,size.Item2).Frame;var layout=WeaponPanelLayout.Calculate(frame);
            check(frame.Contains(layout.Dialog) && layout.Dialog.Contains(layout.Title) && layout.Dialog.Contains(layout.Tabs)
                && layout.Dialog.Contains(layout.Body) && layout.Dialog.Contains(layout.Footer) && layout.Body.Height>=192
                && !layout.Title.Overlaps(layout.Tabs) && !layout.Tabs.Overlaps(layout.Body) && !layout.Body.Overlaps(layout.Footer),
                $"{size.Item1}×{size.Item2} 底料数量控件与属性分页完整容纳，确认区不被遮挡");
        }
    }
}
