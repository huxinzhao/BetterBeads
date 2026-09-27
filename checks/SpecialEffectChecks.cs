using BetterBeads.Data;

internal static class SpecialEffectChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        catalog.Materials.RemoveAll(m=>m.RefinedFrom is not null);
        check(catalog.Materials.Count(m=>m.Series==MaterialSeries.Metal)==4 && catalog.Materials.Count(m=>m.Series==MaterialSeries.Gem)==7,
            "金属四种与宝石七种分别记录系列，其他材料不冒用武器系列");
        catalog.Materials.Add(new(){Id="test.refined-ruby",Series=MaterialSeries.Gem,RefinedFrom="ruby",EffectGroup="ruby",
            Hardness=11,Weight=4,AllowedUses=new(){ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer}});
        check(catalog.Validate().Count==0,"独立精炼材料身份保持来源、用途与效果组一致");
        SpecialEffectReport Evaluate(int ruby,int jade=0,int amethyst=0,params string[] selected)=>SpecialEffects.Evaluate(ProductUse.Sword,catalog,
            new Dictionary<string,int>{{"ruby",ruby},{"jade",jade},{"amethyst",amethyst},{"copper",100-ruby-jade-amethyst}}
                .Where(p=>p.Value>0).ToDictionary(p=>p.Key,p=>p.Value),selected);
        var weak=Evaluate(9,selected:new[]{"ruby"});
        check(weak.Ratios.Single(r=>r.Id=="ruby").State==SpecialEffectState.Ready && Math.Abs(weak.DamageBonusPercent-5.4)<0.000001 && weak.DamagePenaltyPercent==0,
            "红宝石9%连续提供5.4%增伤且无过量处罚");
        check(Evaluate(10,selected:new[]{"ruby"}).DamageBonusPercent==6 && Evaluate(25,selected:new[]{"ruby"}).DamageBonusPercent==12,
            "红宝石10%提供6%增伤，25%封顶12%");
        var excess=Evaluate(26,selected:new[]{"ruby"});
        check(excess.DamageBonusPercent==12 && excess.DamagePenaltyPercent==0
            && Evaluate(26).DamagePenaltyPercent==0 && Evaluate(26).DamageBonusPercent==0,"超过25%保持封顶增益，无惩罚；未选择时不激活");
        check(Evaluate(26,26,26).DamagePenaltyPercent==0,"三种特殊豆同时高占比也没有过量惩罚");
        var pair=Evaluate(10,10,10,"jade","amethyst");
        check(pair.LifeStealRate==0.015 && pair.KnockbackBonusPercent==10 && pair.DamageBonusPercent==0 && pair.Active.Count==2,
            "合格材料只激活玩家选择的两种效果：1.5%吸血与10%击退");
        check(Evaluate(10,10,10,"ruby","jade","amethyst").Errors.Contains("too-many-effects"),
            "即使三种比例都合格也不能同时选择超过两种效果");
        var mixed=SpecialEffects.Evaluate(ProductUse.Sword,catalog,new Dictionary<string,int>{{"ruby",5},{"test.refined-ruby",5},{"copper",90}},new[]{"ruby"});
        check(mixed.DamageBonusPercent==6 && mixed.Ratios.Single(r=>r.Id=="ruby") is {Count:10,Total:100},
            "普通与精炼同源红宝石合并占比，一颗精炼豆仍只计一颗");
        var absent=Evaluate(0);
        check(absent.Ratios.All(r=>r.State==SpecialEffectState.Absent) && absent.DamagePenaltyPercent==0,
            "未投入的特殊豆为不存在状态，不误报能量不足");
        catalog.Materials.Last().EffectGroup="jade";
        check(catalog.Validate().Any(i=>i.StartsWith("material.refinement:"))
            && SpecialEffects.Evaluate(ProductUse.Sword,catalog,new Dictionary<string,int>{{"test.refined-ruby",10},{"copper",90}},new[]{"jade"}).Errors.Count>0,
            "精炼红宝石不能伪装成翡翠效果组，配置与计算均拒绝");
    }
}
