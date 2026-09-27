using System.Text.Json;
using BetterBeads.Data;

internal static class GameplayChecks
{
    public static void Run(Action<bool,string> check)
    {
        ClothingTemplates.Configure(new());FurnitureTemplates.Configure(DefaultManufacturing.Furniture());WeaponTemplates.Configure(DefaultWeapons.Templates());
        var catalog=DefaultProcessing.Create();var p=new WorkshopProgress();
        check(catalog.RulesVersion=="5" && catalog.Match("(O)428",-1)?.Yield==36,"0.16默认规则与布料折算36豆");
        var custom=JsonSerializer.Deserialize<ProcessingCatalog>(DesignStorage.Serialize(catalog))!;custom.RulesVersion="4";
        custom.Materials.Single(m=>m.Id=="diamond").Hardness=10;custom.Materials.Single(m=>m.Id=="refined-diamond").Hardness=99;
        custom.Sources.Single(s=>s.ItemId=="(O)428").Yield=45;
        var migrated=GameplayBalance.Upgrade(custom)!;
        check(migrated.Materials.Single(m=>m.Id=="diamond").Hardness==6 && migrated.Materials.Single(m=>m.Id=="refined-diamond").Hardness==99
            && migrated.Sources.Single(s=>s.ItemId=="(O)428").Yield==45 && custom.Materials.Single(m=>m.Id=="diamond").Hardness==10,"迁移只改旧默认硬度，保留独立定制精炼值与布料产量且不改源对象");
        check(GameplayBalance.Upgrade(migrated) is null,"已迁移配置不重复改写");
        var clothes=new ClothingSettings{HatWool=80,ShirtWool=99,PantsWool=160};GameplayBalance.UpgradeClothing(clothes);
        check(clothes.HatWool==24 && clothes.ShirtWool==99 && clothes.PantsWool==72,"服装预算逐项迁移，自定义99保留");
        check(GameplayBalance.ProtectedSource("(O)709",0,false) && GameplayBalance.ProtectedSource("flower",-80,false) && !GameplayBalance.ProtectedSource("(O)388",0,false) && !GameplayBalance.ProtectedSource("(O)709",0,true),"默认保护花卉硬木，开关允许显式使用");
        var slots=new InventorySlot?[]{new("cloth","(O)428",2,999,RawMaterial:"wool",Yield:36),new("wool","(O)440",1,999,RawMaterial:"wool",Yield:24),null};
        MaterialSupply.Consume(slots,new Dictionary<string,int>{{"wool",48}},catalog,out var surplus);
        check(slots[1] is null && slots[0]?.Count==1 && surplus["wool"]==12,"羊毛先于布料，48豆费用消耗24加36并返12余豆");
        foreach(var (id,floor) in new[]{(ProductTemplates.Picture,1),(DefaultManufacturing.WoodOrnament,16),(DefaultManufacturing.StoneStatue,16),(DefaultManufacturing.Chair,48),(DefaultManufacturing.Table,96)})
        {
            var t=ReferencePatterns.Template(id)!;var b=Blank(t);b.Views["front"].Cells[0]=Cell();
            var bill=DraftAssessment.Evaluate(b,t,catalog,new HashSet<string>(),new Dictionary<string,int>{{"decoration",999}});
            check(bill.CanMake && bill.Materials.Single().Needed==floor,$"{id}单豆图案正确收取结构成本{floor}");
        }
        var sword=DefaultWeapons.Recipes().First(r=>r.Template.Use==ProductUse.Sword);
        var diamond=WeaponStats.Calculate(ProductUse.Sword,sword.WeaponRules!,catalog,new Dictionary<string,int>{{"diamond",16}}).Values!;
        check(diamond.MinDamage==30 && diamond.MaxDamage==42 && diamond.Speed==1,"钻石剑30至42、速度+1，轻量但不替代铱豆伤害");
        var iron=WeaponStats.Calculate(ProductUse.Sword,sword.WeaponRules!,catalog,new Dictionary<string,int>{{"iron",16}}).Values!;
        check(iron.MinDamage==25 && iron.MaxDamage==35,"铁剑默认性能保持25至35");
        double previous=-1;bool continuous=true;
        foreach(int count in new[]{0,1,10,19,20,21,40,100})
        {
            var bill=new Dictionary<string,int>();if(count>0)bill["ruby"]=count;if(count<100)bill["iron"]=100-count;
            var effect=SpecialEffects.Evaluate(ProductUse.Sword,catalog,bill,new[]{"ruby"});
            continuous&=effect.DamageBonusPercent>=previous && effect.DamageBonusPercent==12*Math.Min(count/20d,1) && effect.DamagePenaltyPercent==0;previous=effect.DamageBonusPercent;
        }
        check(continuous,"红宝石收益由0渐进到12%，超过20%不回落、不惩罚");
        var heal=SpecialEffects.Evaluate(ProductUse.Sword,catalog,new Dictionary<string,int>{{"jade",20},{"amethyst",20},{"iron",60}},new[]{"jade","amethyst"});
        check(heal.LifeStealRate==0.03 && heal.KnockbackBonusPercent==20,"双特效上限为3%吸血和20%击退");
        double remainder=0;int gained=0;for(int i=0;i<10;i++)gained+=LifeSteal.HealRate(7,50+gained,100,0.03,ref remainder);
        check(gained==2 && Math.Abs(remainder-0.1)<1e-8,"连续小额伤害累计比例吸血，不按每次命中向上取整");
        LifeSteal.HealRate(10,100,100,0.03,ref remainder);check(remainder==0,"满血清空比例吸血余量");
        var legacy=Snapshot(ReferencePatterns.Create(ReferencePatterns.All.Single(x=>x.Id=="clint.sword")));
        legacy.WeaponRulesVersion="weapons-1";legacy.EffectRulesVersion="effects-1";legacy.Effects=new(){"jade"};legacy.EffectParameters=new(){{"lifeStealRate",0.05},{"damagePenaltyPercent",10}};
        check(ProductTemplates.Matches(legacy) && SoulUpgrades.TryUpgrade(legacy,out _) && DesignStorage.TryReadSnapshot(DesignStorage.Serialize(legacy),out var read) && read!.EffectParameters["lifeStealRate"]==0.05,"旧武器5%吸血、旧惩罚与银河之魂仍可读取，不重算旧属性");
        var fresh=legacy.Copy();fresh.WeaponRulesVersion="weapons-2";fresh.Effects=heal.Active.ToList();fresh.EffectRulesVersion=SpecialEffects.RulesVersion;fresh.EffectParameters=SpecialEffects.Parameters(heal);
        check(ProductTemplates.Matches(fresh) && SoulUpgrades.TryUpgrade(fresh,out _),"新版连续特效快照支持保存与银河之魂");
        check(p.EffectSlots()==0 && WorkshopProgress.Legacy().EffectSlots()==2 && WorkshopProgress.Legacy().Features.Contains("refinement"),"新档高级能力随任务开放，旧档保留原有能力");
        var broken=new Blueprint{Use=ProductUse.Sword,SelectedEffects=null!};
        check(!p.CanMake(broken,catalog),"损坏图纸进入能力检查时安全拒绝");
        p.FirstBenchDay=0;var picture=Snapshot(ReferencePatterns.Create(ReferencePatterns.All[0]));
        check(!p.Available("robin",0) && p.Available("robin",1) && p.FinishStory("robin",1,picture) && !p.FinishStory("robin",1,picture),"罗宾任务次日出现且只能完成一次");
        check(!p.Available("emily",1) && p.Available("emily",2) && p.Available("clint",2),"生活和战斗支线次日独立开放");
        var weapon=Snapshot(ReferencePatterns.Create(ReferencePatterns.All.Single(x=>x.Id=="clint.dagger")));
        check(p.FinishStory("clint",2,weapon) && !p.DeliveredInstances.Contains(weapon.InstanceId) && p.EffectSlots()==1,"克林特检测归还武器并开放一个特效槽，不要求艾米丽任务");
        check(!p.Available("refine",2) && p.FinishStory("refine",3,null) && p.Features.Contains("refinement"),"耐热任务随后开放精炼，材料由运行时事务核验");
        p.RecordKill(3,false);p.RecordKill(4,true);check(p.Kills==0,"任务开始前和创造模式武器击杀不计数");
        for(int i=0;i<12;i++)p.RecordKill(4,false);
        check(p.Kills==10 && p.FinishStory("marlon",4,null) && p.EffectSlots()==2 && p.Features.Contains("souls"),"实战计数封顶10并解锁双特效与强化");
        var creative=picture.Copy();creative.InstanceId=Guid.NewGuid().ToString("N");creative.CreatedInCreativeMode=true;
        check(!p.CanDeliver(creative) && !p.CanDeliver(picture),"创造作品与已交付实例不能再次换奖励");
        const long saveId=246813579;
        check(p.RefreshCommission(4,saveId) && p.Commission?.Week==0 && p.LastRotationWeek==0,"罗宾故事完成后当期自动生成委托");
        var initial=p.Commission!;
        check(!p.RefreshCommission(4,saveId) && !p.RefreshCommission(5,saveId) && p.Commission==initial,"当期读档和次日不会重抽");
        var same=JsonSerializer.Deserialize<WorkshopProgress>(DesignStorage.Serialize(p))!;
        check(!same.RefreshCommission(4,saveId) && same.Commission==initial,"存档往返维持同一委托");
        check(p.RefreshCommission(7,saveId) && p.Commission?.Week==1 && WorkshopStories.Kind(p.Commission!)!=WorkshopStories.Kind(initial),"七日到期自动替换未完成委托且不连出同类");
        var deterministic=new WorkshopProgress();deterministic.Completed["robin"]=1;
        check(deterministic.RefreshCommission(4,saveId) && deterministic.Commission==initial,"同一存档和周期结果稳定");
        check(WorkshopStories.Offers(p,7).Single(c=>c.Template==DefaultManufacturing.Table).Gold==750,"桌子委托报酬为750金");
        check(p.RefreshCommission(28,saveId) && p.Commission?.Week==4,"跨季刷新新一期委托");
        var commission=p.Commission!;
        var template=ReferencePatterns.Template(commission.Template)!;
        var commissionDesign=Blank(template);
        foreach(var grid in commissionDesign.Views.Values)for(int i=0;i<Math.Max(commission.Minimum,template.MinimumMaterials);i++)grid.Cells[i]=Cell();
        var delivery=Snapshot(commissionDesign);
        check(p.FinishCommission(28,delivery) && p.RewardWeek==4 && p.SeasonRewards.Contains(1) && p.Patterns.Contains("summer.shell"),"当期交付且领取夏季首奖");
        check(!p.FinishCommission(28,delivery) && !p.RefreshCommission(28,saveId),"每周期最多领奖一次且当期不再刷新");
        var old=new WorkshopProgress{Version=1,Commission=new WorkshopCommission(0,"Gus",ProductTemplates.Picture,"旧委托",250,32),AcceptedWeek=0};
        old.Completed["robin"]=1;old.UpgradeFromPrevious(10);
        check(old.Version==2 && old.Commission?.Week==1 && old.LastRotationWeek==1 && !old.RefreshCommission(10,saveId) && old.RefreshCommission(14,saveId),"旧版已接委托保留至本期结束，下一期转自动刷新");
        var oldTable=new WorkshopProgress{Version=1,Commission=new WorkshopCommission(0,"Robin",DefaultManufacturing.Table,"旧桌子委托",500,16)};
        oldTable.Completed["robin"]=1;oldTable.UpgradeFromPrevious(10);
        check(oldTable.Commission?.Gold==750 && oldTable.Commission.Week==1,"旧版未完成桌子委托本期报酬同步调整至750金");
        var rewarded=new WorkshopProgress{Version=1,RewardWeek=2};rewarded.Completed["robin"]=1;rewarded.UpgradeFromPrevious(16);
        check(!rewarded.RefreshCommission(16,saveId) && rewarded.Commission is null,"旧版本期已领报酬不重复生成");
        var restored=JsonSerializer.Deserialize<WorkshopProgress>(DesignStorage.Serialize(p))!;
        check(restored.IsValid && restored.Patterns.SetEquals(p.Patterns) && restored.DeliveredInstances.SetEquals(p.DeliveredInstances),"任务、解锁、周限制与交付记录保存往返一致");
        bool patterns=true;foreach(var pattern in ReferencePatterns.All)
        {
            var first=ReferencePatterns.Create(pattern);var second=ReferencePatterns.Create(pattern);var spec=ReferencePatterns.Template(pattern.TemplateId)!;
            patterns&=DesignStorage.IsStructurallyValid(first) && first.Id!=second.Id && first.Views.Count==spec.Views.Length
                && first.Views.Values.All(g=>g.Width==spec.Width && g.Height==spec.Height && g.Cells.Count(c=>c is not null)>=8)
                && first.Views.Values.Any(g=>g.Cells.Where(c=>c is not null).Select(c=>c!.Rgba).Distinct().Count()>=3)
                && ProductTemplates.Matches(Snapshot(first))
                && first.Views.Values.SelectMany(g=>g.Cells).Where(c=>c is not null).All(c=>
                    c!.MaterialId==(WeaponMaterials.IsWeapon(first.Use)?pattern.Id=="clint.dagger"?"copper":"iron":
                        ClothingTemplates.IsClothing(first.Use)?null:"decoration"));
            first.Name="changed";patterns&=second.Name==pattern.Name;
        }
        check(ReferencePatterns.All.Count==20 && patterns,"20张图纸按实际尺寸、方向及至少三种豆色制作且彼此独立");
        check(new[]{"emily.hat","emily.shirt"}.All(id=>
        {
            var views=ReferencePatterns.Create(ReferencePatterns.All.Single(p=>p.Id==id)).Views;
            return views.Values.Select(g=>string.Join(",",g.Cells.Select(c=>c?.Rgba??0))).Distinct().Count()==4;
        }),"帽子和上衣四个方向均为独立绘制图案");
        var copies=new SaveProgress();var library=new BlueprintRepository(copies);var source=ReferencePatterns.Create(ReferencePatterns.All[0]);
        check(library.SaveAs(source,"副本一",out var copy1) && library.SaveAs(source,"副本二",out var copy2)
            && copy1!.Id!=copy2!.Id && library.TryOpen(copy1.Id,out var original) && original!.Name=="副本一"
            && copies.BlueprintRecords.Count==2,"连续复制同一图纸生成独立ID，不覆盖前一份");
        foreach(var (w,h) in new[]{(480,420),(1280,900)})
        {
            var d=PatternDetailLayout.Calculate(w,h);var viewport=new UiRect(0,0,w,h);
            check(viewport.Contains(d.Frame) && d.Frame.Contains(d.Preview) && d.Frame.Contains(d.Directions)
                && d.Frame.Contains(d.Details) && d.Frame.Contains(d.Back) && d.Frame.Contains(d.Copy)
                && !d.Preview.Overlaps(d.Details) && !d.Details.Overlaps(d.Copy),$"{w}×{h}图纸详情预览与操作不越界重叠");
        }
        var confirm=WorkshopMenuLayout.Calculate(1280,900,3,2);
        check(confirm.Count==2 && confirm.Frame.Height==328 && !confirm.Rows.Overlaps(confirm.Back),"确认页围绕两项操作收紧，避免大片空白");
        foreach(var (w,h) in new[]{(480,420),(1280,900)})
        {
            var l=WorkshopMenuLayout.Calculate(w,h,5);var viewport=new UiRect(0,0,w,h);
            check(viewport.Contains(l.Frame) && l.Frame.Contains(l.Description) && l.Frame.Contains(l.Rows) && !l.Rows.Overlaps(l.Back) && !l.Previous.Overlaps(l.Next) && l.Back.Height>=40,$"{w}×{h}任务与参考图纸菜单不越界且操作可见");
        }
    }
    private static BeadCell Cell()=>new(){ColorId="rgb.112233",Rgba=0x112233FF,MaterialId="decoration"};
    private static Blueprint Blank(TemplateSpec t)=>new(){TemplateId=t.Id,Use=t.Use,Views=t.Views.ToDictionary(v=>v,_=>new BeadGrid{Width=t.Width,Height=t.Height,Cells=Enumerable.Repeat<BeadCell?>(null,t.Width*t.Height).ToList()})};
    private static ProductSnapshot Snapshot(Blueprint d)=>new(){Design=d,ActualMaterials=new(){{WeaponMaterials.IsWeapon(d.Use)?"iron":ClothingTemplates.IsClothing(d.Use)?"wool":"decoration",32}},
        WeaponRulesVersion=WeaponMaterials.IsWeapon(d.Use)?"weapons-2":null,FinalStats=WeaponMaterials.IsWeapon(d.Use)?new WeaponStatValues(25,35,0,0.8f,0.02f,3).ToSnapshot():new()};
}
