#if BEADS_LITE
using BetterBeads.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed class CollectorMailContent
{
    private const string Prefix="xinzh.BetterBeads.Collector.";
    private readonly IModHelper helper;
    private readonly Func<SaveProgress?> progress;
    public CollectorMailContent(IModHelper helper,Func<SaveProgress?> progress)
    {
        this.helper=helper;this.progress=progress;
        helper.Events.Content.AssetRequested+=OnAssetRequested;
    }
    public static string MailId(int day)=>Prefix+(Game1.player.IsMainPlayer?"":Game1.player.UniqueMultiplayerID+".")+day;
    public void DayStarted()
    {
        if(!Context.IsWorldReady||progress() is not {} data)return;
        helper.GameContent.InvalidateCache("Data/Mail");
        foreach(var letter in CollectorLedger.Due(data,Game1.Date.TotalDays))
        {
            string id=MailId(letter.Day);
            if(!Game1.player.mailReceived.Contains(id)&&!Game1.player.mailbox.Contains(id)&&!Game1.player.mailForTomorrow.Contains(id))
                Game1.player.mailbox.Add(id);
        }
    }
    private void OnAssetRequested(object? sender,AssetRequestedEventArgs e)
    {
        if(!e.NameWithoutLocale.IsEquivalentTo("Data/Mail")||progress() is not {} data)return;
        e.Edit(asset=>
        {
            var mail=asset.AsDictionary<string,string>().Data;
            foreach(var letter in data.CollectorLetters.Where(l=>l.Works.Count>0))
                mail[MailId(letter.Day)]=Body(letter);
        });
    }
    private static string Body(CollectorLetter letter)
    {
        string intro=ContentText.Get("market.mail-intro","有人悄悄提到了你的拼豆作品：");
        string ending=ContentText.Get("market.mail-ending","它们的收藏估价已经记在作品上。期待你的下一件创作。^^——神秘艺术收藏家");
        string title=ContentText.Get("market.mail-title","一位神秘收藏家的来信");
        var lines=letter.Works.Select(w=>ContentText.Format("market.mail-work",$"「{Safe(w.Name)}」——{w.Price}金"));
        return intro+"^^"+string.Join("^",lines)+"^^"+ending+"[#]"+title;
    }
    private static string Safe(string name)=>name.Replace('^','＾').Replace('#','＃').Replace('\r',' ').Replace('\n',' ');
}
#endif
