using BetterBeads.Data;

internal static class OptimizationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog=DefaultProcessing.Create();
        var unlocked=new HashSet<string>();
        var recipe=DefaultManufacturing.Recipes().First();
        var design=ProductTemplates.DebugSample(false,false,"Original").Design;
        foreach(var bead in design.Views["front"].Cells.Where(c=>c is not null))bead!.ColorId="white";
        var inventory=new InventorySlot?[]{new("beads",BeadItems.BeadId("decoration"),999,999,CanReceiveBeads:true),null};
        void Compare(string label,ManufacturingFailure expected,bool creative=false,int money=10)
        {
            string before=DesignStorage.Serialize(new{design,inventory,catalog,unlocked});
            var status=ManufacturingTransaction.CheckAvailability(design,recipe,catalog,unlocked,inventory,money,creative);
            var preview=ManufacturingTransaction.Preview(design,recipe,catalog,unlocked,inventory,money,"check",creative);
            check(status.Failure==expected && status.Failure==preview.Failure && status.CanMake==(preview.Plan is not null)
                && DesignStorage.Serialize(status.Report)==DesignStorage.Serialize(preview.Report)
                && before==DesignStorage.Serialize(new{design,inventory,catalog,unlocked}),label);
        }
        Compare("轻量检查与确认预览一致，且不修改图案、库存或规则",ManufacturingFailure.None);
        inventory[1]=new("full","(O)388",1,999);
        Compare("轻量检查仍拒绝满包",ManufacturingFailure.NoSpace);
        inventory[0]=inventory[0]! with{Count=design.Views["front"].Cells.Count(c=>c is not null)};
        Compare("轻量检查允许扣料腾出的空位",ManufacturingFailure.None);
        Compare("创造模式不扣材料，不能借用原本不会释放的槽位",ManufacturingFailure.NoSpace,true);
        inventory[1]=null;inventory[0]=inventory[0]! with{Count=1};
        Compare("库存变少立即显示缺料",ManufacturingFailure.InvalidDesign);
        Compare("切创造模式立即更新可制作状态",ManufacturingFailure.None,true);
        inventory[0]=inventory[0]! with{Count=999};
        var cell=design.Views["front"].Cells.First(c=>c is not null)!;cell.ColorId="red";
        Compare("旧色卡未解锁不再阻止制作",ManufacturingFailure.None);
        unlocked.Add("red");Compare("解锁颜色后无需重开菜单",ManufacturingFailure.None);
        recipe=recipe with{ExtraCost=11};Compare("费用变化立即反馈",ManufacturingFailure.InsufficientFunds);
        Compare("余额变化立即反馈",ManufacturingFailure.None,money:11);
        recipe=recipe with{Template=recipe.Template with{Width=8}};
        Compare("版型规则变化立即反馈",ManufacturingFailure.InvalidDesign,money:11);
        recipe=recipe with{Template=recipe.Template with{BillingView="missing"}};
        Compare("无效规则与提交预览一致拒绝",ManufacturingFailure.InvalidRecipe);

        recipe=DefaultWeapons.Recipes().First();
        design=new Blueprint{TemplateId=recipe.Template.Id,Use=recipe.Template.Use,
            Views=new(){["front"]=new(){Width=recipe.Template.Width,Height=recipe.Template.Height,
                Cells=Enumerable.Repeat<BeadCell?>(null,recipe.Template.Width*recipe.Template.Height).ToList()}},
            SupplementaryMaterials=new(){{"copper",recipe.Template.MinimumMaterials}}};
        design.Views["front"].Cells[0]=new(){ColorId="white",Rgba=0xFFF7E8FF,MaterialId="copper"};
        inventory[0]=new("metal",BeadItems.BeadId("copper"),999,999,CanReceiveBeads:true);
        Compare("武器轻量检查包含完整数值规则",ManufacturingFailure.None);
        recipe=recipe with{SpecialEffectsEnabled=false};design.SelectedEffects.Add("ruby");
        Compare("武器未开放的效果仍阻止制作",ManufacturingFailure.InvalidWeaponStats);

        var source=ProductTemplates.DebugSample(true,false,"Hat").Design;
        var progress=new SaveProgress();var repository=new BlueprintRepository(progress);repository.Save(source);
        string original=progress.BlueprintRecords[source.Id];
        var editor=new EditorDocument(source,catalog,"back");
        editor.BeginStroke(0,0,BrushTool.Paint,"white",null);
        check(editor.IsDirty,"落笔时即时更新脏状态，无需等松开鼠标");
        check(editor.SaveAs(repository,out var copy) && !editor.IsStrokeActive && !editor.IsDirty && !editor.CanUndo
            && editor.View=="back" && editor.Snapshot().Id==copy!.Id && copy.Id!=source.Id
            && copy.Views["back"].Cells[0] is not null && progress.BlueprintRecords[source.Id]==original,
            "未保存笔划另存后进入新副本、保留方向、原记录不变");
        editor.Rename("Copy changed");var next=editor.Snapshot();repository.Save(next);editor.MarkSaved(next.Revision);
        check(progress.BlueprintRecords[source.Id]==original && repository.TryOpen(copy!.Id,out var updated)
            && updated!.Name=="Copy changed" && !editor.IsDirty,"另存后的保存只更新副本，不误写原图纸");
        editor.Undo();check(editor.IsDirty,"保存后的撤销刷新脏状态缓存");
        editor.Redo();check(!editor.IsDirty,"重做恢复已保存内容后清除脏状态");
        editor.BeginStroke(0,0,BrushTool.Erase,"white",null);
        check(editor.IsDirty,"擦除即时刷新脏状态");
        editor.DiscardChanges();check(!editor.IsDirty && editor.Pick(0,0) is not null,"放弃改动恢复另存副本基线");
        editor.BeginStroke(0,0,BrushTool.Paint,"white",null);editor.ContinueStroke(0,0);editor.EndStroke();
        check(!editor.IsDirty && !editor.CanUndo,"同色停留和重涂不产生脏状态或虚假撤销步骤");

        var controls=new FrameControls();int lower=0,top=0;var rect=new UiRect(0,0,20,20);
        controls.Add((rect,()=>lower++));
        controls.Add((rect,()=>{top++;controls.TryInvoke(5,5);}));
        check(controls.TryInvoke(5,5) && top==1 && lower==0 && !controls.IsReady && !controls.TryInvoke(5,5),
            "弹窗只响应最上层按钮，操作前失效旧命中，阻止连击和重入");
        controls.Add((rect,()=>top++));controls.Clear();
        check(!controls.TryInvoke(5,5) && top==1,"Esc或布局改变后旧确认按钮不可点击");
        controls.Add((rect,()=>top++));
        check(!controls.TryInvoke(20,5) && controls.IsReady && controls.TryInvoke(5,5) && top==2,
            "重新绘制后恢复交互，边界外点击不消耗有效按钮");
    }
}
