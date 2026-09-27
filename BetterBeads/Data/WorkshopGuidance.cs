namespace BetterBeads.Data;

/// <summary>Read-only guidance derived from existing progress; never awards or changes a task.</summary>
public static class WorkshopGuidance
{
    public static string RewardName(string reward)=>reward switch
    {
        "season.0"=>ContentText.Get("copy.WorkshopGuidance.70994f61d2","春季首次完成委托"), "season.1"=>ContentText.Get("copy.WorkshopGuidance.375980e82c","夏季首次完成委托"),
        "season.2"=>ContentText.Get("copy.WorkshopGuidance.a8e81634b7","秋季首次完成委托"), "season.3"=>ContentText.Get("copy.WorkshopGuidance.972021ec66","冬季首次完成委托"),
        _=>WorkshopStories.All.FirstOrDefault(q=>q.Id==reward)?.Title??ContentText.Get("copy.WorkshopGuidance.aa358f4c84","入门图纸")
    };
    public static string Purpose(WorkshopCommission c)
    {
        int season=c.Week/4%4;
        string[] texts=c.Npc=="Gus"?new[]{ContentText.Get("copy.WorkshopGuidance.9520f858c0","酒馆想添一幅新芽或花束挂画，迎接春日的客人。"),ContentText.Get("copy.WorkshopGuidance.ec37c904d3","格斯想用海浪、贝壳或水果图案给酒馆添点凉意。"),ContentText.Get("copy.WorkshopGuidance.be1d442aea","格斯想用南瓜、落叶或丰收图案装点酒馆。"),ContentText.Get("copy.WorkshopGuidance.2d1624e06f","格斯想用星灯、雪景或暖色挂画装点冬日酒馆。")}:
            c.Npc=="Emily"?new[]{ContentText.Get("copy.WorkshopGuidance.d2cea8d80d","艾米丽正在搭配春游装，花朵和嫩叶都是好灵感。"),ContentText.Get("copy.WorkshopGuidance.581d226c89","艾米丽想尝试夏日穿搭，可以用海风或向日葵配色。"),ContentText.Get("copy.WorkshopGuidance.b3d29fd9a6","艾米丽正在搭配秋装，补丁、落叶和暖色都很合适。"),ContentText.Get("copy.WorkshopGuidance.1845b2cc77","艾米丽想设计冬日穿搭，可以尝试雪花或星光配色。")}:
            new[]{ContentText.Get("copy.WorkshopGuidance.54ed104cd4","罗宾想给春日小屋添一件手作，可以尝试花园或林木主题。"),ContentText.Get("copy.WorkshopGuidance.f92e9c6840","罗宾想布置夏日休憩角，可以尝试海滨或清凉的绿叶主题。"),ContentText.Get("copy.WorkshopGuidance.04bc35bce3","罗宾想布置收获季小屋，可以尝试南瓜、木纹或秋叶主题。"),ContentText.Get("copy.WorkshopGuidance.11160ff5aa","罗宾想布置冬日小屋，可以尝试雪人、松枝或温暖灯火主题。")};
        return texts[season]+ContentText.Get("copy.WorkshopGuidance.77a67a64ab","主题仅供灵感，图案和配色由你决定。");
    }
    public static ReferencePattern? SuggestedPattern(WorkshopProgress state,WorkshopCommission commission)=>ReferencePatterns.All
        .Where(p=>state.Patterns.Contains(p.Id))
        .Where(p=>commission.Matches(new ProductSnapshot{Design=ReferencePatterns.Create(p)}))
        .OrderByDescending(p=>p.Reward=="season."+(commission.Week/4%4))
        .ThenBy(p=>BeadCost(ReferencePatterns.Create(p))).FirstOrDefault();
    public static int BeadCost(Blueprint design)
    {
        var spec=ReferencePatterns.Template(design.TemplateId)!;
        return spec.WoolBudget??Math.Max(spec.MinimumMaterials,Math.Max(spec.StructureBudget,
            design.Views.Values.Sum(g=>g.Cells.Count(c=>c is not null))+design.SupplementaryMaterials.Values.Sum()));
    }
    public static string CostHint(WorkshopCommission c)
    {
        var spec=ReferencePatterns.Template(c.Template)!;
        if(spec.WoolBudget is {} wool)return ContentText.Format("copy.WorkshopGuidance.7eea797f56",$"制作固定消耗{wool}颗羊毛豆（所有方向合计）；羊毛或布料可自动折算。");
        return ContentText.Format("copy.WorkshopGuidance.a617273f6e",$"制作至少消耗{Math.Max(c.Minimum,Math.Max(spec.MinimumMaterials,spec.StructureBudget))}颗普通豆；按实际图案与结构预算中较高者计费。")+
            (ProductTemplates.IsPicture(c.Template)?ContentText.Get("copy.WorkshopGuidance.c5c6c80378","16格和32格画均可交付，精细画按实际豆数收费。"):"");
    }
    public static string RewardSummary(WorkshopProgress state,string id,int day)
    {
        var story=WorkshopStories.All.FirstOrDefault(q=>q.Id==id);
        int gold=story?.Gold??state.Commission?.Gold??0,friendship=story?.Friendship??75;
        var entries=new List<string>();
        if(gold>0)entries.Add(gold+ContentText.Get("copy.WorkshopGuidance.3c1e31a193","金"));if(friendship>0)entries.Add(friendship+ContentText.Get("copy.WorkshopGuidance.6305279d5c","友好度"));
        string reward=id=="commission"?"season."+(day/28%4):id;
        var patterns=ReferencePatterns.All.Where(p=>p.Reward==reward && !state.Patterns.Contains(p.Id)
            && (id!="commission" || !state.SeasonRewards.Contains(day/28%4))).Select(p=>p.Name).ToArray();
        if(patterns.Length>0)entries.Add(ContentText.Get("copy.WorkshopGuidance.207cd83136","图纸：")+string.Join("、",patterns));
        var unlocks=story?.Features.Where(f=>!state.Features.Contains(f)).Select(f=>f switch
            {"effects"=>ContentText.Get("copy.WorkshopGuidance.ea4f1d6156","单特效槽"),"refinement"=>ContentText.Get("copy.WorkshopGuidance.e02e6e9a0b","熔炉精炼"),"dual-effects"=>ContentText.Get("copy.WorkshopGuidance.fcbe6ab764","双特效槽"),"souls"=>ContentText.Get("copy.WorkshopGuidance.483b4c3034","银河之魂强化"),_=>f}).ToArray();
        if(unlocks?.Length>0)entries.Add(ContentText.Get("copy.WorkshopGuidance.e4d3ccb6bd","工艺：")+string.Join("、",unlocks));
        if(id=="robin")entries.Add(ContentText.Get("copy.WorkshopGuidance.5f7d6a1c5a","每七日自动委托"));
        return ContentText.Get("copy.WorkshopGuidance.de4ff756af","奖励：")+string.Join("；",entries)+"。";
    }
    public static string NextStep(string id)=>id switch
    {
        "robin"=>ContentText.Get("copy.WorkshopGuidance.e23b1efff6","当期委托已写入日志；明天留意艾米丽和克林特的来信。"),
        "emily"=>ContentText.Get("copy.WorkshopGuidance.a82a468b30","后续刷新可能出现服装委托；现有委托保持不变。"),
        "clint"=>ContentText.Get("copy.WorkshopGuidance.69c425e14f","现在可以为武器选择一种宝石特效；明天留意耐热试验来信。"),
        "refine"=>ContentText.Get("copy.WorkshopGuidance.0f3f2c01db","在武器的更多菜单取豆，再用熔炉精炼；明天开始马龙的实战任务。"),
        "marlon"=>ContentText.Get("copy.WorkshopGuidance.4be06764ef","可尝试两种宝石搭配；银河之魂强化仍需取得原生稀有材料。"),
        _=>ContentText.Get("copy.WorkshopGuidance.2dee012cc1","本期委托已完成，下个七日周期自动刷新。新图纸可在参考图纸中复制。")
    };
}

