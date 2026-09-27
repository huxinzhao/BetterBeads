using BetterBeads.Data;

internal static class LibraryChecks
{
    public static void Run(Action<bool,string> check)
    {
        var progress=new SaveProgress();var library=new BlueprintRepository(progress);
        var source=ProductTemplates.DebugSample(false,false,"Alpha").Design;
        source.Reference=new(){Width=1,Height=1,Pixels=new uint[]{0x123456FF}};
        library.Save(source);
        library.SaveAs(source,"Alpha",out var copy);
        check(copy!.Id!=source.Id && copy.Revision==1 && source.Revision==1 && library.List("alpha",ProductUse.Picture).Count==2,
            "另存允许同名，创建新身份，不覆盖源图纸");
        copy.Views["front"].Cells[51]!.MaterialId="changed";copy.Reference!.Pixels[0]=0;
        library.TryOpen(source.Id,out var original);
        check(original!.Views["front"].Cells[51]!.MaterialId=="decoration" && original.Reference!.Pixels[0]==0x123456FF,
            "另存后的网格和参考像素独立");
        check(library.Duplicate(source.Id,"Beta",out var duplicated) && duplicated!.Id!=source.Id
            && library.List("Beta").Count==1 && library.List("",ProductUse.Hat).Count==0,
            "库内复制、名称搜索与用途筛选保持独立身份");
        var selected=library.List().Single(e=>e.Id==source.Id);
        check(library.Rename(source.Id,source.Revision,"Renamed") && !library.Delete(source.Id,selected.Raw)
            && !library.Rename(source.Id,source.Revision,"Stale"),"确认期间发生修改，旧删除和旧改名请求均拒绝");
        library.TryOpen(source.Id,out var latest);
        selected=library.List().Single(e=>e.Id==source.Id);
        check(library.Delete(source.Id,selected.Raw) && !library.Save(latest!) && library.TryOpen(copy.Id,out _),
            "删除不影响同名副本，旧草稿不能意外复活被删图纸");
        check(library.SaveAs(latest!,"Recovered",out _),"被删图纸的打开副本仍可显式另存恢复");
        progress.BlueprintRecords["broken"]="{broken";
        var broken=library.List().Single(e=>e.Id=="broken");
        progress.BlueprintRecords["mismatched"]=DesignStorage.Serialize(original!);
        check(!library.TryOpen("mismatched",out var invalid) && invalid is null
            && !library.List().Single(e=>e.Id=="mismatched").IsReadable,
            "记录键与内部身份不一致时不可读，不向列表泄露错误编辑目标");
        check(!broken.IsReadable && broken.Raw=="{broken" && !library.Duplicate("broken","Copy",out _)
            && progress.BlueprintRecords["broken"]=="{broken","损坏记录在列表中保留，不隐式删除或复制为空图");
        var editor=new EditorDocument(original!,DefaultProcessing.Create(),"front");
        editor.Rename("Unsaved");
        check(!editor.TryOpen(copy) && editor.Snapshot().Name=="Unsaved","未保存草稿拒绝直接被另一图纸替换");
        editor.DiscardChanges();
        check(editor.TryOpen(copy) && editor.Snapshot().Id==copy.Id && !editor.IsDirty && !editor.CanUndo,
            "显式放弃后可打开独立副本并清理旧撤销历史");
        copy.Name="External";
        check(editor.Snapshot().Name=="Alpha" && !editor.TryOpen(new Blueprint()),
            "已打开编辑副本与调用方隔离，缺少视图不破坏当前草稿");
    }
}
