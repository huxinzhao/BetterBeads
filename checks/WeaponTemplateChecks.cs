using BetterBeads.Data;

internal static class WeaponTemplateChecks
{
    public static void Run(Action<bool,string> check)
    {
        var templates=new[]{ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer}.Select(use=>new WeaponTemplateDefinition(
            "check.weapon."+use,use,16,16,"check.name","check.description","Mods/check/"+use)).ToArray();
        try
        {
            WeaponTemplates.Configure(templates);
            check(templates.Select(t=>t.NativeType).SequenceEqual(new[]{3,1,2}),"剑、匕首、锤映射各自原生类型，不混用动作");
            foreach(var template in templates)
            {
                var design=new Blueprint{Use=template.Use,TemplateId=template.Id,Views=new(){["front"]=new(){Width=16,Height=16,
                    Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}}};
                design.Views["front"].Cells[255]=new(){ColorId="white",Rgba=0xFFFFFFFF,MaterialId="copper"};
                var product=new ProductSnapshot{Design=design,WeaponRulesVersion="test.rules",
                    FinalStats=new WeaponStatValues(3,5,1,0.5f,0.02f,3).ToSnapshot()};
                check(ProductTemplates.Matches(product) && ProductTemplates.QualifiedId(product)=="(W)"+template.Id
                    && DesignStorage.TryReadSnapshot(DesignStorage.Serialize(product),out var loaded) && ProductTemplates.Matches(loaded!),
                    $"{template.Use} 快照往返对应独立武器身份、视图及完整最终属性");
                var atlas=ProductTemplates.CreateAtlas(product);atlas.Cells[255]!.Rgba=0;
                check(product.Design.Views["front"].Cells[255]!.Rgba==0xFFFFFFFF && atlas.Width==16 && atlas.Height==16,
                    $"{template.Use} 单格武器图集保留边角，纹理数据不共享图纸引用");
                product.Design.Use=ProductUse.Picture;
                check(!ProductTemplates.Matches(product),$"{template.Use} 不能冒用同ID的家具用途");
            }
            var sword=templates[0];
            var invalidSize=sword with{Id="check.large",PixelHeight=32};
            bool rejected=false;
            try{WeaponTemplates.Configure(new[]{invalidSize});}catch(ArgumentException){rejected=true;}
            check(rejected && WeaponTemplates.All.Count==3,"原生武器路径拒绝未适配的大画布，注册失败保持已有模板");
            rejected=false;
            try{WeaponTemplates.Configure(new[]{sword,sword with{Id="check.duplicate"}});}catch(ArgumentException){rejected=true;}
            check(rejected && WeaponTemplates.All.Count==3,"不同武器不得共用同一个占位资源路径");
            rejected=false;
            try{WeaponTemplates.Configure(new[]{sword with{Id=ProductTemplates.Picture}});}catch(ArgumentException){rejected=true;}
            check(rejected,"武器模板不覆盖既有家具身份");
            var snapshot=new ProductSnapshot{Design=new(){Use=sword.Use,TemplateId=sword.Id,Views=new(){["front"]=new(){Width=16,Height=16,
                Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}}}};
            check(!ProductTemplates.Matches(snapshot),"没有最终属性和武器规则版本的快照不被识别为可用武器");
            snapshot.FinalStats=new WeaponStatValues(1,2,0,0,0,1).ToSnapshot();
            check(!ProductTemplates.Matches(snapshot),"仅补齐数值仍不能绕过武器规则版本要求");
        }
        finally{WeaponTemplates.Configure(Array.Empty<WeaponTemplateDefinition>());}
        check(WeaponTemplates.All.Count==0,"离线样例清理后没有启用待确认的正式武器模板");
    }
}
