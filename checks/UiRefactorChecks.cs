using BetterBeads.Data;

internal static class UiRefactorChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(var (w,h) in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
        {
            var l=EditorLayout.Calculate(w,h);var p=ColorPickerLayout.Calculate(l.Frame);
            var toolbar=new[]{l.ProductButton,l.PaintButton,l.EraseButton,l.MoreButton}.Concat(l.UsesDrawers?new[]{l.SidebarButton}:Array.Empty<UiRect>()).ToArray();
            check(toolbar.All(r=>r.Height>=40 && l.CanvasToolbar.Contains(r) && !toolbar.Any(o=>o!=r && r.Overlaps(o))),$"{w}×{h}工具栏真实按钮均至少40像素且无重叠");
            check(l.Header.Contains(l.Title) && l.Header.Contains(l.EditorTab) && l.Header.Contains(l.LibraryTab) && l.Header.Contains(l.CloseButton) && !l.Title.Overlaps(l.EditorTab) && !l.EditorTab.Overlaps(l.LibraryTab) && l.CloseButton.X-l.LibraryTab.Right>=8,$"{w}×{h}标题、两页签和关闭入口处于同一行且互不挤占");
            check(l.PaintButton.X-l.ProductButton.Right==8 && l.EraseButton.X-l.PaintButton.Right==8 && l.MoreButton.X-l.EraseButton.Right==8 && (!l.UsesDrawers || l.SidebarButton.Width>=114 && l.SidebarButton.X-l.MoreButton.Right==8),$"{w}×{h}工具按连续8像素间距排列，小屏豆色／材料入口无需缩字");
            check(ColorPickerLayout.FavoriteCells(l.Tools).All(r=>r.Width>=32 && r.Width<=50) && l.Frame.Height<=800 && ColorPickerLayout.FavoriteAction(l.Tools).Height>=(l.UsesDrawers?40:44),$"{w}×{h}常用豆格保留32至50像素，染色入口符合标准或紧凑按钮高度");
            int x=l.Canvas.X+2,y=l.Canvas.Y+2;
            check(l.AcceptsCanvasInput(false,x,y) && !l.AcceptsCanvasInput(true,x,y) && !l.AcceptsCanvasInput(false,l.MakeButton.X,l.MakeButton.Y),$"{w}×{h}覆盖层屏蔽盘面输入，底部按钮区域不能引起缩放或摆豆");
            bool safe=true;
            foreach(var multiple in new[]{false,true})
            {
                var m=MaterialPanelLayout.Calculate(l.Materials,multiple);
                var rows=new[]{m.Title,m.Amount,m.Range,m.Inspect}.Concat(multiple?new[]{m.View}:Array.Empty<UiRect>()).ToArray();
                safe&=rows.All(r=>l.Materials.Contains(r) && !rows.Any(o=>o!=r && o.Overlaps(r)));
            }
            var weapon=PickerLayout.Materials(l.Materials,22,0);
            safe&=weapon.Cells.All(c=>c.Rect.Height>=40 && l.Materials.Contains(c.Rect));
            check(safe,$"{w}×{h}装饰、四向服装与武器材料区均保留完整操作空间");
            var slots=ColorPickerLayout.FavoriteCells(p.Slots,false);
            check(slots.Count==16 && slots.All(r=>r.Width>=32 && p.Slots.Contains(r)) && !p.Slots.Overlaps(p.Swatch),$"{w}×{h}染色页豆格和前后预览互不覆盖，色格至少32像素");
            var actions=new[]{p.Previous,p.Next,p.Cancel,p.Apply};
            check(actions.All(r=>r.Height>=40 && p.Dialog.Contains(r) && !actions.Any(o=>o!=r && r.Overlaps(o))) && !p.SaturationValue.Overlaps(p.Previous),$"{w}×{h}染色重置、取消、保存始终可见且不挤占色区");
            int px=p.SaturationValue.X+4,py=p.SaturationValue.Y+4;
            check(p.CanPick(true,px,py) && (p.Compact?!p.CanPick(false,px,py):p.CanPick(false,px,py)) && !p.CanPick(true,p.Apply.X,p.Apply.Y),$"{w}×{h}紧凑豆色盒页不会误操作隐藏色区，保存位置也不会取色");
            check(p.Swatch.Contains(p.Info) && p.Info.Width>=100 && (!p.Compact || p.Swatch.Y+4+p.PreviewBeadSize<=p.SlotInfo.Y && !p.Info.Overlaps(p.SlotInfo)),$"{w}×{h}染色预览与编号分行，完整六位色值有固定字号空间");
            var replacement=PickerLayout.Materials(l.DrawerContent,27,999,true);
            check(replacement.Cells.All(c=>l.DrawerContent.Contains(c.Rect) && !c.Rect.Overlaps(replacement.Source) && !c.Rect.Overlaps(replacement.Previous)) && replacement.Cells.Last().Index==26 && l.DrawerContent.Contains(replacement.Source),$"{w}×{h}批量替换保留来源选择与末页材料，不与分页控件重叠");
        }
        check(EditorLayout.Calculate(899,640).UsesDrawers && EditorLayout.Calculate(900,639).UsesDrawers && !EditorLayout.Calculate(900,640).UsesDrawers,"900×640临界值选择正确的呈现模式");
        var controls=new FrameControls();int saved=0,picked=0;
        var r=new UiRect(0,0,40,40);controls.Add((r,()=>saved++));controls.Clear();controls.Add((r,()=>picked++));
        check(controls.TryInvoke(10,10) && picked==1 && saved==0 && !controls.TryInvoke(10,10),"切入侧栏清空底层保存回调，单次选择不能穿透或重复执行");
    }
}
