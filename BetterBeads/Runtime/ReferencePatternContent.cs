using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace BetterBeads.Runtime;

internal static class ReferencePatternContent
{
    private const string Prefix="Mods/xinzh.BetterBeads/Patterns/";
    public static void Register(IModHelper helper,IMonitor monitor)
    {
        helper.Events.Content.AssetRequested+=(_,e)=>
        {
            foreach(var pattern in ReferencePatterns.All)
            foreach(var view in ReferencePatterns.Template(pattern.TemplateId)!.Views)
            {
                string file=ReferencePatternOverrides.FileName(pattern.Id,view);
                if(e.NameWithoutLocale.IsEquivalentTo(Prefix+file[..^4]))
                    e.LoadFromModFile<Texture2D>("assets/patterns/"+file,AssetLoadPriority.Low);
            }
        };
        void Reload(bool invalidate)
        {
            ReferencePatternOverrides.Clear();int loaded=0;
            foreach(var pattern in ReferencePatterns.All)
            {
                try
                {
                    var views=new Dictionary<string,PatternPixels>();
                    foreach(var view in ReferencePatterns.Template(pattern.TemplateId)!.Views)
                    {
                        string asset=Prefix+ReferencePatternOverrides.FileName(pattern.Id,view)[..^4];
                        if(invalidate)helper.GameContent.InvalidateCache(asset);
                        var texture=helper.GameContent.Load<Texture2D>(asset);
                        var spec=ReferencePatterns.Template(pattern.TemplateId)!;
                        if(texture.Width!=spec.Width || texture.Height!=spec.Height)throw new InvalidDataException("Native dimensions required.");
                        var colors=new Color[texture.Width*texture.Height];texture.GetData(colors);
                        views[view]=new(texture.Width,texture.Height,colors.Select(c=>((uint)c.R<<24)|((uint)c.G<<16)|((uint)c.B<<8)|c.A).ToArray());
                    }
                    if(!ReferencePatternOverrides.TrySet(pattern.Id,views))throw new InvalidDataException("All directions must be nonempty, with alpha 0 or 255.");
                    loaded++;
                }
                catch(Exception ex){monitor.Log($"Pattern {pattern.Id}: using built-in art; custom files kept. {ex.Message}",LogLevel.Warn);}
            }
            monitor.Log($"Loaded editable reference artwork: {loaded}/{ReferencePatterns.All.Count}. Existing designs are unchanged.",LogLevel.Info);
        }
        helper.Events.GameLoop.GameLaunched+=(_,_)=>Reload(false);
        helper.ConsoleCommands.Add("betterbeads_reload_patterns","Reload editable reference PNGs for newly opened references; existing designs are unchanged.",(_,_)=>Reload(true));
    }
}
