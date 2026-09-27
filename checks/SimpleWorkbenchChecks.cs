using BetterBeads.Data;

internal static class SimpleWorkbenchChecks
{
    public static void Run(Action<bool,string> check)
    {
        ButtonMotion slow=default,fast=default;
        for(int i=0;i<30;i++)slow=slow.Step(true,true,true,1d/30);
        for(int i=0;i<120;i++)fast=fast.Step(true,true,true,1d/120);
        check(Math.Abs(slow.Hover-fast.Hover)<0.00001 && Math.Abs(slow.Press-fast.Press)<0.00001,"按钮动效在30与120帧下按相同时间收敛");
        for(int i=0;i<120;i++)fast=fast.Step(false,false,true,1d/120);
        check(fast.Hover<0.001 && fast.Press<0.001 && fast.Offset==1,"移开与松手后按钮回到原高度，点击范围不参与动画");
        check(slow.Step(true,true,false,1)==default && slow.Step(true,true,true,-1)==slow,"禁用按钮清除反馈，负时间不会倒放动画");
        var catalog=DefaultProcessing.Create();
        check(catalog.Sources.Count==22 && catalog.Sources.Single(s=>s.ItemId=="(O)334").Yield==12
            && catalog.Sources.Single(s=>s.ItemId=="(O)440").Yield==24,"原料目录保留已确认的金属与羊毛折算量");
        check(catalog.Sources.Where(s=>s.MaterialId is "wood" or "hardwood" or "stone" or "decoration")
            .All(s=>MaterialRules.Canonical(catalog.Materials.Single(m=>m.Id==s.MaterialId))=="decoration"),"非金属宝石羊毛统一普通拼豆");
        foreach(var recipe in DefaultManufacturing.Recipes())
        {
            bool all=catalog.Materials.All(m=>
            {
                var grid=new BeadGrid{Width=recipe.Template.Width,Height=recipe.Template.Height,Cells=Enumerable.Repeat<BeadCell?>(null,recipe.Template.Width*recipe.Template.Height).ToList()};
                grid.Cells[0]=new(){ColorId="white",Rgba=0xFFF7E8FF,MaterialId=m.Id};
                var d=new Blueprint{Use=recipe.Template.Use,TemplateId=recipe.Template.Id,Views=new(){["front"]=grid}};
                var report=DraftAssessment.Evaluate(d,recipe.Template,catalog,new HashSet<string>(),new Dictionary<string,int>{{"decoration",Math.Max(1,recipe.Template.StructureBudget)}});
                return report.CanMake && report.Materials.Single().Id=="decoration" && report.Materials.Single().Needed==Math.Max(1,recipe.Template.StructureBudget);
            });
            check(all,$"{recipe.Template.Id} 旧图纸所有配料自动按普通豆计费，不扣贵重材料");
        }
        check(!MaterialRules.CanUse(catalog.Materials.Single(m=>m.Id=="wood"),ProductUse.Sword)
            && !MaterialRules.CanUse(catalog.Materials.Single(m=>m.Id=="ruby"),ProductUse.Hat)
            && MaterialRules.CanUse(catalog.Materials.Single(m=>m.Id=="wool"),ProductUse.Hat),"武器与服装材质限制保持");
        foreach(var (width,height) in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
        {
            var l=EditorLayout.Calculate(width,height);
            check((l.UsesDrawers || l.Tools.X==l.Materials.X && l.Board.Right<l.Tools.X && l.Tools.Bottom<l.Materials.Y) && l.Body.Contains(l.Board)
                && !l.Board.Overlaps(l.CanvasToolbar) && l.Canvas.Width>=16 && l.Canvas.Height>=16,
                $"{width}×{height}双栏或紧凑工作台的盘面、工具栏与工作栏无重叠");
            var palette=ColorPickerLayout.FavoriteCells(l.Tools);
            bool safe=palette.Count==16 && palette.All(r=>l.Tools.Contains(r) && !r.Overlaps(ColorPickerLayout.FavoriteCaption(l.Tools)) && !r.Overlaps(ColorPickerLayout.FavoriteAction(l.Tools)) && !palette.Any(other=>other!=r && other.Overlaps(r)));
            check(safe,$"{width}×{height} 固定4×4常用色盒完整可见且互不重叠");
            var picker=ColorPickerLayout.Calculate(l.Frame);
            check(l.Frame.Contains(picker.Dialog) && picker.Dialog.Contains(picker.SaturationValue)
                && picker.Dialog.Contains(picker.Hue) && picker.Dialog.Contains(picker.Apply)
                && !picker.SaturationValue.Overlaps(picker.Previous) && !picker.Hue.Overlaps(picker.Slots)
                && !picker.Previous.Overlaps(picker.Next) && !picker.Next.Overlaps(picker.Cancel)
                && ColorPickerLayout.FavoriteCells(picker.Slots,false).All(picker.Slots.Contains),
                $"{width}×{height} HSV调色板、常用格与确认区域无重叠");
            var covered=new HashSet<int>();
            var area=l.Materials;
            var material=PickerLayout.Materials(area,22,0);covered.Clear();safe=true;
            for(int page=0;page<material.Pages;page++)
            {
                var p=PickerLayout.Materials(area,22,page);
                safe&=area.Contains(p.Previous)&&area.Contains(p.Next);
                foreach(var (index,r) in p.Cells){covered.Add(index);safe&=area.Contains(r)&&!r.Overlaps(p.Caption)&&!r.Overlaps(p.Previous)&&!r.Overlaps(p.Next);}
            }
            check(safe && covered.Count==22,$"{width}×{height}右下材质可翻页选到所有材料，不与上方针/橡皮重叠");
        }
    }
}
