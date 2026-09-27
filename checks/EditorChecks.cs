using BetterBeads.Data;

internal static class EditorChecks
{
    public static void Run(Action<bool,string> check)
    {
        var catalog = DefaultProcessing.Create();
        var design = new Blueprint { Use = ProductUse.WoodFurniture, Views = new() {
            ["front"] = new() { Width = 4, Height = 3, Cells = Enumerable.Repeat<BeadCell?>(null, 12).ToList() } } };
        var editor = new EditorDocument(design, catalog, "front");
        editor.BeginStroke(0,0,BrushTool.Paint,"brown","wood"); editor.ContinueStroke(3,0); editor.EndStroke();
        check(editor.ViewSnapshot().Cells.Take(4).All(c => c?.MaterialId == "decoration") && editor.IsDirty
            && design.Views["front"].Cells.All(c => c is null), "快速拖笔补齐中间格且草稿与原图隔离");
        check(editor.Undo() && editor.ViewSnapshot().Cells.All(c => c is null) && !editor.IsDirty && !editor.CanUndo,
            "一次拖笔只记一次撤销，撤销回原图不再标记脏");
        editor.Redo(); editor.MarkSaved(3);
        editor.BeginStroke(0,0,BrushTool.Paint,"red","hardwood"); editor.EndStroke();
        check(editor.Pick(0,0) is { ColorId: "red", MaterialId: "decoration" }, "覆盖已有豆只改颜色，不改变默认材质以外的真实材质");
        editor.Undo();
        check(!editor.IsDirty && editor.Snapshot().Revision == 3, "保存后的撤销保留修订号并正确判定未修改");
        editor.BeginStroke(1,0,BrushTool.Material,"red","hardwood"); editor.EndStroke();
        editor.Fill(0,0,"white","wood");
        check(editor.Pick(0,0)!.MaterialId == "decoration" && editor.Pick(1,0)!.MaterialId == "hardwood"
            && editor.ViewSnapshot().Cells.Take(4).All(c => c?.ColorId == "white"), "同色填充跨材质区域保留各格配料");
        editor.BeginStroke(0,1,BrushTool.Material,"white","wood"); editor.EndStroke();
        check(editor.Pick(0,1) is null && !editor.BeginStroke(0,0,BrushTool.Material,"white","missing-material"), "材质工具不在空格生豆且拒绝未注册材料");
        editor.BeginStroke(0,1,BrushTool.Paint,"black","wood"); editor.ContinueStroke(-1,1); editor.ContinueStroke(3,1);
        check(!editor.IsStrokeActive && editor.Pick(3,1) is null, "离开画布结束笔划，按住重新进入不能继续画");
        var before = editor.ViewSnapshot(); editor.Mirror(true); editor.Mirror(true);
        check(DesignStorage.Serialize(before) == DesignStorage.Serialize(editor.ViewSnapshot()), "镜像往返保留颜色材质与透明格");
        check(!editor.ReplaceMaterial("wood","missing-material") && editor.ReplaceMaterial("decoration","hardwood"), "全图换材质拒绝未注册材料");
        var repairDraft=new Blueprint{Use=ProductUse.WoodFurniture,TemplateId="check.repair",
            Views=new(){["front"]=new(){Width=4,Height=1,Cells=new(){
                new(){ColorId="white",Rgba=0xFFF7E8FF},null,
                new(){ColorId="black",Rgba=0x25242AFF,MaterialId="decoration"},
                new(){ColorId="white",Rgba=0xFFF7E8FF,MaterialId="unknown.old-material"}}}}};
        var repair=new EditorDocument(repairDraft,catalog,"front");
        check(repair.CountMaterial(null)==1 && repair.ReplaceMaterial(null,"wood") && repair.Pick(0,0)!.MaterialId=="wood"
            && repair.Pick(1,0) is null,"全图补充未配料只作用于已有图案格，不在透明格生成豆子");
        check(repair.Undo() && !repair.IsDirty && repair.Pick(0,0)!.MaterialId is null,"未配料批量修复可一次撤销恢复原状态");
        check(repair.ReplaceMaterial("decoration","hardwood") && repair.Pick(2,0) is {ColorId:"black",Rgba:0x25242AFF,MaterialId:"hardwood"},
            "切用途后的不兼容材料可批量换成合法材料，图案颜色保持不变");
        check(repair.ReplaceMaterial("unknown.old-material","wood") && repair.Pick(3,0)!.MaterialId=="wood",
            "已经移除的未知材料也可明确替换修复");
        check(!repair.ReplaceMaterial(null,"missing-material") && repair.Pick(0,0)!.MaterialId is null,
            "未配料修复仍拒绝未注册的目标材料");
        var preview = editor.Snapshot(); preview.Views["front"].Cells[0] = null;
        check(editor.Pick(0,0) is not null && editor.ApplyCandidate(preview) && editor.Pick(0,0) is null && editor.Undo()
            && editor.Pick(0,0) is not null, "候选图案预览隔离，确认一次应用可一次撤销");
        var hat = ProductTemplates.DebugSample(true,false,"hat");
        var clothes = new EditorDocument(hat.Design,catalog,"front");
        check(!clothes.BeginStroke(0,0,BrushTool.Material,"white","wool") && !clothes.ReplaceMaterial("wool","wood"),
            "服装拒绝逐格材质工具");
        clothes.BeginStroke(0,0,BrushTool.Paint,"white","wood"); clothes.EndStroke();
        check(clothes.Pick(0,0) is { MaterialId: null } && clothes.Snapshot().WoolMaterials["wool"] == 80,
            "服装画图不产生逐格配料且保留固定预算");
        var front=DesignStorage.Serialize(clothes.ViewSnapshot());
        clothes.SetView("back");
        clothes.BeginStroke(0,0,BrushTool.Paint,"black","wool");
        clothes.SetView("front");
        check(!clothes.IsStrokeActive && DesignStorage.Serialize(clothes.ViewSnapshot())==front
            && clothes.Snapshot().Views["back"].Cells[0]?.ColorId=="black",
            "切换方向结束笔划且不同方向编辑互不覆盖");
        clothes.Undo();
        check(clothes.View=="front" && clothes.Snapshot().Views["back"].Cells[0] is null
            && DesignStorage.Serialize(clothes.ViewSnapshot())==front && clothes.Snapshot().WoolMaterials["wool"]==80,
            "跨方向撤销只回退设计内容，不切换方向或重复改变预算");
        var transform = new CanvasTransform(); transform.Layout(new(100,100,200,200)); transform.Center(16,16);
        var cell = transform.CellRect(5,6);
        check(transform.TryCell(cell.X+3,cell.Y+3,16,16,out int x,out int y) && x == 5 && y == 6,
            "绘制与命中使用同一坐标换算");
        transform.ZoomAt(cell.X+3,cell.Y+3,1);
        check(transform.TryCell(cell.X+3,cell.Y+3,16,16,out x,out y) && x == 5 && y == 6,
            "鼠标锚点缩放后保持同一格");
        transform.Pan(-30,25); cell = transform.CellRect(5,6);
        check(transform.TryCell(cell.X+2,cell.Y+2,16,16,out x,out y) && x == 5 && y == 6
            && !transform.TryCell(300,200,16,16,out _,out _), "平移后命中一致，画布右边界不落豆");
    }
}
