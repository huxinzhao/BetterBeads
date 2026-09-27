using BetterBeads.Data;
using System.Text.Json;

internal static class UnlockChecks
{
    internal static void Run(Action<bool,string> check)
    {
        string recipe="xinzh.BetterBeads.Workbench";
        check(WorkbenchUnlock.ShopCondition(recipe)=="PLAYER_BASE_FORAGING_LEVEL Current 2, !PLAYER_HAS_CRAFTING_RECIPE Current "+recipe,
            "Robin shop requires permanent foraging level two and unknown recipe");
        check(!WorkbenchUnlock.ShouldSendMail(true,false,false,false,false,false),"no letter below level two");
        check(WorkbenchUnlock.ShouldSendMail(true,true,false,false,false,false),"letter queued on unlock");
        check(!WorkbenchUnlock.ShouldSendMail(true,true,true,false,false,false),"previously learned recipe keeps its progress");
        check(!WorkbenchUnlock.ShouldSendMail(true,true,false,true,false,false),"read letter does not repeat");
        check(!WorkbenchUnlock.ShouldSendMail(true,true,false,false,true,false),"mailbox letter does not repeat");
        check(!WorkbenchUnlock.ShouldSendMail(true,true,false,false,false,true),"tomorrow letter does not repeat");
        check(!WorkbenchUnlock.ShouldSendMail(false,true,false,false,false,false),"invalid recipe configuration sends no misleading letter");
        var zh=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/zh.json"))!;
        var en=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/default.json"))!;
        check(zh["bench.unlock-mail"].StartsWith("你好，农场主！^^嘿，如果你喜欢玩拼豆的话......")
            &&zh["bench.unlock-mail"].EndsWith("^^——罗宾")
            &&en["bench.unlock-mail"].StartsWith("Hello, farmer!^^")
            &&en["bench.unlock-mail"].EndsWith("^^—Robin")
            &&en.ContainsKey("bench.unlock-mail-title"),"Robin letter identifies its sender in both languages");
    }
}
