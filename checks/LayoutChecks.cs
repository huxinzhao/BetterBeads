using BetterBeads.Data;

internal static class LayoutChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var size in new[] { (480,420), (640,480), (854,480), (1024,768), (1280,720), (1280,900), (1920,1080) })
        {
            var layout = EditorLayout.Calculate(size.Item1, size.Item2);
            var viewport = new UiRect(0, 0, size.Item1, size.Item2);
            var area = layout.LibraryArea;
            var library = LibraryLayout.Calculate(area,layout.Frame);
            var import = ImportLayout.Calculate(layout.Frame);
            var lastSource=new UiRect(import.Dialog.X+12,import.Dialog.Y+44+(import.SelectionRows-1)*42,import.Dialog.Width-24,36);
            var lastOption=new UiRect(import.Dialog.X+12,import.Dialog.Y+44+4*42,import.Dialog.Width-24,36);
            check(layout.Frame.Contains(import.Dialog) && import.Dialog.Contains(import.Image)
                && import.Dialog.Contains(import.Status) && import.Dialog.Contains(import.Footer)
                && !import.Image.Overlaps(import.Status) && !import.Status.Overlaps(import.Footer)
                && lastSource.Bottom<=import.Status.Y && lastOption.Bottom<=import.Dialog.Bottom-76,
                $"{size.Item1}×{size.Item2} 导入选择、原图、状态与操作区域不互相挤占");
            check(area.Contains(library.Search) && area.Contains(library.Filter) && area.Contains(library.Rows)
                && area.Contains(library.Status) && area.Contains(library.Actions) && layout.Frame.Contains(library.Dialog)
                && !library.Rows.Overlaps(library.Status) && !library.Status.Overlaps(library.Actions)
                && !library.Search.Overlaps(library.Filter) && library.Dialog.Height>=348,
                $"{size.Item1}×{size.Item2} 图纸库列表、反馈与操作不重叠，详情容纳于框架");
            check(!layout.TooSmall && viewport.Contains(layout.Frame) && layout.Canvas.Width > 0 && layout.Canvas.Height > 0
                && layout.Frame.Contains(layout.Header) && layout.Frame.Contains(layout.Body) && layout.Frame.Contains(layout.Footer)
                && layout.Body.Contains(layout.Canvas) && layout.Footer.Contains(layout.SaveButton) && layout.Footer.Contains(layout.MakeButton)
                && !layout.SaveButton.Overlaps(layout.MakeButton) && !layout.Body.Overlaps(layout.Footer)
                && layout.Frame.Contains(layout.Drawer), $"{size.Item1}×{size.Item2} 画布和固定操作有效，覆盖侧栏始终位于框架内");
            check(layout.UsesDrawers?layout.Frame.Contains(layout.DrawerContent):!layout.Tools.Overlaps(layout.Canvas) && !layout.Materials.Overlaps(layout.Canvas), "常驻工作栏不侵入画布，紧凑侧栏完整容纳于框架");
        }
        check(EditorLayout.Calculate(479,420).TooSmall && EditorLayout.Calculate(480,419).TooSmall,
            "支持边界外使用提示态而非负尺寸控件");
        var r = new UiRect(10,20,40,40);
        check(r.Contains(10,20) && r.Contains(49,59) && !r.Contains(50,40) && !r.Contains(30,60), "命中采用左上包含、右下排除");
    }
}
