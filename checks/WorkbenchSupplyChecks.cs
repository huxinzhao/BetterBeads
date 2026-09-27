using BetterBeads.Data;

internal static class WorkbenchSupplyChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();var unlocked=new HashSet<string>();
        check(new WorkbenchSettings().NearbyChestRadius==5 && MaterialSupply.WithinRange(15,15,10,10,5)
            && MaterialSupply.WithinRange(5,5,10,10,5) && !MaterialSupply.WithinRange(16,10,10,10,5)
            && !MaterialSupply.WithinRange(10,4,10,10,5),"箱子默认横纵5格含边界，6格排除");
        var recipe=DefaultManufacturing.Recipes()[0];
        Blueprint Picture(int beads)
        {
            var t=recipe.Template;
            return new(){TemplateId=t.Id,Use=t.Use,Views=new(){["front"]=new(){Width=t.Width,Height=t.Height,
                Cells=Enumerable.Range(0,t.Width*t.Height).Select(i=>i<beads?new BeadCell{ColorId="rgb.123456",Rgba=0x123456FF,MaterialId="diamond"}:null).ToList()}}};
        }
        InventorySlot Raw(string id,string item,int count,string material,int yield,int quality=0)=>new(id,item,count,999,RawMaterial:material,Yield:yield,Quality:quality);
        InventorySlot Beads(string id,string material,int count)=>new(id,BeadItems.BeadId(material),count,999,CanReceiveBeads:true);
        ManufacturingPreview Preview(Blueprint design,InventorySlot?[] slots,int bag=1,bool creative=false)
            =>ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,slots,0,"supply-test",creative,bag,"bag+chest");
        var slots=new InventorySlot?[]{null,Raw("wood","(O)388",20,"decoration",1)};
        string frozen=DesignStorage.Serialize(slots);var design=Picture(7);
        var plan=Preview(design,slots).Plan!;
        check(plan.OutputSlot==0 && plan.Changes.Single(c=>c.Slot==1).After!.Count==13
            && plan.Product.ActualMaterials.Single().Key=="decoration" && DesignStorage.Serialize(slots)==frozen,
            "附近箱子木材直接熨烫，成品只到背包、预览不扣料且不消耗图案旧钻石配料");
        check(plan.InventoryScope=="bag+chest" && plan.BackpackSlots==1,"制造计划冻结容器范围与背包容量");
        slots=new InventorySlot?[]{new("bag","(O)390",999,999),Raw("wood","(O)388",7,"decoration",1),null};
        check(Preview(design,slots).Failure==ManufacturingFailure.NoSpace,"箱子腾出的格不能冒充成品背包空间");
        slots=new InventorySlot?[]{Raw("bagwood","(O)388",7,"decoration",1),null};
        check(Preview(design,slots).Plan?.OutputSlot==0,"原料恰好扣完可腾背包格交付");
        slots=new InventorySlot?[]{null,Raw("fiber","(O)771",4,"decoration",2),null};
        plan=Preview(design,slots).Plan!;
        check(plan.Product.ActualMaterials["decoration"]==7 && plan.Changes.Any(c=>c.After is {Count:1,CanReceiveBeads:true})
            && plan.Changes.Single(c=>c.Slot==1).After!.ItemId==BeadItems.BeadId("decoration"),
            "4纤维折8豆只用7豆，余1普通豆回到空位，原料和余豆不重复计费");
        slots=new InventorySlot?[]{null,Raw("fiber","(O)771",5,"decoration",2)};
        check(Preview(design,slots).Failure==ManufacturingFailure.NoSurplusSpace,"成品可放但原料未扣完且余豆无处存放，整笔拒绝");
        slots=new InventorySlot?[]{null,Beads("legacy","hardwood",7),Raw("fiber","(O)771",5,"decoration",2)};
        plan=Preview(design,slots).Plan!;
        check(plan.Changes.All(c=>c.Slot!=2) && plan.Changes.Any(c=>c.Slot==1 && c.After is null),"先消耗旧普通材质豆，再考虑原料");
        slots=new InventorySlot?[]{null,Raw("hardwood","(O)709",5,"decoration",4),Raw("wood","(O)388",7,"decoration",1)};
        plan=Preview(design,slots).Plan!;
        check(plan.Changes.All(c=>c.Slot!=1) && plan.Changes.Single(c=>c.Slot==2).After is null,"普通木材优先于硬木，避免先扣昂贵原料");
        slots=new InventorySlot?[]{null,Raw("goldflower","(O)591",2,"decoration",4,2),Raw("flower","(O)591",2,"decoration",4),null};
        plan=Preview(design,slots).Plan!;
        check(plan.Changes.All(c=>c.Slot!=1),"同来源低品质优先，品质不提高产量");
        slots=new InventorySlot?[]{null,Beads("metal","copper",100),Beads("gem","diamond",100),Beads("wool","wool",100)};
        check(Preview(design,slots).Failure==ManufacturingFailure.InvalidDesign,"装饰缺普通豆时不自动烧掉金属、宝石或羊毛");
        slots=new InventorySlot?[]{null,Raw("wood","(O)388",1,"decoration",1)};
        var before=DesignStorage.Serialize(slots);
        check(Preview(design,slots,1,true).Plan!.Changes.Count==1 && DesignStorage.Serialize(slots)==before,
            "创造模式不扣附近箱子原料，不生成假余豆");

        recipe=ClothingTemplates.Recipes().First();var t=recipe.Template;
        var hat=new Blueprint{TemplateId=t.Id,Use=t.Use,WoolMaterials=new(){{"wood",999}}};
        foreach(string view in t.Views){var g=new BeadGrid{Width=t.Width,Height=t.Height,Cells=Enumerable.Repeat<BeadCell?>(null,t.Width*t.Height).ToList()};g.Cells[0]=new(){ColorId="free-old-color",Rgba=0xFEDCBAFF};hat.Views[view]=g;}
        slots=new InventorySlot?[]{null,Raw("wool","(O)440",4,"wool",24),null};
        plan=Preview(hat,slots).Plan!;
        check(plan.Product.ActualMaterials.Count==1 && plan.Product.ActualMaterials["wool"]==24
            && plan.Changes.Any(c=>c.Slot==1 && c.After is {Count:3,RawMaterial:"wool"}),"帽子自动羊毛预算24豆，4份羊毛只用1份并保留3份，无需手动配料");
        check(plan.Product.Design.Views.Count==4,"四向服装只收费一次且保留全部图案");
        slots[1]=Beads("ordinary","decoration",999);
        check(Preview(hat,slots).Failure==ManufacturingFailure.InvalidDesign,"服装不能用普通豆替代羊毛");

        recipe=DefaultWeapons.Recipes().First();t=recipe.Template;
        var sword=new Blueprint{TemplateId=t.Id,Use=t.Use,SupplementaryMaterials=new(){{"copper",15}},
            Views=new(){["front"]=new(){Width=t.Width,Height=t.Height,Cells=Enumerable.Repeat<BeadCell?>(null,t.Width*t.Height).ToList()}}};
        sword.Views["front"].Cells[0]=new(){ColorId="rgb.123456",Rgba=0x123456FF,MaterialId="copper"};
        slots=new InventorySlot?[]{null,Raw("bar","(O)334",2,"copper",12),null};
        plan=Preview(sword,slots).Plan!;
        check(plan.Product.ActualMaterials["copper"]==16 && plan.Changes.Any(c=>c.After is {Count:8,CanReceiveBeads:true})
            && plan.Product.FinalStats.Count>0,"武器铜锭折豆包含底料、余豆和最终属性计算");
        check(plan.Product.Design.Views["front"].Cells[0]!.Rgba==0x123456FF,"武器自定义颜色不改变属性或材质");
        var prep=BeadPreparation.Preview(slots,1,"scope",catalog,"copper","prep");
        check(prep.Plan is {Produced:12,Consumed:1} && prep.Plan.Changes.Single(c=>c.Slot==0).After!.Count==12,
            "武器备料一次铜锭产12豆到背包，供原生精炼使用");
        check(BeadPreparation.Preview(slots,1,"scope",catalog,"refined-copper","prep").Plan is null,"精炼豆不能绕过熔炉直接折算");
        check(BeadPreparation.Preview(new InventorySlot?[]{Beads("full","wood",999),slots[1],null},1,"scope",catalog,"copper","prep").Plan is null,
            "备料背包满时不把可拿去精炼的豆子静默放进箱子");
        check(DesignStorage.Serialize(slots)==DesignStorage.Serialize(new InventorySlot?[]{null,Raw("bar","(O)334",2,"copper",12),null}),"备料预览与制造预览都不改库存");

        recipe=DefaultManufacturing.Recipes()[0];bool conserved=true;
        for(int need=1;need<=128;need++)
        {
            slots=new InventorySlot?[]{null,Beads("old","stone",3),Raw("raw","(O)771",100,"decoration",2),null};
            var p=Preview(Picture(need),slots).Plan;
            if(p is null){conserved=false;break;}
            var after=slots.ToArray();foreach(var change in p.Changes)after[change.Slot]=change.After;
            int oldStock=MaterialSupply.Stock(slots,catalog)["decoration"],newStock=MaterialSupply.Stock(after,catalog)["decoration"];
            conserved&=oldStock-newStock==need && after[0]?.ItemId==recipe.OutputItemId;
        }
        check(conserved,"128种需求量验证原料折算、旧豆和余豆总量守恒");
        var favorites=BeadPalette.Normalize(null);check(favorites.Count==16 && favorites.All(c=>(byte)c==255),"旧存档无色盒字段时初始化16个不透明常用色");
        check(BeadPalette.Normalize(new uint[]{0x12345600})[0]==0x123456FF,"自定义色盒强制不透明，避免隐形计费豆");
        bool roundtrip=true;
        foreach(uint rgb in BeadPalette.Defaults){var hsv=BeadPalette.ToHsv(rgb);roundtrip&=BeadPalette.FromHsv(hsv.H,hsv.S,hsv.V)==rgb;}
        check(roundtrip && BeadPalette.FromHsv(0,1,1)==0xFF0000FF && BeadPalette.FromHsv(120,1,1)==0x00FF00FF,"HSV调色红绿端点及16个默认色往返无损");
        var edit=new EditorDocument(Picture(1),catalog,"front");
        check(edit.BeginStroke(1,0,BrushTool.Paint,"rgb.ABCDEF","decoration") && edit.Pick(1,0)!.Rgba==0xABCDEFFF,"任意RGB色可直接摆豆");
        var gridBefore=DesignStorage.Serialize(edit.Snapshot());favorites[0]=0xFFFF00FF;
        check(DesignStorage.Serialize(edit.Snapshot())==gridBefore,"修改常用色不重新染色已摆图案");
        edit.EndStroke();check(edit.Undo() && edit.Pick(1,0) is null,"自定义颜色摆豆支持完整撤销");
        var exact=PixelImport.Create(Picture(1),"front",new(){Width=1,Height=1,Pixels=new[]{0x123457FFu}},
            new(ImportSizing.Original,PreserveColors:true),catalog,unlocked);
        check(exact.Candidate.Views["front"].Cells.Any(c=>c?.Rgba==0x123457FF) && exact.LockedColors.Count==0,"原图导入默认可精确保色，不强行量化到常用16色");
        var progress=new SaveProgress{FavoriteColors=favorites};
        var restored=System.Text.Json.JsonSerializer.Deserialize<SaveProgress>(DesignStorage.Serialize(progress));
        check(restored!.FavoriteColors.SequenceEqual(favorites),"16色随存档序列化往返保留");
        var mixed=Picture(2);mixed.Views["front"].Cells[0]!.ColorId="legacy-color";
        var mixedEditor=new EditorDocument(mixed,catalog,"front");
        check(mixedEditor.ReplaceColor("rgb.123456","rgb.ABCDEF") && mixedEditor.ViewSnapshot().Cells.Take(2).All(c=>c?.Rgba==0xABCDEFFF),
            "吸管得到RGB后批量换色也能匹配旧图纸同色ID");
        mixedEditor.Undo();mixedEditor.Fill(0,0,"rgb.ABCDEF","decoration");
        check(mixedEditor.ViewSnapshot().Cells.Take(2).All(c=>c?.Rgba==0xABCDEFFF),"同色填充跨越新旧颜色ID边界，按真实RGB判断");
    }
}
