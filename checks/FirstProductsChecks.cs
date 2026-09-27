using BetterBeads.Data;

internal static class FirstProductsChecks
{
    public static void Run(Action<bool,string> check)
    {
        FurnitureTemplates.Configure(DefaultManufacturing.Furniture());
        var catalog=DefaultProcessing.Create();var progress=new SaveProgress();
        var recipes=DefaultManufacturing.Recipes();
        foreach(var recipe in recipes)
        {
            string material=recipe.Template.Use switch{ProductUse.Picture=>"decoration",ProductUse.WoodFurniture=>"wood",_=>"stone"};
            string source=material switch{"decoration"=>"(O)771","wood"=>"(O)388",_=>"(O)390"};
            var beadRecipe=catalog.Sources.First(r=>r.MaterialId==material && r.ItemId==source);
            var slots=new InventorySlot?[]{new("beads",BeadItems.BeadId(material),Math.Max(2,recipe.Template.StructureBudget),999,CanReceiveBeads:true),null};

            var design=new Blueprint{Name="首批成品",Use=recipe.Template.Use,TemplateId=recipe.Template.Id,
                Views=new(){["front"]=new(){Width=recipe.Template.Width,Height=recipe.Template.Height,Cells=Enumerable.Repeat<BeadCell?>(null,recipe.Template.Width*recipe.Template.Height).ToList()}}};
            design.Views["front"].Cells[0]=new(){ColorId="white",Rgba=0xFFF7E8FF,MaterialId=material};
            design.Views["front"].Cells[255]=new(){ColorId="black",Rgba=0x25242AFF,MaterialId=material};
            var before=DesignStorage.Serialize(design);
            var plan=ManufacturingTransaction.Preview(design,recipe,catalog,progress.UnlockedColors,slots,0,"make-"+material).Plan;
            check(plan is not null && plan.MoneyAfter==0 && plan.Product.ActualMaterials["decoration"]==Math.Max(2,recipe.Template.StructureBudget)
                && plan.OutputItemId=="(F)"+design.TemplateId && ProductTemplates.Matches(plan.Product)
                && plan.Changes.Single(c=>c.Slot==plan.OutputSlot).After is {Count:1,MaxStack:1},
                $"{recipe.Template.Use} 旧普通材质豆接正式熨烫：按像素与结构预算较大值扣豆、零费用、独立单件成品");
            var definition=FurnitureTemplates.All.Single(t=>t.Id==design.TemplateId);
            var fields=definition.FurnitureData("测试").Split('/');
            check(fields[2]==$"{definition.PixelWidth/16} {definition.PixelHeight/16}" && fields[3]==$"{definition.FootprintWidth} {definition.FootprintHeight}" && fields[5]=="0" && definition.Matches(design),
                $"{recipe.Template.Use} 正式产物元数据：独立尺寸、占地、售价0");
            var snapshot=plan!.Product;
            check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(snapshot),out var restored)
                && ProductTemplates.Matches(restored!) && ProductTemplates.CreateAtlas(restored!).Cells[255]!.Rgba==0x25242AFF
                && DesignStorage.Serialize(design)==before && progress.BlueprintRecords.Count==0,
                $"{recipe.Template.Use} 成品快照往返保留边角图案，制作不保存或修改图纸");
            var less=new InventorySlot?[]{new("short",BeadItems.BeadId(material),Math.Max(2,recipe.Template.StructureBudget)-1,999,CanReceiveBeads:true),null};
            check(ManufacturingTransaction.Preview(design,recipe,catalog,progress.UnlockedColors,less,0,"short").Report!
                .Issues.Any(i=>i.Code=="insufficient-material"),$"{recipe.Template.Use} 正式模板缺一豆拒绝");
            design.Views["front"].Cells[255]=null;
            check(ManufacturingTransaction.Preview(design,recipe,catalog,progress.UnlockedColors,slots,0,"single").Plan is not null,
                $"{recipe.Template.Use} 正式模板单颗图案在满足结构预算后可制作");
            design.Views["front"].Cells[0]=null;
            check(ManufacturingTransaction.Preview(design,recipe,catalog,progress.UnlockedColors,less,0,"empty").Report!
                .Issues.Any(i=>i.Code=="empty"),$"{recipe.Template.Use} 正式模板空图拒绝");
        }
        var altered=recipes[0].Template.Views;altered[0]="corrupted";
        check(DefaultManufacturing.Recipes().All(r=>r.Template.Views.SequenceEqual(new[]{"front"})),
            "正式模板每次返回独立视图列表，调用方不能污染后续默认值");
    }
}
