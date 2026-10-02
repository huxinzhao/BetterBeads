using BetterBeads.Data;

internal static class DecorationChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(ProductCategories.All.Select(c=>c.Use).SequenceEqual(new[]{ProductUse.Picture,ProductUse.WoodFurniture,
            ProductUse.Wallpaper,ProductUse.Flooring,ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer}),"category order");
        check(ProductCategories.All.Take(4).All(c=>c.Group==ProductGroup.Furniture&&c.Sizes.SequenceEqual(new[]{16,32}))
            &&ProductCategories.All.Skip(4).All(c=>c.Group==ProductGroup.Weapon&&c.Sizes.SequenceEqual(new[]{16,24,32})),"group sizes");
        check(ManufacturingCatalog.Validate(SimpleCrafting.Recipes().Where(r=>SimpleCrafting.IsDecoration(r.Template.Use)).ToArray(),SimpleCrafting.Catalog()).Count==0,
            "decoration recipes registered with their native item types");
        foreach(var use in new[]{ProductUse.Wallpaper,ProductUse.Flooring})foreach(int size in new[]{16,32})
        {
            var category=ProductCategories.For(use)!;
            var design=SimpleCrafting.Blank(category.TemplateFor(size));
            check(SimpleCrafting.Supported(design)&&design.BackgroundRgba==0xEADFC6FF,
                $"decoration template {use} {size}");
            design.Views["front"].Cells[0]=new(){ColorId="one",Rgba=0xC0A080FF,MaterialId="decoration"};
            int fee=size==16?20:40;
            var cost=SimpleCrafting.Cost(design);
            check(cost.Item=="(O)388"&&cost.Count==fee,$"fixed wood fee {use} {size}");
            var recipe=SimpleCrafting.Recipes().Single(r=>r.Template.Id==design.TemplateId);
            var stock=new InventorySlot?[]{new("wood","(O)388",fee,999,RawMaterial:"decoration",Yield:1),null};
            var plan=SimpleCrafting.Evaluate(design,recipe,SimpleCrafting.Catalog(),stock,0,"decor",false,true,2,"").Plan;
            check(plan is not null&&plan.Product.ActualMaterials["(O)388"]==fee
                &&SimpleCrafting.SaleValue(plan.Product,_=>2)==fee*4,$"make and fixed resale {use} {size}");
            string code=BlueprintSharing.Encode(design);
            check(code.StartsWith("BB3.")&&BlueprintSharing.TryDecode(code,out var copy)&&copy!.BackgroundRgba==design.BackgroundRgba
                &&copy.Use==use,$"share roundtrip {use} {size}");
            var document=new EditorDocument(design,SimpleCrafting.Catalog(),"front");
            check(document.SetBackground(0x102030FF)&&document.Undo()&&document.Snapshot().BackgroundRgba==0xEADFC6FF
                &&document.Redo()&&document.Snapshot().BackgroundRgba==0x102030FF,$"background undo {use} {size}");
        }
        check(DecorationPattern.Part(16,9,12)==0&&DecorationPattern.Part(32,0,0)==0
            &&DecorationPattern.Part(32,1,0)==1&&DecorationPattern.Part(32,0,1)==2
            &&DecorationPattern.Part(32,5,7)==3,"tiled quadrant and clipping");
        var tiled=SimpleCrafting.Blank(SimpleCrafting.Wallpaper32);
        uint[] cornerColors={0xB06040FF,0x6080A0FF,0x8060A0FF,0xE0B060FF};
        for(int part=0;part<4;part++)
            tiled.Views["front"].Cells[(part/2*16)*32+part%2*16]=new(){Rgba=cornerColors[part],MaterialId="decoration"};
        var flat=DecorationPattern.Atlas(tiled,new ArtDefinition(),false);
        var textured=DecorationPattern.Atlas(tiled,new ArtDefinition(),true);
        check(flat.Width==256&&flat.Height==16&&textured.Width==1024&&textured.Height==64,
            "decorating atlas retains sixteen logical tile slots at either texture density");
        for(int part=0;part<4;part++)
            check(flat.Pixels[part*16]==cornerColors[part]
                &&textured.Pixels[part*64]==BeadFinish.Bake(tiled.Views["front"],new ArtDefinition()).Pixels
                    [(part/2*16*4)*128+part%2*16*4],$"quadrant {part} preserves the baked bead at its tile origin");
        check(textured.Pixels[4]==tiled.BackgroundRgba,"empty cells use the flat background without bead shading");
        foreach(var view in new[]{(480,420),(1280,900)})
        {
            var layout=SimpleEditorLayout.Calculate(view.Item1,view.Item2);
            var area=layout.UsesDrawers?layout.DrawerContent:layout.Sidebar;
            var actions=SimpleEditorLayout.ColorActions(area,false,true);
            var preview=SimpleEditorLayout.ProductPreview(area,false,true);
            var baseAction=new UiRect(area.X+16,actions.Dye.Y+52,area.Width-32,40);
            check(area.Contains(baseAction),$"base-color button fits supply area at {view.Item1}x{view.Item2}");
            check(preview.Height==0||preview.Height>=56&&preview.Y>=actions.Dye.Bottom+56,
                $"base-color action and preview do not overlap at {view.Item1}x{view.Item2}: preview={preview}, dye={actions.Dye}");
        }
        var left=SimpleCrafting.Blank(SimpleCrafting.Wallpaper16);
        var right=left.Copy();right.BackgroundRgba=0xFFFFFFFF;
        check(BlueprintContent.Key(left)!=BlueprintContent.Key(right),"background participates in deduplication");
    }
}
