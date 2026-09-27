using BetterBeads.Data;
internal static class PatternArtChecks
{
    public static void Run(Action<bool,string> check)
    {
        ClothingTemplates.Configure(new());FurnitureTemplates.Configure(DefaultManufacturing.Furniture());WeaponTemplates.Configure(DefaultWeapons.Templates());
        var catalog=DefaultProcessing.Create();var recipes=DefaultManufacturing.Recipes().Concat(DefaultWeapons.Recipes()).Concat(ClothingTemplates.Recipes()).ToArray();
        check(ManufacturingCatalog.Validate(recipes,catalog).Count==0,"新增32格挂画注册、纹理标识及制作模板一致");
        var products=new Dictionary<string,ProductSnapshot>();
        foreach(var reference in ReferencePatterns.All)
        {
            var design=ReferencePatterns.Create(reference);var recipe=recipes.Single(r=>r.Template.Id==design.TemplateId);
            var stock=new InventorySlot?[]{new("ordinary",BeadItems.BeadId("decoration"),999,999,CanReceiveBeads:true),new("iron",BeadItems.BeadId("iron"),999,999,CanReceiveBeads:true),new("copper",BeadItems.BeadId("copper"),999,999,CanReceiveBeads:true),new("wool",BeadItems.BeadId("wool"),999,999,CanReceiveBeads:true),null};
            var result=ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),stock,0,"art-"+reference.Id);
            bool valid=result.Plan is {} plan && ProductTemplates.Matches(plan.Product) && DesignStorage.TryReadSnapshot(DesignStorage.Serialize(plan.Product),out var restored)
                && ProductTemplates.CreateAtlas(restored!).Width==(design.Use==ProductUse.Shirt?256:recipe.Template.Width)
                && design.Views.Values.All(g=>g.Cells.Count==g.Width*g.Height && g.Cells.Any(c=>c is not null));
            check(valid,reference.Id+"：实际网格可熨烫、用料可满足、成品快照往返有效");
            if(result.Plan is {} completed)products[reference.Id]=completed.Product;
        }
        var old=ProductTemplates.DebugSample(false,false,"旧16格作品");old.CreatedInCreativeMode=false;
        var refined=products["robin.house"];
        check(ProductTemplates.Matches(old) && ProductTemplates.Matches(refined) && old.Design.Views["front"].Width==16 && refined.Design.Views["front"].Width==32,"旧16格和新32格挂画并存，不修改旧成品尺寸");
        var robin=new WorkshopProgress{FirstBenchDay=0};var gus=new WorkshopCommission(0,"Gus",ProductTemplates.Picture,"酒馆挂画",250,32);
        check(WorkshopStories.Matches(robin,"robin",old) && WorkshopStories.Matches(robin,"robin",refined)
            && gus.Matches(old) && gus.Matches(refined) && !gus.Matches(products["spring.pot"]),"罗宾故事与格斯委托接受两种挂画，但拒绝其他种类");
        check(ReferencePatterns.All.Count(p=>p.TemplateId==ProductTemplates.DetailedPicture)==8
            && refined.ActualMaterials["decoration"]==refined.Design.Views["front"].Cells.Count(c=>c is not null),"8张挂画原生32格构图，用料按实际豆数计费");
        var progress=new SaveProgress();var library=new BlueprintRepository(progress);library.Save(old.Design);string saved=progress.BlueprintRecords[old.Design.Id];
        check(library.SaveAs(refined.Design,refined.Design.Name,out var copy) && copy!.Id!=old.Design.Id && progress.BlueprintRecords[old.Design.Id]==saved,"新图纸复制不会替换玩家已保存的16格设计");
        check(new[]{"emily.hat","emily.shirt"}.All(id=>products[id].Design.Views.Values.Select(g=>string.Join(',',g.Cells.Select(c=>c?.Rgba??0))).Distinct().Count()==4),"四向服装图案完整且各自独立");
        foreach(var (w,h) in new[]{(480,420),(1280,900)})
        {
            var l=PatternDetailLayout.Calculate(w,h);
            check(l.Frame.Contains(l.Mode) && !l.Title.Overlaps(l.Mode) && !l.Mode.Overlaps(l.Preview) && l.Mode.Height>=40 && l.Frame.Contains(l.Preview) && !l.Details.Overlaps(l.Copy),$"{w}×{h}成品/豆盘切换与详情布局不重叠");
        }
    }
}
