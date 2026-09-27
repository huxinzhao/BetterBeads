using BetterBeads.Data;

internal static class RefinementSoulCreativeChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        check(catalog.Validate().Count==0 && catalog.Materials.Count(m=>m.RefinedFrom is not null)==11,
            "四种金属与七种宝石均有独立精炼豆，目录引用有效");
        var expected=new Dictionary<string,(double,double)>{{"emerald",(4.5,3.2)},{"aquamarine",(4.2,2.8)},{"ruby",(5.4,4)},
            {"amethyst",(3.6,3)},{"topaz",(4.8,3.5)},{"jade",(3.9,4)},{"diamond",(6,3.5)}};
        check(expected.All(p=>catalog.Materials.Single(m=>m.Id==p.Key) is {} m && m.Hardness==p.Value.Item1 && m.Weight==p.Value.Item2),
            "七种宝石采用逐项确认的独立硬度与重量指数");
        check(catalog.Materials.Where(m=>m.RefinedFrom is not null).All(m=>
        {
            var original=catalog.Materials.Single(o=>o.Id==m.RefinedFrom);
            return m.Hardness==original.Hardness*1.25 && m.Weight==original.Weight && m.EffectGroup==original.EffectGroup;
        }),"精炼豆硬度提升25%，重量与同源效果组保持一致");
        var recipes=DefaultRefinement.Recipes(catalog);
        check(recipes.Count==11 && recipes.All(r=>r.InputCount==10 && r.OutputCount==5 && r.CoalCount==1 && r.Minutes==30)
            && !recipes.Any(r=>r.SourceMaterial.StartsWith("refined-")),"精炼采用10豆加1煤产5豆、30分钟，仅精炼一次");
        var legacy=DefaultProcessing.Create();legacy.Materials.RemoveAll(m=>m.RefinedFrom is not null);legacy.RulesVersion="3";
        foreach(var m in legacy.Materials.Where(m=>m.Series==MaterialSeries.Gem)){m.Hardness=m.Id=="diamond"?10:7;m.Weight=m.Id=="diamond"?4:3;}
        legacy.Materials.Single(m=>m.Id=="ruby").Hardness=17;legacy.Sources[0].Yield=9;
        string old=DesignStorage.Serialize(legacy);var migrated=DefaultRefinement.UpgradeLegacy(legacy)!;
        check(migrated.RulesVersion=="4" && migrated.Sources[0].Yield==9 && migrated.Materials.Single(m=>m.Id=="aquamarine").Weight==2.8
            && migrated.Materials.Single(m=>m.Id=="ruby").Hardness==17 && migrated.Materials.Single(m=>m.Id=="refined-ruby").Hardness==21.25
            && DesignStorage.Serialize(legacy)==old && DefaultRefinement.UpgradeLegacy(migrated) is null,
            "旧默认宝石按新表迁移，自定义硬度/产量保留，精炼继承实际配置且不重复迁移");
        var bad=DefaultProcessing.Create();bad.Materials.Single(m=>m.Id=="refined-ruby").Hardness=1;
        check(bad.Validate().Any(i=>i=="material.refinement:refined-ruby"),"比原料更弱的精炼豆不能通过配置校验");
        var recipe=DefaultWeapons.Recipes().Single(r=>r.Template.Use==ProductUse.Sword);
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        var design=new Blueprint{Name="精炼测试",Use=ProductUse.Sword,TemplateId=recipe.Template.Id,
            Views=new(){["front"]=new(){Width=16,Height=16,Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}},
            SupplementaryMaterials=new(){{"refined-iridium",15}}};
        design.Views["front"].Cells[0]=new(){ColorId="red",Rgba=0xD84E63FF,MaterialId="refined-iridium"};
        var emptyInventory=new InventorySlot?[]{null,null};var locked=new HashSet<string>();
        var normal=ManufacturingTransaction.Preview(design,recipe,catalog,locked,emptyInventory,0,"normal");
        var creative=ManufacturingTransaction.Preview(design,recipe,catalog,locked,emptyInventory,0,"creative",true);
        check(normal.Plan is null && normal.Report!.Issues.Any(i=>i.Code=="insufficient-material") && creative.Plan is not null
            && creative.Report!.Materials.All(m=>m.IsFree && m.Owned==0 && m.Missing==0) && locked.Count==0,
            "创造模式从空背包免费制造；普通模式仍检查缺料，两者无颜色限制");
        var product=creative.Plan!.Product;
        check(product.FinalStats["minDamage"]==56 && product.FinalStats["maxDamage"]==79 && product.ActualMaterials["refined-iridium"]==16
            && product.CreatedInCreativeMode && creative.Plan.Changes.Count==1 && creative.Plan.MoneyAfter==0,
            "创造模式仍按精炼设计用料计算属性，仅添加成品，不扣料不扣钱");
        var stocked=new InventorySlot?[]{new("bead",BeadItems.BeadId("refined-iridium"),16,999,CanReceiveBeads:true),null};
        var free=ManufacturingTransaction.Preview(design,recipe with{ExtraCost=500},catalog,locked,stocked,0,"free",true).Plan!;
        check(free.OutputSlot==1 && free.Changes.All(c=>c.Slot!=0) && free.MoneyAfter==0,
            "创造模式即使已有真实材料也原样保留，模板额外费用一并免除");
        check(ManufacturingTransaction.Preview(design,recipe,catalog,locked,stocked.Take(1).ToArray(),0,"full",true).Failure==ManufacturingFailure.NoSpace,
            "创造模式满背包仍拒绝，不能吞掉已有材料来腾位置");
        design.SupplementaryMaterials["wood"]=1;
        check(ManufacturingTransaction.Preview(design,recipe,catalog,locked,emptyInventory,0,"illegal",true).Plan is null,
            "创造模式不绕过武器用途限制和配料合法性");
        design.SupplementaryMaterials.Remove("wood");
        check(ManufacturingTransaction.Preview(design,recipe,catalog,locked,emptyInventory,0,"off").Plan is null,
            "退出创造模式后立即恢复颜色与材料要求");
        string original=DesignStorage.Serialize(product),visual=DesignStorage.VisualKey(product);
        check(SoulUpgrades.TryUpgrade(product,out var one) && one!.FinalStats["minDamage"]==62 && one.FinalStats["maxDamage"]==87
            && one.GalaxySoul is {Level:1,BaseMinDamage:56,BaseMaxDamage:79} && DesignStorage.Serialize(product)==original,
            "首个银河之魂提升10%，保存未强化基准，预览不修改原件");
        SoulUpgrades.TryUpgrade(one!,out var two);SoulUpgrades.TryUpgrade(two!,out var three);
        check(three!.GalaxySoul!.Level==3 && three.FinalStats["minDamage"]==73 && three.FinalStats["maxDamage"]==103
            && !SoulUpgrades.TryUpgrade(three,out _),"三级银河之魂按原基准累计30%，非逐次乘1.1，满级拒绝");
        check(DesignStorage.VisualKey(three)==visual && three.InstanceId==product.InstanceId && three.Design.TemplateId==product.Design.TemplateId
            && three.Effects.SequenceEqual(product.Effects) && three.ActualMaterials.OrderBy(p=>p.Key).SequenceEqual(product.ActualMaterials.OrderBy(p=>p.Key))
            && DesignStorage.TryReadSnapshot(DesignStorage.Serialize(three),out var restored) && ProductTemplates.Matches(restored!),
            "强化保留作品身份、图案、实际配料及效果，等级与属性可往返保存");
        var copy=three.Copy();copy.GalaxySoul!.Level=1;
        check(!ProductTemplates.Matches(copy) && three.GalaxySoul.Level==3,"强化状态深复制，等级与保存伤害不一致时拒绝读取");
        var cap=product.Copy();cap.FinalStats["minDamage"]=119;cap.FinalStats["maxDamage"]=120;
        SoulUpgrades.TryUpgrade(cap,out var capped);
        check(capped!.FinalStats["minDamage"]==120 && capped.FinalStats["maxDamage"]==120,"银河之魂伤害仍受120最终上限约束");
        var legacyProduct=product.Copy();legacyProduct.CreatedInCreativeMode=false;legacyProduct.EffectRulesVersion=null;
        legacyProduct.Effects.Clear();legacyProduct.EffectParameters.Clear();
        check(SoulUpgrades.TryUpgrade(legacyProduct,out _),"旧版已保存武器无需重新算材质即可使用银河之魂");
        check(!new WorkbenchSettings().CreativeMode,"创造模式默认关闭，旧配置缺字段时保持普通玩法");
    }
}
