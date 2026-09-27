using System.Text.Json;
using BetterBeads.Data;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace BetterBeads.Runtime;
internal sealed class WorkshopService
{
    private const string Prefix="xinzh.BetterBeads.Workshop.";
    public static WorkshopService? Current {get;private set;}
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly Func<SaveProgress?> save;
    private readonly ProductItems products;
    private bool nativeChat,submitting;
    public WorkshopProgress? State=>save()?.Workshop;
    public static int Today=>Game1.Date.TotalDays;
    public static bool Has(string feature)=>PlayMode.Creative || Current?.State?.Features.Contains(feature)==true;
    public WorkshopService(IModHelper helper,IMonitor monitor,Func<SaveProgress?> save,ProductItems products)
    {
        this.helper=helper;this.monitor=monitor;this.save=save;this.products=products;Current=this;
        helper.Events.Content.AssetRequested+=(_,e)=>
        {
            if(e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))e.Edit(a=>
            {
                var data=a.AsDictionary<string,string>().Data;
                foreach(var q in WorkshopStories.All)data[Prefix+q.Id]=$"Basic/{q.Title}/{q.Description}/{q.Description}/-1//-1//false";
                data[Prefix+"commission"]=ContentText.Get("copy.WorkshopService.9ce221cab6","Basic/拼豆委托/制作一件村民需要的作品。/前往委托人处确认并交付作品。/-1//-1//false");
            });
            if(e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))e.Edit(a=>
            {
                var data=a.AsDictionary<string,string>().Data;
                foreach(var q in WorkshopStories.All)data[Prefix+q.Id]=ContentText.Format("copy.WorkshopService.693902594e",$"你好！^^{q.Description}^^不必赶时间；准备好后带上作品来找我。——{NpcName(q.Npc)}[#]{q.Title}");
            });
        };
        new Harmony(Prefix+"Npc").Patch(AccessTools.Method(typeof(NPC),"checkAction"),prefix:new(typeof(WorkshopService),nameof(Interact)));
    }
    public void OpenedBench(){if(State is {} p && p.FirstBenchDay<0)p.FirstBenchDay=Today;}
    public void DayStarted()
    {
        if(!Context.IsWorldReady || Context.IsMultiplayer || State is not {} p)return;
        foreach(var q in WorkshopStories.All.Where(q=>p.Available(q.Id,Today)))
        {
            string id=Prefix+q.Id;
            if(q.Id=="robin")p.IntroSent=true;
            if(!Game1.player.mailReceived.Contains(id) && !Game1.player.mailbox.Contains(id) && !Game1.player.mailForTomorrow.Contains(id))Game1.player.mailbox.Add(id);
        }
        bool refreshed=p.RefreshCommission(Today,unchecked((long)Game1.uniqueIDForThisGame));
        SyncJournal();
        if(refreshed)Game1.addHUDMessage(new HUDMessage(ContentText.Get("copy.WorkshopService.bf83011efb","新的拼豆委托已写入任务日志。")));
    }
    public void SyncJournal()
    {
        if(State is not {} p)return;
        foreach(var q in WorkshopStories.All)
        {
            string id=Prefix+q.Id;
            if(p.Available(q.Id,Today))EnsureQuest(id,q.Title,q.Description,q.Id=="marlon"?ContentText.Format("copy.WorkshopService.3305945ba1",$"拼豆武器击败怪物：{p.Kills}/10；向马龙报告。"):q.Description);
            else Game1.player.removeQuest(id);
        }
        if(p.Commission is { } c && c.Week==Today/7)EnsureQuest(Prefix+"commission",c.Title,CommissionDescription(c),ContentText.Format("copy.WorkshopService.4fc545b498",$"向{NpcName(c.Npc)}交付：{TemplateName(c.Template)}；{DaysRemaining()}天后刷新。"));
        else Game1.player.removeQuest(Prefix+"commission");
    }
    private static void EnsureQuest(string id,string title,string description,string objective)
    {
        var quest=Game1.player.questLog.FirstOrDefault(q=>q.id.Value==id);
        if(quest is null){Game1.player.addQuest(id);quest=Game1.player.questLog.FirstOrDefault(q=>q.id.Value==id);}
        if(quest is null)return;
        quest.canBeCancelled.Value=false;
        // Base Quest uses these cached text fields; no custom serialized Quest subclass is introduced.
        AccessTools.Field(typeof(Quest),"_questTitle").SetValue(quest,title);
        AccessTools.Field(typeof(Quest),"_questDescription").SetValue(quest,description);
        AccessTools.Field(typeof(Quest),"_currentObjective").SetValue(quest,objective);
    }
    public void RecordKill(bool creative)
    {if(State is {} p){int before=p.Kills;p.RecordKill(Today,creative);if(before!=p.Kills)SyncJournal();}}
    private static bool Interact(NPC __instance,Farmer __0,GameLocation __1,ref bool __result)
    {
        var service=Current;
        if(service is null || service.nativeChat || Context.IsMultiplayer || !Context.IsWorldReady || !__0.IsLocalPlayer
            || Game1.activeClickableMenu is not null || Game1.eventUp || service.State is not {} p)return true;
        // Ordinary held gifts retain the native interaction without an extra menu.
        if(__0.ActiveObject is {} held && !ProductItems.IsProduct(held))return true;
        bool story=WorkshopStories.All.Any(q=>q.Npc==__instance.Name && p.Available(q.Id,Today));
        if(!story && !(p.Commission?.Npc==__instance.Name && p.Commission.Week==Today/7))return true;
        __result=true;service.ShowNpc(__instance);return false;
    }
    private void ShowNpc(NPC npc)
    {
        Game1.activeClickableMenu=new WorkshopMenu(NpcName(npc.Name)+ContentText.Get("copy.WorkshopService.2514a8011f"," · 拼豆工坊"),ContentText.Get("copy.WorkshopService.80d0a3e73e","可以聊聊拼豆，也可以照常交谈。交付前会再次确认具体物品。"),()=>
        {
            var list=new List<WorkshopChoice>();
            if(State is {} p)
            {
                foreach(var q in WorkshopStories.All.Where(q=>q.Npc==npc.Name && p.Available(q.Id,Today)))
                    list.Add(new(q.Title,()=>ShowDelivery(npc,q.Id)));
                if(p.Commission?.Npc==npc.Name && p.Commission.Week==Today/7)list.Add(new(p.Commission.Title,()=>ShowDelivery(npc,"commission")));
            }
            list.Add(new(ContentText.Get("copy.WorkshopService.a651f351bf","正常聊天"),()=>{Game1.activeClickableMenu=null;nativeChat=true;try{npc.checkAction(Game1.player,Game1.currentLocation);}finally{nativeChat=false;}}));
            return list;
        });
    }
    public void ShowPatterns(IClickableMenu? parent,Action<Blueprint>? copied=null)
    {
        bool ownedOnly=true;
        var designs=ReferencePatterns.All.ToDictionary(p=>p.Id,ReferencePatterns.Create);
        void Open()=>Game1.activeClickableMenu=new WorkshopMenu(ContentText.Get("copy.WorkshopService.ed3c1ec8a1","参考图纸"), ContentText.Format("copy.WorkshopService.4c88d2acef",$"已收录{ReferencePatterns.All.Count(p=>State?.Patterns.Contains(p.Id)==true)}/{ReferencePatterns.All.Count}张。每个季节首次完成委托可获得两张主题图纸。图纸可复制后自由修改；未获得的主题也可自行手绘。"),()=>
        {
            var state=State;var rows=new List<WorkshopChoice>{new(ownedOnly?ContentText.Get("copy.WorkshopService.a123239f96","筛选：已获得 → 查看全部"):ContentText.Get("copy.WorkshopService.c3719b7af3","筛选：全部 → 只看已获得"),()=>{ownedOnly=!ownedOnly;Open();})};
            foreach(var pattern in ReferencePatterns.All.Where(p=>!ownedOnly || state?.Patterns.Contains(p.Id)==true))
            {
                bool owned=state?.Patterns.Contains(pattern.Id)==true;var design=designs[pattern.Id];
                rows.Add(new(pattern.Name+(owned?ContentText.Get("copy.WorkshopService.40dadcadbc"," · 查看"):" · "+WorkshopGuidance.RewardName(pattern.Reward)),()=>ShowPatternDetail(pattern,owned,Open,copied,parent),true,design));
            }
            return rows;
        },parent);
        Open();
    }
    private void ShowPatternDetail(ReferencePattern pattern,bool owned,Action back,Action<Blueprint>? copied,IClickableMenu? parent)
    {
        var design=ReferencePatterns.Create(pattern);
        Game1.activeClickableMenu=new PatternDetailMenu(pattern,design,owned,back,()=>
        {
            if(save() is {} progress && progress.Workshop?.Patterns.Contains(pattern.Id)==true && new BlueprintRepository(progress).SaveAs(ReferencePatterns.Create(pattern),pattern.Name,out var saved))
            {copied?.Invoke(saved!);Game1.activeClickableMenu=parent;}
        });
    }
    private void ShowDelivery(NPC npc,string id)
    {
        if(State is not {} p)return;
        var story=WorkshopStories.All.FirstOrDefault(q=>q.Id==id);var commission=p.Commission;
        if(story is null && (commission is null || commission.Week!=Today/7))return;
        string title=story?.Title??commission!.Title,description=story?.Description??CommissionDescription(commission!);
        if(id=="marlon")description+=ContentText.Format("copy.WorkshopService.b8ac3ee042",$" 当前进度：{p.Kills}/10。");
        Game1.activeClickableMenu=new WorkshopMenu(title,description,()=>
        {
            var rows=new List<WorkshopChoice>();
            if(id is "refine" or "marlon")rows.Add(new(id=="refine"?ContentText.Get("copy.WorkshopService.dc708b2721","交付2铁锭与1煤炭"):ContentText.Get("copy.WorkshopService.1d10f02813","报告实战结果"),()=>PreviewDelivery(npc,id,-1)));
            else for(int i=0;i<Game1.player.Items.Count;i++)
            {
                int slot=i;var item=Game1.player.Items[i];var snapshot=products.Read(item);
                if(item?.Stack!=1 || snapshot is null || !p.CanDeliver(snapshot))continue;
                bool valid=id=="commission"?commission?.Matches(snapshot)==true:WorkshopStories.Matches(p,id,snapshot);
                if(valid)rows.Add(new(ContentText.Format("copy.WorkshopService.0b91b690cf",$"背包第{i+1}格 · {snapshot.Design.Name}"),()=>PreviewDelivery(npc,id,slot),Preview:snapshot.Design));
            }
            if(rows.Count==0)rows.Add(new(ContentText.Get("copy.WorkshopService.3b5924a525","背包中没有符合条件的作品"),()=>{},false));return rows;
        },back:()=>ShowNpc(npc));
    }
    private void PreviewDelivery(NPC npc,string id,int slot)
    {
        if(State is not {} p)return;
        Item? item=slot>=0 && slot<Game1.player.Items.Count?Game1.player.Items[slot]:null;
        string? raw=products.Read(item) is {} snapshot?DesignStorage.Serialize(snapshot):null;
        var story=WorkshopStories.All.FirstOrDefault(q=>q.Id==id);
        string name=story?.Title??p.Commission?.Title??ContentText.Get("copy.WorkshopService.9093b5730c","委托");
        string cost=id=="refine"?ContentText.Get("copy.WorkshopService.32f4515785","消耗2铁锭、1煤炭"):id=="marlon"?ContentText.Get("copy.WorkshopService.bac7cb9fe6","报告进度，不消耗物品"):id=="clint"?ContentText.Get("copy.WorkshopService.5dc766584b","检测后归还武器"):ContentText.Format("copy.WorkshopService.f6376fff0c",$"交付并消耗背包第{slot+1}格的作品");
        string frozen=DesignStorage.Serialize(p);int day=Today;
        Confirm(name,$"{cost}。"+WorkshopGuidance.RewardSummary(p,id,day),()=>
        {
            string result=Submit(npc,id,slot,item,raw,frozen,day);
            Game1.activeClickableMenu=new WorkshopMenu(name,result,()=>Array.Empty<WorkshopChoice>(),back:()=>ShowNpc(npc));
        },()=>ShowDelivery(npc,id));
    }
    private string Submit(NPC npc,string id,int slot,Item? selected,string? raw,string frozen,int day)
    {
        if(submitting || Context.IsMultiplayer || !Context.IsWorldReady || save() is not {} progress || progress.Workshop is not {} before
            || Today!=day || DesignStorage.Serialize(before)!=frozen)return ContentText.Get("copy.WorkshopService.f33809c1b2","状态已变化，请重新确认。");
        submitting=true;
        var items=Game1.player.Items;int money=Game1.player.Money;
        bool hadFriend=Game1.player.friendshipData.TryGetValue(npc.Name,out var friend);int oldFriend=friend?.Points??0;
        var original=new Dictionary<int,Item>();var originalCounts=new Dictionary<int,int>();var rawIdentity=new Dictionary<int,(string Id,int Quality)>();var replacement=new Dictionary<int,Item?>();bool applying=false;
        try
        {
            if(npc.currentLocation!=Game1.currentLocation || Microsoft.Xna.Framework.Vector2.Distance(npc.Position,Game1.player.Position)>192)return ContentText.Get("copy.WorkshopService.7bb02fafae","请靠近委托人后交付。");
            var story=WorkshopStories.All.FirstOrDefault(q=>q.Id==id);var commission=before.Commission;
            if((story?.Npc??commission?.Npc)!=npc.Name)return ContentText.Get("copy.WorkshopService.cc3abab942","委托人不匹配。");
            var snapshot=products.Read(selected);
            if(slot>=0 && (slot>=items.Count || !ReferenceEquals(items[slot],selected) || selected!.Stack!=1 || snapshot is null || DesignStorage.Serialize(snapshot)!=raw))return ContentText.Get("copy.WorkshopService.b7441e3e6e","作品已变化，请重新选择。");
            var after=JsonSerializer.Deserialize<WorkshopProgress>(frozen)!;
            if(id=="commission"?!after.FinishCommission(day,snapshot):!after.FinishStory(id,day,snapshot))return ContentText.Get("copy.WorkshopService.69cf0b7f7f","尚未满足条件，或奖励已领取。");
            bool firstCommission=id=="robin" && after.RefreshCommission(day,unchecked((long)Game1.uniqueIDForThisGame));
            if(id=="refine")
            {
                foreach(var need in new[]{("(O)335",2),("(O)382",1)})
                {
                    int remaining=need.Item2;
                    for(int i=0;i<items.Count && remaining>0;i++)if(items[i] is {} current && current.Stack>0 && current.QualifiedItemId==need.Item1 && InventoryService.IsPlainObject(current))
                    {
                        int stack=current.Stack;int take=Math.Min(remaining,stack);remaining-=take;original[i]=current;originalCounts[i]=stack;rawIdentity[i]=(current.QualifiedItemId,current.Quality);
                        if(take==stack)replacement[i]=null;
                        else {var rest=current.getOne();rest.Stack=stack-take;replacement[i]=rest;}
                    }
                    if(remaining>0)return ContentText.Get("copy.WorkshopService.bf9af65604","背包需要2铁锭和1煤炭；本次没有扣料。");
                }
            }
            else if(id=="commission" || story?.Consumed==true){if(slot<0 || selected is null)return ContentText.Get("copy.WorkshopService.4b4e096c66","缺少交付作品。");original[slot]=selected;originalCounts[slot]=1;replacement[slot]=null;}
            int gold=story?.Gold??commission!.Gold,friendship=story?.Friendship??75;
            if((long)money+gold>int.MaxValue)return ContentText.Get("copy.WorkshopService.1040839e38","金币已达到上限。");
            if(Today!=day || Game1.player.Money!=money || DesignStorage.Serialize(progress.Workshop)!=frozen
                || Game1.player.friendshipData.TryGetValue(npc.Name,out var currentFriend)!=hadFriend || hadFriend && (!ReferenceEquals(currentFriend,friend) || currentFriend!.Points!=oldFriend)
                || rawIdentity.Any(p=>original[p.Key].QualifiedItemId!=p.Value.Id || original[p.Key].Quality!=p.Value.Quality || !InventoryService.IsPlainObject(original[p.Key]))
                || original.Any(p=>!ReferenceEquals(items[p.Key],p.Value) || p.Value.Stack!=originalCounts[p.Key]) || selected is not null && DesignStorage.Serialize(products.Read(selected))!=raw)
                return ContentText.Get("copy.WorkshopService.3992eadbda","库存或任务状态已变化，本次没有扣料。");
            applying=true;
            foreach(var pair in replacement)items[pair.Key]=pair.Value!;
            Game1.player.Money=money+gold;
            if(friendship>0)Game1.player.changeFriendship(friendship,npc);
            progress.Workshop=after;
            applying=false;
            try{SyncJournal();if(firstCommission)Game1.addHUDMessage(new HUDMessage(ContentText.Get("copy.WorkshopService.bf83011efb","新的拼豆委托已写入任务日志。")));}
            catch(Exception ex){monitor.Log(ContentText.Get("copy.WorkshopService.48d77769e1","奖励已领取，任务日志将在下次加载恢复：")+ex.Message,LogLevel.Warn);}
            return ContentText.Get("copy.WorkshopService.1de9cef50a","交付完成！")+WorkshopGuidance.RewardSummary(before,id,day)+WorkshopGuidance.NextStep(id)+ContentText.Get("copy.WorkshopService.07bfc1a615","请正常保存游戏。");
        }
        catch(Exception ex)
        {
            if(applying)
            {
                foreach(var pair in original)items[pair.Key]=pair.Value;
                Game1.player.Money=money;progress.Workshop=before;
                if(hadFriend && friend is not null)friend.Points=oldFriend;else Game1.player.friendshipData.Remove(npc.Name);
            }
            monitor.Log(ContentText.Get("copy.WorkshopService.9a9d2f976f","拼豆委托提交失败：")+ex,LogLevel.Error);return ContentText.Get("copy.WorkshopService.f96e66d40b","交付失败，已恢复本次物品、金币和进度。");
        }
        finally{submitting=false;}
    }
    private static void Confirm(string title,string message,Action yes,Action no)=>Game1.activeClickableMenu=new WorkshopMenu(title,message,
        ()=>new[]{new WorkshopChoice(ContentText.Get("copy.WorkshopService.36f33adaf0","确认"),yes),new WorkshopChoice(ContentText.Get("copy.WorkshopService.2cd0f3be87","取消"),no)},back:no);
    private static int DaysRemaining()=>7-Today%7;
    private string CommissionDescription(WorkshopCommission c)
    {
        var hint=State is {} state?WorkshopGuidance.SuggestedPattern(state,c):null;
        string reference=hint is null?ContentText.Get("copy.WorkshopService.9b7840c523","暂无已获得的对应图纸，可直接在拼豆台手绘。"):
            ContentText.Format("copy.WorkshopService.b8930ff10f",$"已获得参考「{hint.Name}」：{WorkshopGuidance.BeadCost(ReferencePatterns.Create(hint))}颗")+
            (ClothingTemplates.IsClothing(ReferencePatterns.Template(hint.TemplateId)!.Use)?ContentText.Get("copy.WorkshopService.f0394d1d2f","羊毛豆。"):ContentText.Get("copy.WorkshopService.cf14945c04","普通豆。"));
        return ContentText.Format("copy.WorkshopService.f3845e99a7",$"{NpcName(c.Npc)}需要1件{TemplateName(c.Template)}，每个视图至少{c.Minimum}颗豆。")+
            ContentText.Format("copy.WorkshopService.4ddaafff3d",$"奖励{c.Gold}金和75友好度；{DaysRemaining()}天后刷新。")+WorkshopGuidance.CostHint(c)+
            ContentText.Get("copy.WorkshopService.f739df57b1","报酬固定，不随尺寸、豆数或昂贵材料增加。")+reference+WorkshopGuidance.Purpose(c);
    }
    public static string NpcName(string id)=>id switch{"Robin"=>ContentText.Get("copy.WorkshopService.cac14c7743","罗宾"),"Clint"=>ContentText.Get("copy.WorkshopService.b15b0623b2","克林特"),"Emily"=>ContentText.Get("copy.WorkshopService.ddd49ff6f8","艾米丽"),"Gus"=>ContentText.Get("copy.WorkshopService.3f3584edcb","格斯"),"Marlon"=>ContentText.Get("copy.WorkshopService.38468be9cf","马龙"),_=>id};
    private static string TemplateName(string id)=>id==ProductTemplates.Hat?ContentText.Get("copy.WorkshopService.82489aee02","拼豆帽"):id==ClothingTemplates.Shirt?ContentText.Get("copy.WorkshopService.f0ed5fbca3","上衣"):id==ClothingTemplates.Pants?ContentText.Get("copy.WorkshopService.55511e2b93","长裤"):id==DefaultManufacturing.Chair?ContentText.Get("copy.WorkshopService.e57f49ca9c","椅子"):id==DefaultManufacturing.Table?ContentText.Get("copy.WorkshopService.306de54769","桌子"):id==DefaultManufacturing.StoneStatue?ContentText.Get("copy.WorkshopService.24f7873c62","雕像"):ProductTemplates.IsPicture(id)?ContentText.Get("copy.WorkshopService.e457813606","拼豆画"):ContentText.Get("copy.WorkshopService.cb08044c2d","摆件");
}
