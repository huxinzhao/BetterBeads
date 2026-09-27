using BetterBeads.Data;

internal static class EffectIntegrationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();WeaponTemplates.Configure(DefaultWeapons.Templates());
        var recipe=DefaultWeapons.Recipes().Single(r=>r.Template.Use==ProductUse.Sword);
        var design=new Blueprint{Use=ProductUse.Sword,TemplateId=recipe.Template.Id,SelectedEffects=new(){"ruby","jade"},
            Views=new(){["front"]=new(){Width=16,Height=16,Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}},
            SupplementaryMaterials=new(){{"ruby",10},{"jade",10},{"amethyst",26},{"copper",53}}};
        design.Views["front"].Cells[0]=new(){MaterialId="copper",ColorId="white",Rgba=0xFFFFFFFF};
        var bill=new Dictionary<string,int>{{"ruby",10},{"jade",10},{"amethyst",26},{"copper",54}};
        var inventory=bill.Select(p=>(InventorySlot?)new InventorySlot(p.Key,BeadItems.BeadId(p.Key),p.Value,999,CanReceiveBeads:true)).ToArray();
        var preview=WeaponDraftPreview.Evaluate(design,recipe,catalog,new HashSet<string>(),bill);
        var plan=ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),inventory,0,"effects").Plan!;
        check(preview.Report.CanMake && preview.Effects is {DamageBonusPercent:6,DamagePenaltyPercent:0,LifeStealRate:0.015}
            && plan.Product.FinalStats.OrderBy(p=>p.Key).SequenceEqual(preview.Stats!.Values!.ToSnapshot().OrderBy(p=>p.Key)),
            "效果比例含底料，预览与实际制造一致：红宝石增伤、翡翠吸血、未选择紫水晶不激活且不罚伤害");
        double hardness=bill.Sum(p=>catalog.Materials.Single(m=>m.Id==p.Key).Hardness*p.Value)/100;
        check(plan.Product.FinalStats["minDamage"]==Math.Round(hardness*5*1.06,MidpointRounding.AwayFromZero)
            && plan.Product.FinalStats["maxDamage"]==Math.Round(hardness*7*1.06,MidpointRounding.AwayFromZero),
            "伤害按连续增益计算，只在最终阶段取整");
        var rules=recipe.WeaponRules! with{MinDamage=new(1000,0,0,1,120),MaxDamage=new(1000,0,0,1,120)};
        var capped=WeaponStats.Calculate(ProductUse.Sword,rules,catalog,bill,preview.Effects).Values!;
        check(capped.MinDamage==120 && capped.MaxDamage==120,"超过上限的原始伤害先处理增益，再限制为120");
        var product=plan.Product;
        check(product.Effects.SequenceEqual(new[]{"ruby","jade"}) && product.EffectParameters["lifeStealRate"]==0.015
            && DesignStorage.TryReadSnapshot(DesignStorage.Serialize(product),out var restored) && ProductTemplates.Matches(restored!),
            "成品冻结实际生效效果及参数，序列化后独立于当前配方");
        product.EffectParameters["lifeStealRate"]=0.5;
        check(!ProductTemplates.Matches(product) && plan.Product.EffectParameters["lifeStealRate"]==0.015,
            "非法吸血倍率拒绝，修改交付副本不污染制造计划");
        product=plan.Product;product.EffectRulesVersion=null;product.Effects.Clear();product.EffectParameters.Clear();
        check(ProductTemplates.Matches(product),"旧版无效果成品仍可读取，不根据新比例追罚旧武器");
        product.Effects.Add("ruby");check(!ProductTemplates.Matches(product),"没有效果规则版本不能伪造已生效效果");
        var editor=new EditorDocument(design,catalog,"front");var candidate=editor.Snapshot();candidate.SelectedEffects.Clear();
        editor.ApplyCandidate(candidate);editor.Undo();
        check(editor.Snapshot().SelectedEffects.SequenceEqual(new[]{"ruby","jade"}) && design.SelectedEffects.Count==2,
            "效果设置作为独立候选，确认后一次撤销，原图纸不被修改");
        design.SelectedEffects.Add("amethyst");
        check(WeaponDraftPreview.Evaluate(design,recipe,catalog,new HashSet<string>(),bill).Report.Issues.Any(i=>i.Code=="effect-selection")
            && ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),inventory,0,"bad-effects").Plan is null,
            "选择三种效果时预览明确提示且交易拒绝，不静默忽略第三种");
        design.SelectedEffects=null!;
        check(WeaponDraftPreview.Evaluate(design,recipe,catalog,new HashSet<string>(),bill).Report.Issues.Any(i=>i.Code=="structure"),
            "损坏的空效果集合不会让武器预览抛异常");
        var legacy=DefaultProcessing.Create();legacy.RulesVersion="2";legacy.Sources[0].Yield=9;
        legacy.Materials.RemoveAll(m=>m.RefinedFrom is not null);
        foreach(var m in legacy.Materials){m.Series=MaterialSeries.Other;m.EffectGroup=null;}
        legacy.Materials.Single(m=>m.Id=="ruby").Hardness=17;
        string raw=DesignStorage.Serialize(legacy);var upgraded=DefaultEffects.UpgradeLegacy(legacy)!;
        check(upgraded.RulesVersion=="3" && upgraded.Materials.Single(m=>m.Id=="ruby") is {Hardness:17,EffectGroup:"ruby",Series:MaterialSeries.Gem}
            && upgraded.Sources[0].Yield==9 && DesignStorage.Serialize(legacy)==raw && DefaultEffects.UpgradeLegacy(upgraded) is null,
            "旧材料补齐系列与效果组，保留自定义数值和加工量且迁移只做一次");
        check(LifeSteal.ActualDamage(10,-90,100)==10 && LifeSteal.ActualDamage(10,10,-1)==0
            && LifeSteal.ActualDamage(10,20,10)==0,"吸血只认实际扣血，击杀溢出、未命中和回血不增加收益");
        int remainder=0;
        check(LifeSteal.Heal(19,50,100,ref remainder)==0 && remainder==19
            && LifeSteal.Heal(1,50,100,ref remainder)==1 && remainder==0,"吸血不足1点的余量累积，20实际伤害恰好回血1点");
        remainder=19;
        check(LifeSteal.Heal(10000,99,100,ref remainder)==1 && remainder==0
            && LifeSteal.Heal(100,100,100,ref remainder)==0 && remainder==0
            && LifeSteal.Heal(100,0,100,ref remainder)==0,"吸血不超最大生命、不在满血时囤积余量，也不能复活");
    }
}
