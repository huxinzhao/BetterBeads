using BetterBeads.Data;
using BetterBeads.Runtime;

internal static class NoAppearanceChecks
{
    internal static void Run()
    {
        int passed=0;
        void Check(bool valid,string message)
        {
            if(!valid)throw new Exception(message);
            passed++;Console.WriteLine("PASS "+message);
        }
        var assembly=typeof(ProductItems).Assembly;
        foreach(string name in new[]{"AppearanceService","AppearanceMenu","AppearanceConfirmMenu","AppearanceContent","AppearanceDrops","AppearanceDrawing","AppearanceToolUpgrade"})
            Check(assembly.GetType("BetterBeads.Runtime."+name) is null,name+" is not compiled into the installation");
        foreach(string name in new[]{"AppearanceWorld","ToolAppearance","BeadAppearance","AppearanceDropSettings","AppearanceLayout"})
            Check(assembly.GetType("BetterBeads.Data."+name) is null,name+" is absent from the runtime assembly");
        Check(assembly.GetType("BetterBeads.Runtime.WeaponAppearancePatches") is not null,"existing weapon forge implementation remains available");
        Check(OnlineRules.Protocol=="1.1.0-RC6-noappearance1","distinct multiplayer protocol prevents mixing with the appearance candidate");
        string directory=Path.GetDirectoryName(assembly.Location)!;
        foreach(string file in new[]{"AppearanceBench.png","AppearanceStone.png","appearance-drops.json"})
            Check(!File.Exists(Path.Combine(directory,"assets",file)),"unused "+file+" is not copied to the build");
        foreach(string locale in new[]{"default","zh"})
        {
            var text=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(directory,"i18n",locale+".json")))!;
            Check(!text.Keys.Any(k=>k.StartsWith("appearance.",StringComparison.Ordinal)),locale+" appearance copy was removed");
            Check(text.ContainsKey("simple.recovery-copy"),locale+" recovery copy feedback remains translated");
        }
        Console.WriteLine($"PASS {passed} no-appearance variant checks; no game launched.");
    }
}
