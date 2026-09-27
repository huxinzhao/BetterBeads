using BetterBeads.Data;
using System.Text.Json;

internal static class WorkshopPolishChecks
{
    public static void Run(Action<bool,string> check)
    {
        ClothingTemplates.Configure(new());FurnitureTemplates.Configure(DefaultManufacturing.Furniture());WeaponTemplates.Configure(DefaultWeapons.Templates());
        var state=new WorkshopProgress();state.Completed["robin"]=1;
        for(int season=0;season<4;season++)
        {
            var offers=WorkshopStories.Offers(state,season*28);
            check(offers.All(c=>c.IsValid && WorkshopGuidance.Purpose(c).Contains("图案和配色由你决定")) && offers[2].Template==
                (season==3?DefaultManufacturing.StoneStatue:DefaultManufacturing.WoodOrnament),$"季节{season}委托有效，主题不限制手绘，冬季有雕像");
        }
        var gus=WorkshopStories.Offers(state,0)[1];
        check(WorkshopGuidance.SuggestedPattern(state,gus) is {} starter && state.Patterns.Contains(starter.Id),"只推荐已获得且符合交付条件的图纸");
        state.Patterns.Add("spring.flower");
        check(WorkshopGuidance.SuggestedPattern(state,gus)?.Id=="spring.flower","优先推荐已获得的当季主题");
        check(WorkshopGuidance.SuggestedPattern(state,WorkshopStories.Offers(state,7)[0]) is null,"没有桌图时不推荐锁定图纸");
        var clone=DesignStorage.Serialize(state);WorkshopGuidance.SuggestedPattern(state,gus);WorkshopGuidance.RewardSummary(state,"commission",0);
        check(DesignStorage.Serialize(state)==clone,"查看提示不修改进度");
        state.Completed["emily"]=2;var shirt=WorkshopStories.Offers(state,7)[2];
        check(WorkshopGuidance.CostHint(shirt).Contains("48颗羊毛豆") && !WorkshopGuidance.CostHint(shirt).Contains("普通豆"),"服装显示整件羊毛预算");
        ClothingTemplates.Configure(new(){ShirtWool=99});
        check(WorkshopGuidance.CostHint(shirt).Contains("99颗羊毛豆"),"服装提示遵循自定义预算");ClothingTemplates.Configure(new());
        check(WorkshopGuidance.CostHint(WorkshopStories.Offers(state,7)[0]).Contains("96颗普通豆") && WorkshopGuidance.CostHint(gus).Contains("32格"),"桌结构底价与双尺寸画收费明确");
        state.Commission=gus;
        check(WorkshopGuidance.RewardSummary(state,"commission",0).Contains("新芽盆栽") && !WorkshopGuidance.RewardSummary(state,"commission",0).Contains("春日花束"),"只展示这次新增的季节图纸");
        state.SeasonRewards.Add(0);
        check(!WorkshopGuidance.RewardSummary(state,"commission",0).Contains("图纸"),"已领季节首奖不再承诺图纸");
        state.Features.Add("effects");
        check(!WorkshopGuidance.RewardSummary(state,"clint",7).Contains("工艺") && WorkshopGuidance.RewardSummary(state,"clint",7).Contains("铜叶短刃"),"旧档已有工艺不误报新解锁");
        check(Enumerable.Range(0,4).Select(i=>WorkshopGuidance.RewardName("season."+i)).Distinct().Count()==4 && !WorkshopGuidance.RewardName("clint").Contains("clint"),"解锁条件为具体季节和中文任务名");
        var winter=new WorkshopProgress();winter.Completed["robin"]=1;winter.RefreshCommission(84,441);
        var saved=JsonSerializer.Deserialize<WorkshopProgress>(DesignStorage.Serialize(winter))!;
        check(!saved.RefreshCommission(85,441) && saved.Commission==winter.Commission,"冬季读档与隔日查看不重抽");
        int prior=saved.LastCommissionKind;saved.RefreshCommission(91,441);
        check(saved.LastCommissionKind!=prior && saved.Commission?.Week==13,"跨期替换且相邻不同类");
        var sculpture=WorkshopStories.Offers(new(),84)[2];
        var delivered=new ProductSnapshot{Design=ReferencePatterns.Create(ReferencePatterns.All.Single(p=>p.Id=="winter.snow"))};
        var deliveryState=new WorkshopProgress{Commission=sculpture,LastRotationWeek=12};deliveryState.Completed["robin"]=1;
        check(deliveryState.FinishCommission(84,delivered) && !deliveryState.FinishCommission(84,delivered) && !deliveryState.RefreshCommission(84,441),"冬季雕像可交付，每周期仍仅领奖一次");
        var old=new WorkshopProgress{Version=1,Commission=new(0,"Robin",DefaultManufacturing.WoodOrnament,"旧冬日摆件",250,16)};
        old.Completed["robin"]=1;old.UpgradeFromPrevious(84);
        check(old.Commission?.Template==DefaultManufacturing.WoodOrnament && !old.RefreshCommission(84,441),"旧当期摆件不强换雕像");
        foreach(var (lines,choices,capacity) in new[]{(1,0,1),(5,2,2),(6,2,2),(23,2,2),(7,21,3),(19,0,1)})
        {
            var seenLines=new HashSet<int>();var seenChoices=new HashSet<int>();var initial=WorkshopPages.Calculate(0,lines,choices,capacity);
            for(int page=0;page<initial.Count;page++)
            {
                var p=WorkshopPages.Calculate(page,lines,choices,capacity);
                for(int n=p.DescriptionStart;n<Math.Min(lines,p.DescriptionStart+p.DescriptionCount);n++)seenLines.Add(n);
                for(int n=p.ChoiceStart;n<Math.Min(choices,p.ChoiceStart+capacity);n++)seenChoices.Add(n);
            }
            check(seenLines.Count==lines && seenChoices.Count==choices && WorkshopPages.Calculate(999,lines,choices,capacity).Page==initial.Count-1,
                $"{lines}行说明/{choices}选项全部可达，缩放后页码受限");
        }
        var table=ReferencePatterns.Create(ReferencePatterns.All.Single(p=>p.Id=="summer.table"));
        var bill=DraftAssessment.Evaluate(table,ReferencePatterns.Template(table.TemplateId)!,DefaultProcessing.Create(),new HashSet<string>(),new Dictionary<string,int>{{"decoration",9999}});
        check(bill.Materials.Single().Needed==WorkshopGuidance.BeadCost(table),"推荐用料与制作计费一致");
        foreach(var pattern in ReferencePatterns.All)Console.WriteLine($"COST {pattern.Id}: {WorkshopGuidance.BeadCost(ReferencePatterns.Create(pattern))}");
    }
}
