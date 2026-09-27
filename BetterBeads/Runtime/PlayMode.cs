using BetterBeads.Data;
using StardewModdingAPI;

namespace BetterBeads.Runtime;

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod,Action reset,Action save,bool titleScreenOnly=false);
    void AddBoolOption(IManifest mod,Func<bool> getValue,Action<bool> setValue,Func<string> name,Func<string>? tooltip=null,string? fieldId=null);
}
internal static class PlayMode
{
    private static WorkbenchSettings settings=new();
    public static bool? RemoteCreative {get;set;}
    public static bool Creative=>Context.IsMultiplayer&&!Context.IsMainPlayer?RemoteCreative??false:settings.CreativeMode;
    public static WorkbenchSettings Feedback=>settings;
    private static IModHelper? owner;
    public static bool AllowValuableSources=>settings.AllowValuableOrdinarySources;
    public static void ToggleValuableSources(){settings.AllowValuableOrdinarySources=!settings.AllowValuableOrdinarySources;owner?.WriteConfig(settings);}
    public static IReadOnlySet<string> Unlocked(ProcessingCatalog catalog,SaveProgress progress)
        =>catalog.Colors.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
    public static void Register(IModHelper helper,IManifest manifest,WorkbenchSettings config)
    {
        settings=config;owner=helper;
        helper.Events.GameLoop.GameLaunched+=(_,_)=>
        {
            var api=helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if(api is null)return;
            api.Register(manifest,()=>{settings.CreativeMode=false;settings.AllowValuableOrdinarySources=false;settings.ReducedMotion=false;settings.HoverSounds=settings.ClickSounds=settings.BeadSounds=settings.SuccessSounds=true;},()=>helper.WriteConfig(settings),titleScreenOnly:false);
            api.AddBoolOption(manifest,()=>settings.ReducedMotion,v=>settings.ReducedMotion=v,()=>ContentText.Get("feedback.reduced","减少动效"),fieldId:"ReducedMotion");
            api.AddBoolOption(manifest,()=>settings.HoverSounds,v=>settings.HoverSounds=v,()=>ContentText.Get("feedback.hover","按钮悬停音效"),fieldId:"HoverSounds");
            api.AddBoolOption(manifest,()=>settings.ClickSounds,v=>settings.ClickSounds=v,()=>ContentText.Get("feedback.click","按钮点击音效"),fieldId:"ClickSounds");
            api.AddBoolOption(manifest,()=>settings.BeadSounds,v=>settings.BeadSounds=v,()=>ContentText.Get("feedback.bead","摆豆音效"),fieldId:"BeadSounds");
            api.AddBoolOption(manifest,()=>settings.SuccessSounds,v=>settings.SuccessSounds=v,()=>ContentText.Get("feedback.success","制作成功音效"),fieldId:"SuccessSounds");
#if !BEADS_LITE
            api.AddBoolOption(manifest,()=>settings.AllowValuableOrdinarySources,value=>settings.AllowValuableOrdinarySources=value,
                ()=>ContentText.Get("copy.PlayMode.c892fb6efd","允许普通豆消耗花卉与硬木"),()=>ContentText.Get("copy.PlayMode.bff4e4ef9a","默认关闭。开启后仍会在熨烫确认页列出实际消耗。"),"AllowValuableOrdinarySources");
#endif
            api.AddBoolOption(manifest,()=>settings.CreativeMode,value=>settings.CreativeMode=value,
                ()=>helper.Translation.Get("creative.option").ToString(),()=>helper.Translation.Get("creative.tooltip").ToString(),"CreativeMode");
        };
    }
}
