using System.Text.Json;
using BetterBeads.Data;

internal static class ArtChecks
{
    public static void Run(Action<bool,string> check)
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"BetterBeads","assets","art.json")))dir=dir.Parent;
        var art=JsonSerializer.Deserialize<ArtDefinition>(File.ReadAllText(Path.Combine(dir!.FullName,"BetterBeads","assets","art.json")))!;
        var catalog=DefaultProcessing.Create();
        check(art.IsValid() && art.ShowBeadHoles && new ArtDefinition().ShowBeadHoles && art.UseNativeUi && new ArtDefinition().UseNativeUi,"正式美术清单与配色可读取，默认启用圆环豆粒与原生菜单皮肤");
        check(catalog.Materials.All(m=>art.Beads.ContainsKey(m.Id)),"全部27种普通及精炼材料都有按ID绑定的图标");
        check(catalog.Materials.Select(m=>art.BeadIndex(m.Id,128,64)).Distinct().Count()==catalog.Materials.Count,"不同材料不误用同一图块");
        var oldIndex=art.BeadIndex("ruby",128,64);catalog.Materials.Reverse();
        check(art.BeadIndex(catalog.Materials.Single(m=>m.Id=="ruby").Id,128,64)==oldIndex,"改变材料顺序不改变材质对应的图案");
        check(art.BeadIndex("custom-new-material",128,64)==art.Beads["unknown"],"作者新增材料时使用独立的未知材质图标");
        art.Beads["ruby"]=999;
        check(art.BeadIndex("ruby",128,64)==art.Beads["unknown"],"越界材质映射使用安全回退");
        check(art.BeadIndex("ruby",1,1)==-1,"损坏或过小图集不会读取越界像素");
        check(!ArtDefinition.Fits(int.MaxValue,int.MaxValue,16,128,64),"图集索引计算使用宽整数避免溢出");
        check(!ArtDefinition.Fits(0,0,16,128,64) && !ArtDefinition.Fits(-1,8,16,128,64),"拒绝零列数与负图块位置");
        art.Ink="#white";check(!art.IsValid(),"无效文字颜色不会进入绘制");art.Ink="392D32";
        art.Schema=99;check(!art.IsValid(),"未知美术接口版本回退");art.Schema=1;
        art.BeadColumns=0;check(!art.IsValid(),"零列数清单被拒绝");art.BeadColumns=8;
        art.Icons=null!;check(!art.IsValid(),"空图标映射不会导致空引用绘制");
    }
}
