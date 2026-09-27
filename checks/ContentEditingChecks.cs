using BetterBeads.Data;
using System.Text.Json;

internal static class ContentEditingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/default.json"))!;
        var chinese=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/zh.json"))!;
        check(catalog.Keys.ToHashSet().SetEquals(chinese.Keys),"中性与中文文案键完整对应");
        ContentText.Resolve=key=>catalog.GetValueOrDefault(key);
        check(ContentText.Get("missing","回退") == "回退","缺少文本键回退源码，不显示内部ID");
        ContentText.Resolve=_=>"{1} / {0:00}";
        check(ContentText.Format("test",$"原文{7}、{"材料"}")=="材料 / 07","编辑文案保留数字格式且可调整占位符顺序");
        int warnings=0;ContentText.InvalidFormat=_=>warnings++;ContentText.Resolve=_=>"{999}";
        check(ContentText.Format("invalid",$"原文{7}")=="原文7" && ContentText.Format("invalid",$"原文{8}")=="原文8" && warnings==1,"错误占位符回退，警告仅一次");
        ContentText.Resolve=key=>catalog.GetValueOrDefault(key);ContentText.InvalidFormat=null;
        WorkshopPolishChecks.Run(check);
        var reference=ReferencePatterns.All.Single(p=>p.Id=="starter.sprout");
        var original=ReferencePatterns.Create(reference);var before=DesignStorage.Serialize(original);
        var pixels=Enumerable.Repeat(0xFF3344FFu,32*32).ToArray();
        check(ReferencePatternOverrides.TrySet(reference.Id,new Dictionary<string,PatternPixels>{{"front",new(32,32,pixels)}}),"支持原尺寸PNG像素输入");
        pixels[0]=0;
        var customized=ReferencePatterns.Create(reference);
        check(customized.Views["front"].Cells[0]?.Rgba==0xFF3344FF && customized.TemplateId==original.TemplateId && before==DesignStorage.Serialize(original),"新参考使用定制像素，输入数组与旧图纸均隔离");
        var exported=ReferencePatternOverrides.Get(reference.Id,"front")!;exported.Pixels[0]=0;
        check(ReferencePatternOverrides.Get(reference.Id,"front")!.Pixels[0]==0xFF3344FF,"读取像素返回副本，不能改写目录缓存");
        check(!ReferencePatternOverrides.TrySet(reference.Id,new Dictionary<string,PatternPixels>{{"front",new(16,16,new uint[256])}}),"错误尺寸拒绝，不拉伸图案");
        var partial=new uint[1024];partial[0]=0xFF334480;
        check(!ReferencePatternOverrides.TrySet(reference.Id,new Dictionary<string,PatternPixels>{{"front",new(32,32,partial)}}),"半透明像素拒绝，避免预乘造成豆色错误");
        check(!ReferencePatternOverrides.TrySet(reference.Id,new Dictionary<string,PatternPixels>{{"front",new(32,32,new uint[1024])}}),"全透明图纸拒绝");
        check(!ReferencePatternOverrides.TrySet("emily.hat",new Dictionary<string,PatternPixels>{{"front",new(20,20,new uint[400])}}),"缺少帽子方向拒绝整套替换");
        check(!ReferencePatternOverrides.TrySet("unknown",new Dictionary<string,PatternPixels>()),"不通过美术接口新增或改变图纸ID");
        ReferencePatternOverrides.Clear();
        check(ReferencePatterns.Create(reference).Views["front"].Cells.Select(c=>c?.Rgba).SequenceEqual(original.Views["front"].Cells.Select(c=>c?.Rgba)),"清空定制后恢复内置参考图，原图不受影响");
        ContentText.Resolve=null;
    }
}
