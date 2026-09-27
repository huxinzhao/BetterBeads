using BetterBeads.Data;

internal static class WeaponMaterialChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        // Synthetic values only: no pending balance proposal is installed by these checks.
        foreach(var m in catalog.Materials.Where(m=>WeaponMaterials.IsWeapon(m.AllowedUses.First())))
        {m.Hardness=2;m.Weight=4;}
        catalog.Materials.Single(m=>m.Id=="ruby").Hardness=8;
        catalog.Materials.Single(m=>m.Id=="ruby").Weight=2;
        catalog.Materials.Single(m=>m.Id=="ruby").EffectGroup="test-effect";
        var design=new Blueprint{Name="weapon",Use=ProductUse.Sword,TemplateId="check.sword",
            Views=new(){["front"]=new(){Width=1,Height=1,Cells=new(){new(){ColorId="white",Rgba=0xFFFFFFFF,MaterialId="ruby"}}}},
            SupplementaryMaterials=new(){{"copper",3}}};
        var template=new TemplateSpec("check.sword",ProductUse.Sword,1,1,new[]{"front"},"front",ManufacturingAvailable:true,MinimumMaterials:4);
        var inventory=new Dictionary<string,int>{{"ruby",1},{"copper",3}};
        var report=DraftAssessment.Evaluate(design,template,catalog,new HashSet<string>(),inventory);
        var bill=report.Materials.ToDictionary(m=>m.Id,m=>m.Needed);
        var summary=WeaponMaterials.Analyze(design.Use,catalog,bill).Summary!;
        check(report.CanMake && summary.Total==4 && summary.AverageHardness==3.5 && summary.AverageWeight==3.5
            && summary.MaterialFractions["ruby"]==0.25 && summary.EffectFractions["test-effect"]==0.25,
            "武器统一账单合并图案与底料，各豆计一次，底料进入平均值及效果分母");
        var recipe=new ManufacturingRecipe(template,"(W)check.sword",0,WeaponStatsChecks.SyntheticRules());
        var slots=new InventorySlot?[]{new("r",BeadItems.BeadId("ruby"),1,999,CanReceiveBeads:true),
            new("c",BeadItems.BeadId("copper"),3,999,CanReceiveBeads:true)};
        var plan=ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),slots,0,"weapon-check").Plan!;
        check(plan.Product.ActualMaterials["ruby"]==1 && plan.Product.ActualMaterials["copper"]==3
            && plan.Changes.Single(c=>c.Slot==1).After is null,"制造计划按统一底料账单扣完两种豆，不仅扣显示图案");
        design.SupplementaryMaterials["copper"]=2;
        check(DraftAssessment.Evaluate(design,template,catalog,new HashSet<string>(),inventory).Issues.Any(i=>i.Code=="minimum-material"),
            "武器图案非空但总投入不足时拒绝");
        design.SupplementaryMaterials=new(){{"wood",3}};
        check(DraftAssessment.Evaluate(design,template,catalog,new HashSet<string>(),new Dictionary<string,int>{{"ruby",1},{"wood",3}})
            .Issues.Any(i=>i.Code=="incompatible-material"),"库存足量的木豆不能作为武器底料");
        check(WeaponMaterials.Analyze(ProductUse.Sword,catalog,new Dictionary<string,int>{{"wood",3}}).Summary is null
            && WeaponMaterials.Analyze(ProductUse.Sword,catalog,new Dictionary<string,int>()).Summary is null,
            "属性入口独立拒绝非法底料及零投入，不能绕过用途规则");
        check(WeaponMaterials.Analyze(ProductUse.Hammer,catalog,new Dictionary<string,int>{{"iridium",12}}).Summary!.AverageHardness==2
            && WeaponMaterials.Analyze(ProductUse.Dagger,catalog,new Dictionary<string,int>{{"ruby",12}}).Summary!.AverageHardness==8,
            "纯铱与纯宝石均可计算，不强制金属底料");
        var unconfigured=DefaultProcessing.Create();foreach(var m in unconfigured.Materials){m.Hardness=0;m.Weight=0;}
        check(WeaponMaterials.Analyze(ProductUse.Sword,unconfigured,bill).Failure==WeaponMaterialFailure.UnconfiguredMaterial,
            "零硬度/重量的未配置材料不会生成武器属性");
        catalog.Materials.Single(m=>m.Id=="copper").EffectGroup="test-effect";
        var grouped=WeaponMaterials.Analyze(ProductUse.Sword,catalog,bill).Summary!;
        check(grouped.EffectFractions["test-effect"]==1 && grouped.MaterialFractions["ruby"]==0.25
            && grouped.MaterialFractions["copper"]==0.75,"只有显式配置的效果组才合并，实际材料比例仍分开保存");
        design.SupplementaryMaterials=new(){{"ruby",int.MaxValue}};
        check(DraftAssessment.Evaluate(design,template,catalog,new HashSet<string>(),inventory).Issues.Any(i=>i.Code=="material-overflow"),
            "图案和底料合并溢出时拒绝，不变为负数或低成本");
        design.SupplementaryMaterials=new(){{"missing-material",3}};
        check(DesignStorage.TryReadBlueprint(DesignStorage.Serialize(design),out var read)
            && read!.SupplementaryMaterials["missing-material"]==3,"未知底料仍可保存和往返，保留用户原数据");
        var copy=design.Copy();copy.SupplementaryMaterials["missing-material"]=7;
        check(design.SupplementaryMaterials["missing-material"]==3,"图纸复制不共享底料清单");
        var old=System.Text.Json.Nodes.JsonNode.Parse(DesignStorage.Serialize(design))!;
        old.AsObject().Remove("SupplementaryMaterials");
        check(DesignStorage.TryReadBlueprint(old.ToJsonString(),out var legacy) && legacy!.SupplementaryMaterials.Count==0,
            "旧版没有底料字段的图纸正常载入为空底料，不修改版本身份");
        design.SupplementaryMaterials=new(){{"copper",3}};
        var doc=new EditorDocument(design,catalog,"front");
        check(doc.SetSupplementaryMaterial("copper",5) && doc.Snapshot().SupplementaryMaterials["copper"]==5
            && doc.Undo() && doc.Snapshot().SupplementaryMaterials["copper"]==3 && !doc.IsDirty,
            "编辑底料作为独立一步撤销，恢复保存基线");
        check(!doc.SetSupplementaryMaterial("wood",4) && !doc.SetSupplementaryMaterial("copper",-1),
            "底料编辑拒绝非法用途及负数数量");
        design.Views["front"].Cells[0]=null;
        check(DraftAssessment.Evaluate(design,template,catalog,new HashSet<string>(),inventory).Issues.Any(i=>i.Code=="empty"),
            "底料不能代替至少一个有效图案格");
        var picture=ProductTemplates.DebugSample(false,false,"picture").Design;picture.SupplementaryMaterials["copper"]=1;
        var pictureTemplate=DefaultManufacturing.Recipes().Single(r=>r.Template.Use==ProductUse.Picture).Template;
        check(DraftAssessment.Evaluate(picture,pictureTemplate,catalog,new HashSet<string>(),inventory).Materials.All(m=>m.Id=="decoration"),
            "家具只收普通豆，不扣旧图纸残留武器底料");
    }
}
