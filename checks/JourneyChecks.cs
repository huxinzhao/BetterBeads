using BetterBeads.Data;

internal static class JourneyChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();FurnitureTemplates.Configure(DefaultManufacturing.Furniture());
        var recipes=DefaultManufacturing.Recipes().Concat(DefaultWeapons.Recipes()).Concat(ClothingTemplates.Recipes()).ToArray();
        check(recipes.Length==11 && ManufacturingCatalog.Validate(recipes,catalog).Count==0,"11种成品模板与注册身份一致，服装完整进入制造目录");
        foreach(var recipe in ClothingTemplates.Recipes())
        {
            var t=recipe.Template;var bp=new Blueprint{Use=t.Use,TemplateId=t.Id,WoolMaterials=new(){{"wool",t.WoolBudget!.Value}}};
            foreach(var view in t.Views)
            {
                var g=new BeadGrid{Width=t.Width,Height=t.Height,Cells=Enumerable.Repeat<BeadCell?>(null,t.Width*t.Height).ToList()};
                g.Cells[0]=new(){ColorId="white",Rgba=0xFFF7E8FF};bp.Views[view]=g;
            }
            int cost=t.WoolBudget.Value;
            var inventory=new InventorySlot?[]{new("wool",BeadItems.BeadId("wool"),cost,999,CanReceiveBeads:true)};
            var plan=ManufacturingTransaction.Preview(bp,recipe,catalog,new HashSet<string>(),inventory,0,"clothes").Plan;
            check(plan is not null && plan.Product.ActualMaterials.Count==1 && plan.Product.ActualMaterials["wool"]==cost && plan.OutputSlot==0
                && ProductTemplates.Matches(plan.Product),$"{t.Use} 方向不重复计费，扣完羊毛释放格能装入成品");
            check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(plan!.Product),out var copy) && ProductTemplates.Matches(copy!),$"{t.Use} 快照可独立往返读取");
            var report=DraftAssessment.Evaluate(bp,t,catalog,new HashSet<string>(),new Dictionary<string,int>{{"wool",cost-1}});
            check(report.Issues.Any(i=>i.Code=="insufficient-material"),$"{t.Use} 缺一颗羊毛不可制作");
            bp.Views["front"].Cells[0]=null;
            check(!DraftAssessment.Evaluate(bp,t,catalog,new HashSet<string>(),new Dictionary<string,int>(),true).CanMake,$"{t.Use} 创造模式不能绕过空图案");
        }
        var source=ProductTemplates.DebugSample(false,false,"transition").Design;
        foreach(var g in source.Views.Values)foreach(var c in g.Cells)if(c is not null){c.ColorId="white";c.MaterialId="wood";}
        var hat=TemplateConversion.Preview(source,ClothingTemplates.Find(ProductTemplates.Hat)!,catalog).Candidate;
        check(hat.Views.Count==4 && hat.Views["front"].Cells.Any(c=>c is not null) && hat.Views["back"].Cells.All(c=>c is null)
            && hat.WoolMaterials["wool"]==24 && hat.Views.Values.All(g=>g.Cells.All(c=>c?.MaterialId is null)),"单图转帽子仅放入前向，固定羊毛预算且不带逐格木料");
        var editor=new EditorDocument(source,catalog,"front");editor.ApplyCandidate(hat);editor.SetView("back");
        var picture=TemplateConversion.Preview(hat,DefaultManufacturing.Recipes()[0].Template,catalog,ImportSizing.FitNearest,sourceView:"back").Candidate;
        check(editor.ApplyCandidate(picture) && editor.View=="front" && editor.Undo() && editor.Snapshot().Use==ProductUse.Hat,"多方向转单图回到有效视图，转换可完整撤销");
        var shirt=TemplateConversion.Preview(hat,ClothingTemplates.Find(ClothingTemplates.Shirt)!,catalog,ImportSizing.FitNearest).Candidate;
        shirt.Views["left"].Cells[0]=new(){ColorId="white",Rgba=0x123456FF};
        var atlas=ClothingTemplates.ShirtAtlas(shirt);
        check(atlas.Width==256 && atlas.Height==32 && atlas.Cells[16*256]?.Rgba==0x123456FF
            && Enumerable.Range(0,32).All(y=>atlas.Cells.Skip(y*256+128).Take(128).All(c=>c is null)),"上衣使用256×32原生格式，左向保持原生独立方向，染色层透明");
        var assigned=DraftRepairs.AssignMaterial(source,catalog,"decoration");
        check(assigned.Views["front"].Cells.Where(c=>c is not null).All(c=>c!.MaterialId=="decoration")
            && source.Views["front"].Cells.First(c=>c is not null)!.MaterialId=="wood","旧装饰草稿配普通豆，保留原草稿");
        assigned.Views["front"].Cells[0]=new(){ColorId="removed-color",Rgba=0xFF0000FF,MaterialId="diamond"};
        var fixedColors=DraftRepairs.UnlockedColors(assigned,catalog,new HashSet<string>());
        check(fixedColors.Views["front"].Cells[0]!.MaterialId=="diamond" && fixedColors.Views["front"].Cells[0]!.ColorId!="removed-color"
            && assigned.Views["front"].Cells[0]!.ColorId=="removed-color","缺色修复只改颜色，预览不改原稿与材料");
        check(DefaultManufacturing.Furniture().Single(t=>t.Id==DefaultManufacturing.Chair).FurnitureData("椅").Split('/')[1]=="chair"
            && DefaultManufacturing.Furniture().Single(t=>t.Id==DefaultManufacturing.Table).FurnitureData("桌").Split('/')[1]=="table","椅子与桌子使用原生交互类型，普通装饰不继承功能");
    }
}
