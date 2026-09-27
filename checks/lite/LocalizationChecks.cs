using BetterBeads.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class LocalizationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var en=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/default.json"))!;
        var zh=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/zh.json"))!;
        check(en.Count>=737&&en.Keys.ToHashSet().SetEquals(zh.Keys),"English and Chinese cover the same translation keys");
        check(en.Values.All(v=>!Regex.IsMatch(v,@"\p{IsCJKUnifiedIdeographs}")),"English fallback contains no Chinese text");
        check(en.Values.All(v=>!string.IsNullOrWhiteSpace(v)),"English entries are nonempty");
        string[] Tokens(string value)=>Regex.Matches(value,@"\{\{[^{}]+\}\}|(?<!\{)\{\d+(?:,[^{}:]+)?(?::[^{}]+)?\}(?!\})").Select(m=>m.Value).OrderBy(s=>s).ToArray();
        check(en.All(p=>Tokens(p.Value).SequenceEqual(Tokens(zh[p.Key]))),"All named and formatted numeric placeholders match");
        check(en.All(p=>new[]{"^^","[#]"}.All(m=>p.Value.Split(m).Length==zh[p.Key].Split(m).Length)),"Native mail markers are preserved");
        check(en.Where(p=>zh[p.Key].StartsWith("Basic/")).All(p=>p.Value.Split('/').Length==zh[p.Key].Split('/').Length&&p.Value.StartsWith("Basic/")&&p.Value.Split('/').Skip(4).SequenceEqual(zh[p.Key].Split('/').Skip(4))),"Native quest control fields are preserved");
        var old=ContentText.Resolve;
        try
        {
            ContentText.Resolve=k=>en.GetValueOrDefault(k);
            check(ContentText.Format("simple.cost-summary",$"*预计消耗{"Wood"}{45}个，当前可用{120}个。") == "Cost: Wood × 45; available: 120.","English cost sentence keeps correct spacing and order");
            check(ContentText.Format("simple.size-label",$"选择尺寸：{32}*{32}px")=="Size: 32×32px","English canvas dimensions format correctly");
            check(ContentText.Format("simple.category-label",$"选择品类：{"Bead sword"}")=="Type: Bead sword","English category includes selected product");
            check(ContentText.Format("simple.missing-raw",$"还缺 {2} 个{"Iron Bar"}")=="Need 2 more Iron Bar.","Missing-material amount is preserved");
            check(ContentText.Format("simple.received-native",$"你制作了「{"My Chicken"}」！")=="You made My Chicken!","Native received dialogue uses English without changing the name");
            check(ContentText.Get("simple.template.white-chicken","白色鸡")=="White Chicken","Native template display names translate");
            check(ContentText.Get("missing.key","fallback")=="fallback","Missing keys keep the explicit fallback");
            var design=SimpleCrafting.Blank(SimpleCrafting.Picture16);design.Name="我的小鸡";
            string snapshot=DesignStorage.Serialize(design);
            ContentText.Resolve=k=>zh.GetValueOrDefault(k)??en.GetValueOrDefault(k);
            check(ContentText.Format("simple.cost-summary",$"cost {"木材"} {45} {120}")=="*预计消耗木材45个，当前可用120个。","Chinese cost sentence stays unchanged");
            check(ContentText.Get("simple.template.white-chicken","unknown")=="白色鸡","Chinese resolves before English fallback");
            check(snapshot==DesignStorage.Serialize(design),"Changing display language does not modify saved design data or names");
            check(zh["copy.ArtResources.16e7a38c73"]=="国Ag"&&en["copy.ArtResources.16e7a38c73"]=="Ag","Font measurement sample matches each language");
        }
        finally{ContentText.Resolve=old;}
        var literalKeys=new List<string>();
        foreach(var file in Directory.EnumerateFiles("BetterBeads","*.cs",SearchOption.AllDirectories).Where(p=>!p.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)))
        {
            string code=File.ReadAllText(file);
            literalKeys.AddRange(Regex.Matches(code,"ContentText\\.(?:Get|Format)\\(\"([^\"]+)\"\\s*,").Select(m=>m.Groups[1].Value));
        }
        check(literalKeys.All(en.ContainsKey),"Every literal ContentText key has an English entry");
        foreach(var (file,prefix) in new[]{("SimpleEditorPanel.cs","simple."),("SimpleLibraryPanel.cs","simple."),("CreativePalettePanel.cs","simple."),("ScenePreviewPanel.cs","simple."),("ImageImportPanel.cs","simple.import-")})
        {
            string code=File.ReadAllText("BetterBeads/Runtime/"+file);
            var keys=Regex.Matches(code,"\\bT\\(\"([^\"]+)\"\\s*,").Select(m=>prefix+m.Groups[1].Value);
            check(keys.All(en.ContainsKey),file+" has translations for all fixed UI labels");
        }
        check(!File.ReadAllText("BetterBeads/Runtime/WorkbenchMenu.cs").Contains("MeasureString(\"国Ag\")"),"English workbench sizing does not use a hard-coded Chinese probe");
        check(en["simple.metal-short.copper"]=="Cu"&&en["simple.metal-short.iron"]=="Fe"&&en["simple.metal-short.iridium"]=="Ir","Compact metal labels retain distinct identities");
    }
}
