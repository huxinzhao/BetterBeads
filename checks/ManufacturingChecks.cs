using BetterBeads.Data;

internal static class ManufacturingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();var unlocked=new HashSet<string>();
        var design=new Blueprint{Name="Transaction sample",TemplateId="check.template",Use=ProductUse.WoodFurniture,
            Views=new(){["front"]=new(){Width=2,Height=1,Cells=new(){
                new(){ColorId="white",Rgba=0xFFF7E8FF,MaterialId="wood"},new(){ColorId="black",Rgba=0x25242AFF,MaterialId="hardwood"}}}}};
        // Deliberately synthetic template/fee: not a proposed or confirmed gameplay setting.
        var recipe=new ManufacturingRecipe(new("check.template",ProductUse.WoodFurniture,2,1,new[]{"front"},"front",ManufacturingAvailable:true),"(F)check.product",7);
        var inventory=new InventorySlot?[]{new("wood",BeadItems.BeadId("wood"),1,999,CanReceiveBeads:true),
            new("hardwood",BeadItems.BeadId("hardwood"),2,999,CanReceiveBeads:true)};
        var before=DesignStorage.Serialize(inventory);var draftBefore=DesignStorage.Serialize(design);
        var preview=ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,10,"one");var plan=preview.Plan!;
        check(preview.Failure==ManufacturingFailure.None && plan.OutputSlot==0 && plan.MoneyAfter==3
            && plan.Changes[0].After is {Count:1,MaxStack:1} && plan.Changes[1].After?.Count==1,
            "满包制造使用本次扣料释放槽位，逐材质扣料并只产一件");
        check(DesignStorage.Serialize(inventory)==before && DesignStorage.Serialize(design)==draftBefore,
            "制造预览不改库存、金钱或草稿");
        design.Views["front"].Cells[0]!.Rgba=0;
        var product=plan.Product;product.Design.Name="Mutated";product.ActualMaterials["wood"]=99;
        check(plan.Product.Design.Name=="Transaction sample" && plan.Product.Design.Views["front"].Cells[0]!.Rgba==0xFFF7E8FF
            && plan.Product.ActualMaterials["decoration"]==2 && plan.Product.FinalStats.Count==0 && plan.Product.Effects.Count==0,
            "冻结成品快照不受原草稿或返回副本修改影响，不继承来源属性");
        check(plan.Matches(inventory,10) && !plan.Matches(inventory,9),"提交核对包含金钱，不使用过期余额");
        design.Views["front"].Cells[0]!.Rgba=0xFFF7E8FF;
        inventory[0]=inventory[0]! with{Identity="replacement"};
        check(!plan.Matches(inventory,10),"同ID同数量的替换实例也使制造计划过期");
        inventory[0]=inventory[0]! with{Count=3};
        check(ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,10,"two").Failure==ManufacturingFailure.NoSpace,
            "满包且扣料未释放槽位时拒绝，不掉落替代交付");
        check(ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,6,"three").Failure==ManufacturingFailure.InsufficientFunds,
            "模板费用不足不返回可提交计划");
        inventory[0]=inventory[0]! with{CanReceiveBeads=false};inventory[1]=inventory[1]! with{Count=1};
        var special=ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,10,"four");
        check(special.Plan is null && special.Report!.Issues.Any(i=>i.Code=="insufficient-material"),
            "带特殊状态的同ID豆子不计为可扣库存");
        inventory[0]=inventory[0]! with{CanReceiveBeads=true,Count=2};design.Views["front"].Cells[0]!.Rgba=0xFFFFFFFF;
        design.Views["front"].Cells[1]!.MaterialId="unknown";
        check(ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,10,"five").Plan is not null,
            "旧装饰图案未知材质不阻止自动普通豆计费");
        design.Views["front"].Cells[1]!.MaterialId="hardwood";design.Views["front"].Cells[1]!.ColorId="red";
        check(ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,10,"six").Plan is not null,
            "足料时使用任意颜色，不检查色卡解锁");
        var gate=new TransactionRequests();
        check(gate.TryBegin(plan.RequestId) && !gate.TryBegin("other"),"制造请求执行中阻止重入");
        gate.Finish(plan.RequestId,true);
        check(!gate.TryBegin(plan.RequestId) && gate.TryBegin("retry-new"),"同制造请求完成后不能重复交付，明确新请求可继续");
        gate.Finish("retry-new",false);
        check(gate.TryBegin("retry-new"),"制造失败未交付时允许原请求重试");
        gate.Finish("retry-new",true);
        var invalidRecipe=recipe with{Template=recipe.Template with{BillingView="missing"}};
        check(ManufacturingTransaction.Preview(design,invalidRecipe,catalog,unlocked,inventory,10,"seven").Failure==ManufacturingFailure.InvalidRecipe,
            "非法计费视图拒绝，避免使用错误模板计算免费成品");
        inventory[1]=inventory[1]! with{Identity=inventory[0]!.Identity};
        check(ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,10,"eight").Failure==ManufacturingFailure.InvalidInventory,
            "同一物品实例重复占用槽位时拒绝，避免重复计算可扣库存");
    }
}
