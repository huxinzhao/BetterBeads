namespace BetterBeads.Data;

public sealed record ReferencePattern(string Id,string Name,string Reward,string TemplateId,string Source);
public static class ReferencePatterns
{
    private static string Sword=>"xinzh.BetterBeads.Sword16";
    private static string Dagger=>"xinzh.BetterBeads.Dagger16";
    private static string Hammer=>"xinzh.BetterBeads.Hammer16";
    public static IReadOnlyList<ReferencePattern> All {get;}=new ReferencePattern[]
    {
        new("starter.sprout",ContentText.Get("copy.ReferencePatterns.8124f7e843","土豆苗小花园"),"starter",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.d52502a3d1","josehzz · CC0 改编")),
        new("starter.heart",ContentText.Get("copy.ReferencePatterns.d754e8556f","草莓暖心木牌"),"starter",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.d52502a3d1","josehzz · CC0 改编")),
        new("robin.house",ContentText.Get("copy.ReferencePatterns.5050ca5e35","林中木屋"),"robin",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("robin.chair",ContentText.Get("copy.ReferencePatterns.25b401d17e","花园木椅"),"robin",DefaultManufacturing.Chair,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("emily.hat",ContentText.Get("copy.ReferencePatterns.ed1c8e3992","向日葵草帽"),"emily",ProductTemplates.Hat,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("emily.shirt",ContentText.Get("copy.ReferencePatterns.8143a29679","叶纹围裙"),"emily",ClothingTemplates.Shirt,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("clint.dagger",ContentText.Get("copy.ReferencePatterns.0305d857d8","铜叶短刃"),"clint",Dagger,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("clint.sword",ContentText.Get("copy.ReferencePatterns.51a2e36e01","绯红护手剑"),"clint",Sword,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("refine.hammer",ContentText.Get("copy.ReferencePatterns.f57cd083e5","晶簇重锤"),"refine",Hammer,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("refine.sign",ContentText.Get("copy.ReferencePatterns.e745b040dd","炼炉木牌"),"refine",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("marlon.crest",ContentText.Get("copy.ReferencePatterns.49c131fbe8","冒险家徽记"),"marlon",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("marlon.sword",ContentText.Get("copy.ReferencePatterns.b6aded70ba","星光守卫剑"),"marlon",Sword,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("spring.flower",ContentText.Get("copy.ReferencePatterns.f77f40736b","春日花束"),"season.0",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.d52502a3d1","josehzz · CC0 改编")),
        new("spring.pot",ContentText.Get("copy.ReferencePatterns.4e0243d194","新芽盆栽"),"season.0",DefaultManufacturing.WoodOrnament,ContentText.Get("copy.ReferencePatterns.d52502a3d1","josehzz · CC0 改编")),
        new("summer.shell",ContentText.Get("copy.ReferencePatterns.6cc00ad125","潮汐贝壳"),"season.1",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("summer.table",ContentText.Get("copy.ReferencePatterns.f4339c659f","海风茶桌"),"season.1",DefaultManufacturing.Table,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("autumn.pumpkin",ContentText.Get("copy.ReferencePatterns.0e4568fae5","丰收南瓜"),"season.2",DefaultManufacturing.WoodOrnament,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("autumn.pants",ContentText.Get("copy.ReferencePatterns.45a4a7f7cf","秋叶补丁裤"),"season.2",ClothingTemplates.Pants,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("winter.star",ContentText.Get("copy.ReferencePatterns.364e8a3a60","冬夜星灯"),"season.3",ProductTemplates.DetailedPicture,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘")),
        new("winter.snow",ContentText.Get("copy.ReferencePatterns.b45675acdd","围巾雪人"),"season.3",DefaultManufacturing.StoneStatue,ContentText.Get("copy.ReferencePatterns.5c77248021","BetterBeads · 原创重绘"))
    };
    public static TemplateSpec? Template(string id)=>DefaultManufacturing.Recipes().Concat(DefaultWeapons.Recipes()).Concat(ClothingTemplates.Recipes()).FirstOrDefault(r=>r.Template.Id==id)?.Template;
    public static Blueprint Create(ReferencePattern pattern)
    {
        var template=Template(pattern.TemplateId)??throw new ArgumentException("Unknown pattern template");
        var design=new Blueprint{Name=pattern.Name,TemplateId=template.Id,Use=template.Use};
        foreach(var view in template.Views)
        {
            var sprite=ReferencePatternArt.Get(pattern.Id,view);
            if(sprite.Width!=template.Width || sprite.Height!=template.Height)throw new InvalidOperationException("Pattern size differs from template.");
            var bytes=Convert.FromBase64String(sprite.Cells);
            var custom=ReferencePatternOverrides.Get(pattern.Id,view);
            var grid=new BeadGrid{Width=sprite.Width,Height=sprite.Height};
            for(int i=0;i<bytes.Length;i++)
            {
                uint rgba=custom is null?sprite.Palette[bytes[i]]:custom.Pixels[i];
                if((rgba&255)==0){grid.Cells.Add(null);continue;}
                grid.Cells.Add(new BeadCell{ColorId=BeadPalette.Id(rgba),Rgba=rgba,
                    MaterialId=WeaponMaterials.IsWeapon(template.Use)?pattern.Id=="clint.dagger"?"copper":"iron":
                        ClothingTemplates.IsClothing(template.Use)?null:"decoration"});
            }
            design.Views[view]=grid;
        }
        if(WeaponMaterials.IsWeapon(design.Use))
        {
            int count=design.Views["front"].Cells.Count(c=>c is not null);
            if(count<template.MinimumMaterials)design.SupplementaryMaterials[pattern.Id=="clint.dagger"?"copper":"iron"]=template.MinimumMaterials-count;
        }
        return design;
    }
}
