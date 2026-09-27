using BetterBeads.Data;

internal static class FurnitureChecks
{
    public static void Run(Action<bool,string> check)
    {
        // Synthetic registry entries test support for dimensions, not author-approved production templates.
        var definition=new FurnitureTemplateDefinition("check.tall",ProductUse.Statue,16,32,1,1,"check.name","Mods/check/tall");
        try
        {
            FurnitureTemplates.Configure(new[]{definition});
            var fields=definition.FurnitureData("Test statue").Split('/');
            check(fields[2]=="1 2" && fields[3]=="1 1" && fields[5]=="0" && fields[8]=="0",
                "家具元数据分别保存绘制尺寸、占地、零售价与独立图集索引");
            var snapshot=new ProductSnapshot{Design=new(){Use=ProductUse.Statue,TemplateId=definition.Id,
                Views=new(){["front"]=new(){Width=16,Height=32,Cells=Enumerable.Repeat<BeadCell?>(null,512).ToList()}}}};
            snapshot.Design.Views["front"].Cells[511]=new(){ColorId="white",Rgba=0xFFF7E8FF,MaterialId="stone"};
            check(ProductTemplates.Matches(snapshot) && ProductTemplates.CreateAtlas(snapshot).Height==32
                && ProductTemplates.CreateAtlas(snapshot).Cells[511]?.MaterialId=="stone",
                "单视图家具图集按实际模板尺寸生成，保留底部像素");
            var atlas=ProductTemplates.CreateAtlas(snapshot);atlas.Cells[511]!.Rgba=0;
            check(snapshot.Design.Views["front"].Cells[511]!.Rgba!=0,"不同尺寸家具生成图集不共享图纸可变数据");
            snapshot.Design.Use=ProductUse.WoodFurniture;
            check(!ProductTemplates.Matches(snapshot),"木摆件不能冒用相同大小的石雕模板身份");
            bool failed=false;
            try{FurnitureTemplates.Configure(new[]{definition,definition with{PixelHeight=16}});}catch(ArgumentException){failed=true;}
            check(failed && FurnitureTemplates.TryGet(definition.Id,out var retained) && retained.PixelHeight==32,
                "重复模板身份冲突时拒绝整个注册，保留原配置");
            failed=false;
            try{FurnitureTemplates.Configure(new[]{definition with{PixelWidth=17}});}catch(ArgumentException){failed=true;}
            check(failed,"非原生家具格尺寸拒绝，不截断成错误源矩形");
            check(ProductTemplates.Matches(ProductTemplates.DebugSample(false,false,"legacy")),"扩展家具注册后旧拼豆画样例仍可读取");
        }
        finally{FurnitureTemplates.Configure(Array.Empty<FurnitureTemplateDefinition>());}
        check(!FurnitureTemplates.TryGet(definition.Id,out _) && FurnitureTemplates.All.Count==1,
            "测试注册清理后仅保留原有调试模板，不启用任何待确认生产模板");
    }
}
