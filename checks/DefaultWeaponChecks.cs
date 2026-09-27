using BetterBeads.Data;

internal static class DefaultWeaponChecks
{
    public static void Run(Action<bool,string> check)
    {
        FurnitureTemplates.Configure(DefaultManufacturing.Furniture());WeaponTemplates.Configure(DefaultWeapons.Templates());
        var catalog=DefaultProcessing.Create();var recipes=DefaultWeapons.Recipes();
        check(ManufacturingCatalog.Validate(DefaultManufacturing.Recipes().Concat(recipes).ToArray(),catalog).Count==0,
            "已确认的三类家具和三类武器启动配置、物品身份、模板尺寸及材质指数一致");
        foreach(var recipe in recipes)
        {
            var template=recipe.Template;
            var design=new Blueprint{Name="正式武器",Use=template.Use,TemplateId=template.Id,Views=new(){["front"]=new(){Width=16,Height=16,
                Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}},SupplementaryMaterials=new(){{"iridium",template.MinimumMaterials-1}}};
            design.Views["front"].Cells[0]=new(){ColorId="white",Rgba=0xFFFFFFFF,MaterialId="iridium"};
            var slots=new InventorySlot?[]{new("beads",BeadItems.BeadId("iridium"),template.MinimumMaterials,999,CanReceiveBeads:true)};
            var plan=ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),slots,0,"official-"+template.Use).Plan!;
            var stats=plan.Product.FinalStats;bool sword=template.Use==ProductUse.Sword,dagger=template.Use==ProductUse.Dagger;
            check(plan.MoneyAfter==0 && plan.OutputSlot==0 && plan.Product.ActualMaterials["iridium"]==(sword?16:dagger?12:24)
                && stats["minDamage"]==(sword?45:dagger?27:63) && stats["maxDamage"]==(sword?63:dagger?36:90)
                && stats["speed"]==(sword?-1:dagger?1:-3),$"{template.Use} 正式最低投入与纯铱伤害/速度符合已确认默认值");
            check(ProductTemplates.QualifiedId(plan.Product)==recipe.OutputItemId
                && DesignStorage.TryReadSnapshot(DesignStorage.Serialize(plan.Product),out var restored) && ProductTemplates.Matches(restored!),
                $"{template.Use} 实际制造快照可交付已注册武器并往返保存");
            design.SupplementaryMaterials["iridium"]--;
            check(ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),slots,0,"short").Plan is null,
                $"{template.Use} 少一颗最低投入时拒绝，不能靠小图案绕过成本");
        }
        var swordRecipe=recipes.Single(r=>r.Template.Use==ProductUse.Sword);
        var copper=WeaponStats.Calculate(ProductUse.Sword,swordRecipe.WeaponRules!,catalog,new Dictionary<string,int>{{"copper",16}}).Values!;
        var diamond=WeaponStats.Calculate(ProductUse.Sword,swordRecipe.WeaponRules!,catalog,new Dictionary<string,int>{{"diamond",16}}).Values!;
        check(copper.MinDamage==15 && copper.MaxDamage==21 && copper.Speed==0
            && diamond.MinDamage==30 && diamond.MaxDamage==42 && diamond.Speed==1,"纯铜与纯钻石剑符合已确认示例，纯宝石允许制作");
        check(ManufacturingCatalog.Validate(new[]{swordRecipe with{OutputItemId="(F)"+swordRecipe.Template.Id}},catalog).Any(i=>i.StartsWith("recipe.output:"))
            && ManufacturingCatalog.Validate(new[]{swordRecipe with{Template=swordRecipe.Template with{Width=32}}},catalog).Any(i=>i.StartsWith("recipe.template:")),
            "启动时拒绝错误产物类别和不一致模板尺寸，不延后到扣料提交失败");
        check(ManufacturingCatalog.Validate(new[]{swordRecipe,swordRecipe},catalog).Any(i=>i.StartsWith("recipe.duplicate:"))
            && !ManufacturingCatalog.ValidRecipe(swordRecipe with{Template=swordRecipe.Template with{MinimumBeads=257}}),
            "重复生产身份及超过画布容量的最低有效格配置拒绝");
        var legacy=DefaultProcessing.Create();legacy.RulesVersion="1";
        legacy.Materials.RemoveAll(m=>m.RefinedFrom is not null);
        foreach(var m in legacy.Materials){m.Hardness=0;m.Weight=0;}
        legacy.Sources[0].Yield=7;string original=DesignStorage.Serialize(legacy);
        var upgraded=DefaultWeapons.UpgradeLegacy(legacy)!;
        check(upgraded.RulesVersion=="2" && upgraded.Sources[0].Yield==7 && upgraded.Materials.Single(m=>m.Id=="iridium").Hardness==9
            && DesignStorage.Serialize(legacy)==original,"旧零指数配置升级保留用户产量等字段，原对象可完整备份且不被改写");
        check(DefaultWeapons.UpgradeLegacy(upgraded) is null,"已升级配置不重复迁移或生成备份");
        legacy.Materials.Single(m=>m.Id=="copper").Hardness=12;
        check(DefaultWeapons.UpgradeLegacy(legacy) is null,"自定义非零材质指数不被默认值覆盖");
        var broken=catalog.Materials.Single(m=>m.Id=="iridium");broken.Hardness=0;
        check(ManufacturingCatalog.Validate(recipes,catalog).Any(i=>i.StartsWith("recipe.weapon-materials:")),
            "启动时拒绝未配置的合法武器材料，避免误显示可制作");
        var designForStatus=new Blueprint{Use=ProductUse.Sword,TemplateId=swordRecipe.Template.Id,Views=new(){["front"]=new(){Width=16,Height=16,
            Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}},SupplementaryMaterials=new(){{"iridium",15}}};
        designForStatus.Views["front"].Cells[0]=new(){ColorId="white",Rgba=0xFFFFFFFF,MaterialId="iridium"};
        var status=WeaponDraftPreview.Evaluate(designForStatus,swordRecipe,catalog,new HashSet<string>(),new Dictionary<string,int>{{"iridium",16}});
        check(!status.Report.CanMake && status.Report.Issues.Any(i=>i.Code=="weapon-rules"),
            "图纸库与属性预览不会把规则无效的足料武器标为可制作");
    }
}
