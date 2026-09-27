using BetterBeads.Data;

internal static class WeaponStatsChecks
{
    // Explicit synthetic test data, deliberately different from the pending gameplay proposal.
    public static WeaponStatRules SyntheticRules()=>new("test.rules",ProductUse.Sword,
        new(1,1,0,1,50),new(0,2,0,1,100),new(0,0,-0.625,-10,10),
        new(0.1,0,0.1,0,2),new(0.02,0,0,0,1),new(3,0,0,0,10));
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        foreach(var m in catalog.Materials){m.Hardness=4;m.Weight=5;}
        var ruby=catalog.Materials.Single(m=>m.Id=="ruby");ruby.Hardness=8;ruby.Weight=1;
        var bill=new Dictionary<string,int>{{"copper",3},{"ruby",1}};
        var rules=SyntheticRules();
        var result=WeaponStats.Calculate(ProductUse.Sword,rules,catalog,bill);var values=result.Values!;
        check(result.Failure==WeaponStatFailure.None && values.MinDamage==6 && values.MaxDamage==10
            && values.Speed==-3 && values.Knockback==0.5f && values.CritChance==0.02f && values.CritMultiplier==3,
            "武器换算共用加权均值，整数半值远离零，浮点属性保持原生精度");
        var larger=WeaponStats.Calculate(ProductUse.Sword,rules,catalog,new Dictionary<string,int>{{"copper",300},{"ruby",100}});
        check(larger.Values==values,"同配比增加图案或底料总数不无限放大伤害及速度");
        var caps=rules with{MinDamage=new(-100,0,0,1,50),MaxDamage=new(1000,0,0,1,100),Speed=new(1000,0,0,-10,10),
            Knockback=new(-5,0,0,0,2),CritChance=new(5,0,0,0,0.25),CritMultiplier=new(20,0,0,1,5)};
        var capped=WeaponStats.Calculate(ProductUse.Sword,caps,catalog,bill).Values!;
        check(capped.MinDamage==1 && capped.MaxDamage==100 && capped.Speed==10 && capped.Knockback==0
            && capped.CritChance==0.25f && capped.CritMultiplier==5,"所有武器最终属性按各自配置范围截断");
        var crossed=rules with{MinDamage=new(20,0,0,1,50),MaxDamage=new(2,0,0,1,100)};
        var ordered=WeaponStats.Calculate(ProductUse.Sword,crossed,catalog,bill).Values!;
        check(ordered.MinDamage==20 && ordered.MaxDamage==20,"最高伤害低于最低伤害时纠正且不越过配置上限");
        check(!((rules with{MinDamage=new(1,1,0,1,101)}).IsValid)
            && !((rules with{Speed=new(1,0,0,-1.5,10)}).IsValid)
            && !((rules with{CritChance=new(1,0,0,0,2)}).IsValid)
            && !((rules with{Knockback=new(double.NaN,0,0,0,2)}).IsValid),
            "冲突上限、非整数边界、概率越界和非有限系数在计算前拒绝");
        check(WeaponStats.Calculate(ProductUse.Hammer,rules,catalog,bill).Failure==WeaponStatFailure.InvalidRules,
            "剑规则不能误用于锤或其他武器类型");
        var overflow=rules with{MinDamage=new(0,double.MaxValue,0,1,50)};
        check(WeaponStats.Calculate(ProductUse.Sword,overflow,catalog,bill).Failure==WeaponStatFailure.NonFiniteResult,
            "属性运算溢出明确拒绝，不转成最大强度成品");
        var stored=values.ToSnapshot();
        check(WeaponStatValues.TryRead(stored,out var read) && read==values,"最终属性快照往返无需当前平衡配置");
        stored["speed"]=1.5;
        check(!WeaponStatValues.TryRead(stored,out _),"损坏快照的非整数速度拒绝应用");
        stored=values.ToSnapshot();stored.Remove("critChance");
        check(!WeaponStatValues.TryRead(stored,out _),"最终属性缺字段时拒绝，不用当天默认值补造属性");
        var design=new Blueprint{Name="stats",TemplateId="check.stats",Use=ProductUse.Sword,
            Views=new(){["front"]=new(){Width=1,Height=1,Cells=new(){new(){ColorId="white",Rgba=0xFFFFFFFF,MaterialId="ruby"}}}},
            SupplementaryMaterials=new(){{"copper",3}}};
        var template=new TemplateSpec(design.TemplateId,design.Use,1,1,new[]{"front"},"front",ManufacturingAvailable:true,MinimumMaterials:4);
        var recipe=new ManufacturingRecipe(template,"(W)check.stats",0,rules);
        var slots=new InventorySlot?[]{new("r",BeadItems.BeadId("ruby"),1,999,CanReceiveBeads:true),
            new("c",BeadItems.BeadId("copper"),3,999,CanReceiveBeads:true)};
        var plan=ManufacturingTransaction.Preview(design,recipe,catalog,new HashSet<string>(),slots,0,"stats").Plan!;
        check(plan.Product.WeaponRulesVersion==rules.Version && WeaponStatValues.TryRead(plan.Product.FinalStats,out var final) && final==values,
            "制造直接冻结预览函数的最终属性与武器规则版本");
        var raw=DesignStorage.Serialize(plan.Product);ruby.Hardness=12;
        check(!plan.MatchesRules(recipe,catalog) && DesignStorage.TryReadSnapshot(raw,out var product)
            && WeaponStatValues.TryRead(product!.FinalStats,out var old) && old==values,
            "修改材质但忘改版本也使未提交计划过期，旧成品仍读保存属性");
        ruby.Hardness=8;
        check(plan.MatchesRules(recipe,catalog) && !plan.MatchesRules(recipe with{WeaponRules=rules with{Version="next"}},catalog),
            "确认时完整规则与制造计划匹配，模板规则版本变化不能静默提交");
        check(ManufacturingTransaction.Preview(design,recipe with{WeaponRules=null},catalog,new HashSet<string>(),slots,0,"missing").Failure==ManufacturingFailure.InvalidRecipe,
            "未配置属性规则的武器配方不产生空属性成品");
        var unconfigured=DefaultProcessing.Create();foreach(var m in unconfigured.Materials){m.Hardness=0;m.Weight=0;}
        check(ManufacturingTransaction.Preview(design,recipe,unconfigured,new HashSet<string>(),slots,0,"unconfigured").Failure==ManufacturingFailure.InvalidWeaponStats,
            "材质指数仍为待配置值时制造明确禁用");
        var detached=plan.Product;detached.FinalStats["minDamage"]=999;
        check(plan.Product.FinalStats["minDamage"]==6,"外部修改返回的属性副本不污染冻结制造计划");
    }
}
