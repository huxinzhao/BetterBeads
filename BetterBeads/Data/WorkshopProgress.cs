namespace BetterBeads.Data;

public sealed class WorkshopProgress
{
    public int Version {get;set;}=2;
    public int FirstBenchDay {get;set;}=-1;
    public bool IntroSent {get;set;}
    public Dictionary<string,int> Completed {get;set;}=new();
    public HashSet<string> Features {get;set;}=new();
    public HashSet<string> Patterns {get;set;}=new(){"starter.sprout","starter.heart"};
    public HashSet<string> DeliveredInstances {get;set;}=new();
    public HashSet<int> SeasonRewards {get;set;}=new();
    public int Kills {get;set;}
    public int AcceptedWeek {get;set;}=-1;
    public int RewardWeek {get;set;}=-1;
    public int LastRotationWeek {get;set;}=-1;
    public int LastCommissionKind {get;set;}=-1;
    public WorkshopCommission? Commission {get;set;}
    public bool IsValid=>Version==2 && FirstBenchDay>=-1 && Kills is >=0 and <=10 && AcceptedWeek>=-1 && RewardWeek>=-1
        && LastRotationWeek>=-1 && LastCommissionKind is >=-1 and <=2
        && Completed is not null && Features is not null && Patterns is not null && DeliveredInstances is not null && SeasonRewards is not null
        && Completed.All(p=>WorkshopStories.All.Any(q=>q.Id==p.Key) && p.Value>=0)
        && (Commission is null || Commission.IsValid);
    public static WorkshopProgress Legacy()=>new(){Features=new(){"effects","refinement","dual-effects","souls"}};
    public void UpgradeFromPrevious(int day)
    {
        if(Version!=1)return;
        Version=2;
        if(Commission is {} previous)
        {
            int week=day/7;
            Commission=previous with{Week=week,Gold=previous.Template==DefaultManufacturing.Table?750:previous.Gold};LastRotationWeek=week;
            LastCommissionKind=WorkshopStories.Kind(previous);
        }
        else if(RewardWeek==day/7)LastRotationWeek=day/7;
    }
    public int EffectSlots(bool creative=false)=>creative || Features.Contains("dual-effects")?2:Features.Contains("effects")?1:0;
    public bool CanMake(Blueprint design,ProcessingCatalog catalog,bool creative=false)
    {
        if(!DesignStorage.IsStructurallyValid(design))return false;
        if(creative || !WeaponMaterials.IsWeapon(design.Use))return true;
        if(design.SelectedEffects.Count>EffectSlots())return false;
        var ids=design.Views.Values.SelectMany(g=>g.Cells).Where(c=>c is not null).Select(c=>c!.MaterialId)
            .Concat(design.SupplementaryMaterials.Where(p=>p.Value>0).Select(p=>(string?)p.Key));
        return Features.Contains("refinement") || !ids.Any(id=>catalog.Materials.Any(m=>m.Id==id && m.RefinedFrom is not null));
    }
    public bool Available(string id,int day)
    {
        var quest=WorkshopStories.All.FirstOrDefault(q=>q.Id==id);
        if(quest is null || Completed.ContainsKey(id))return false;
        return quest.Previous is null?FirstBenchDay>=0 && day>FirstBenchDay:Completed.TryGetValue(quest.Previous,out int done) && day>done;
    }
    public bool RefreshCommission(int day,long saveId)
    {
        int week=day/7;
        if(!Completed.ContainsKey("robin") || LastRotationWeek==week)return false;
        LastRotationWeek=week;
        if(RewardWeek==week){Commission=null;return false;}
        var offers=WorkshopStories.Offers(this,day);
        var available=Enumerable.Range(0,offers.Count).Where(i=>i!=LastCommissionKind).ToArray();
        int seed=unchecked((int)(saveId^(saveId>>32)) ^ (week*unchecked((int)0x9E3779B9)));
        int choice=available[new Random(seed).Next(available.Length)];
        Commission=offers[choice];LastCommissionKind=choice;
        return true;
    }
    public void RecordKill(int day,bool creativeWeapon)
    {if(!creativeWeapon && Available("marlon",day))Kills=Math.Min(10,Kills+1);}
    public bool CanDeliver(ProductSnapshot? item)=>item is not null && !item.CreatedInCreativeMode
        && !DeliveredInstances.Contains(item.InstanceId) && ProductTemplates.Matches(item);
    public bool FinishStory(string id,int day,ProductSnapshot? item)
    {
        if(!Available(id,day) || !WorkshopStories.Matches(this,id,item))return false;
        var quest=WorkshopStories.All.Single(q=>q.Id==id);Completed[id]=day;
        if(quest.Consumed && item is not null)DeliveredInstances.Add(item.InstanceId);
        foreach(var feature in quest.Features)Features.Add(feature);
        foreach(var pattern in ReferencePatterns.All.Where(p=>p.Reward==id))Patterns.Add(pattern.Id);
        return true;
    }
    public bool FinishCommission(int day,ProductSnapshot? item)
    {
        if(Commission is null || Commission.Week!=day/7 || LastRotationWeek!=day/7 || RewardWeek==day/7
            || !CanDeliver(item) || !Commission.Matches(item!))return false;
        DeliveredInstances.Add(item!.InstanceId);RewardWeek=day/7;Commission=null;
        int season=day/28%4;
        if(SeasonRewards.Add(season))foreach(var pattern in ReferencePatterns.All.Where(p=>p.Reward=="season."+season))Patterns.Add(pattern.Id);
        return true;
    }
}

public sealed record WorkshopStory(string Id,string Npc,string Title,string Description,string? Previous,int Gold,int Friendship,bool Consumed,string[] Features);
public sealed record WorkshopCommission(int Week,string Npc,string Template,string Title,int Gold,int Minimum)
{
    public bool IsValid=>Week>=0 && Gold>0 && Minimum>0 && (Npc is "Robin" or "Gus" or "Emily") && ReferencePatterns.Template(Template) is not null;
    public bool Matches(ProductSnapshot item)=>(item.Design.TemplateId==Template || ProductTemplates.IsPicture(Template) && ProductTemplates.IsPicture(item.Design.TemplateId))
        && item.Design.Views.Values.All(g=>g.Cells.Count(c=>c is not null)>=Minimum);
}
public static class WorkshopStories
{
    public static int Kind(WorkshopCommission commission)=>commission.Npc=="Gus"?1:
        commission.Template is DefaultManufacturing.Chair or DefaultManufacturing.Table?0:2;
    public static IReadOnlyList<WorkshopStory> All {get;}=new WorkshopStory[]
    {
        new("robin","Robin",ContentText.Get("copy.WorkshopProgress.777725ec2a","第一件手作"),ContentText.Get("copy.WorkshopProgress.f27727ce2b","罗宾想为工坊添一幅拼豆画。交付一幅至少32颗豆的拼豆画；图案和配色由你决定。完成后开放每周委托，获得两张图纸。"),null,200,50,true,Array.Empty<string>()),
        new("emily","Emily",ContentText.Get("copy.WorkshopProgress.1ce470add2","把作品穿出去"),ContentText.Get("copy.WorkshopProgress.77eb23335f","艾米丽想看看你的穿搭。交付一顶四向完整、每向至少8颗豆的拼豆帽。完成后开放服装委托，获得两张图纸。"),"robin",350,50,true,Array.Empty<string>()),
        new("clint","Clint",ContentText.Get("copy.WorkshopProgress.7cba4bfc85","金属也能拼"),ContentText.Get("copy.WorkshopProgress.bfc5cc0712","向克林特展示一件铜、铁及其精炼豆合计至少占60%的拼豆武器。武器检测后归还。完成后开放一个特效槽，获得两张图纸。"),"robin",0,25,false,new[]{"effects"}),
        new("refine","Clint",ContentText.Get("copy.WorkshopProgress.66414afe62","耐热试验"),ContentText.Get("copy.WorkshopProgress.b4dfdb1f78","克林特准备测试拼豆的耐热性。交付2铁锭和1煤炭，学习用熔炉精炼并获得两张图纸。"),"clint",0,25,false,new[]{"refinement"}),
        new("marlon","Marlon",ContentText.Get("copy.WorkshopProgress.7c4f80c0b5","真正派上用场"),ContentText.Get("copy.WorkshopProgress.4e9bd301cd","用非创造模式拼豆武器击败10只怪物，再向马龙报告。开启双特效与银河之魂强化，并获得两张图纸。"),"refine",0,0,false,new[]{"dual-effects","souls"})
    };
    public static bool Matches(WorkshopProgress state,string id,ProductSnapshot? item)=>id switch
    {
        "refine"=>true, // Inventory transaction separately requires two iron bars and one coal.
        "marlon"=>state.Kills>=10,
        "robin"=>state.CanDeliver(item) && ProductTemplates.IsPicture(item!.Design.TemplateId) && item.Design.Views["front"].Cells.Count(c=>c is not null)>=32,
        "emily"=>state.CanDeliver(item) && item!.Design.TemplateId==ProductTemplates.Hat && item.Design.Views.Count==4 && item.Design.Views.Values.All(g=>g.Cells.Count(c=>c is not null)>=8),
        "clint"=>state.CanDeliver(item) && WeaponMaterials.IsWeapon(item!.Design.Use) && item.ActualMaterials.Values.Sum(v=>(long)v)>0
            && item.ActualMaterials.Where(p=>p.Key is "copper" or "iron" or "refined-copper" or "refined-iron").Sum(p=>(long)p.Value)*100>=item.ActualMaterials.Values.Sum(v=>(long)v)*60,
        _=>false
    };
    public static IReadOnlyList<WorkshopCommission> Offers(WorkshopProgress state,int day)
    {
        int week=day/7;string theme=new[]{ContentText.Get("copy.WorkshopProgress.2d9993166d","春日新芽"),ContentText.Get("copy.WorkshopProgress.1039824c5a","夏日海风"),ContentText.Get("copy.WorkshopProgress.3d1710d2c9","秋日收获"),ContentText.Get("copy.WorkshopProgress.2b133801cf","冬日星光")}[day/28%4];
        var result=new List<WorkshopCommission>{new(week,"Robin",week%2==0?DefaultManufacturing.Chair:DefaultManufacturing.Table,theme+ContentText.Get("copy.WorkshopProgress.f93cb12e8e"," · 家居"),week%2==0?350:750,16),
            new(week,"Gus",ProductTemplates.Picture,theme+ContentText.Get("copy.WorkshopProgress.10be1dfa0f"," · 酒馆挂画"),250,32)};
        if(state.Completed.ContainsKey("emily"))
        {
            int kind=week%3;result.Add(new(week,"Emily",kind==0?ProductTemplates.Hat:kind==1?ClothingTemplates.Shirt:ClothingTemplates.Pants,theme+ContentText.Get("copy.WorkshopProgress.48f9d29ee0"," · 穿搭"),kind==0?450:kind==1?800:1150,8));
        }
        else result.Add(new(week,"Robin",day/28%4==3?DefaultManufacturing.StoneStatue:DefaultManufacturing.WoodOrnament,theme+ContentText.Get("copy.WorkshopProgress.28509538a0"," · 小摆件"),250,16));
        return result;
    }
}
