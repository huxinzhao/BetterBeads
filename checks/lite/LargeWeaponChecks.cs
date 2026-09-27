using BetterBeads.Data;

internal static class LargeWeaponChecks
{
    internal static void Run(Action<bool,string> check)
    {
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        var catalog=SimpleCrafting.Catalog();
        foreach(var use in new[]{ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer})
        foreach(int size in new[]{16,24,32})
        foreach(string metal in SimpleCrafting.Metals)
        {
            string id=SimpleCrafting.WeaponId(use,size);
            var design=SimpleCrafting.Blank(id);
            design.SupplementaryMaterials.Clear();design.SupplementaryMaterials[metal]=0;
            design.Views["front"].Cells[0]=new(){Rgba=0xAA7733FF,ColorId="brown",MaterialId=metal};
            int baseCost=use switch{ProductUse.Dagger=>3,ProductUse.Hammer=>6,_=>4};
            int expectedCost=(int)Math.Ceiling(baseCost*size/16d);
            var cost=SimpleCrafting.Cost(design);
            check(SimpleCrafting.Supported(design)&&design.Views["front"].Width==size&&cost.Count==expectedCost,
                $"large weapon template and fee {use} {size} {metal}");
            var expected=SimpleCrafting.Stats(use,metal);
            var stats=SimpleCrafting.Stats(design);
            double multiplier=size==24?1.1:size==32?1.2:1;
            check(stats.MinDamage==(int)Math.Round(expected.MinDamage*multiplier,MidpointRounding.AwayFromZero)
                &&stats.MaxDamage==(int)Math.Round(expected.MaxDamage*multiplier,MidpointRounding.AwayFromZero)
                &&stats.Speed==expected.Speed
                &&stats.Knockback==expected.Knockback&&stats.CritChance==expected.CritChance,
                $"large weapon stats {use} {size} {metal}");
            var recipe=SimpleCrafting.Recipes().First(r=>r.Template.Id==id);
            var inventory=new InventorySlot?[]{new("bars",cost.Item,expectedCost,999,RawMaterial:metal,Yield:1),null};
            var preview=SimpleCrafting.Evaluate(design,recipe,catalog,inventory,0,"large-weapon",false,true,2,"");
            check(preview.Plan is not null&&preview.Plan.Product.FinalStats.GetValueOrDefault("reachScale",1)==.5
                &&preview.Plan.Product.ActualMaterials[cost.Item]==expectedCost,
                $"frozen reach and actual materials {use} {size} {metal}");
            check(SimpleCrafting.SaleValue(preview.Plan!.Product,_=>60)==expectedCost*120,
                $"large weapon sale price follows actual bars {use} {size} {metal}");
            string code=BlueprintSharing.Encode(design);
            check(BlueprintSharing.TryDecode(code,out var imported)&&imported!.TemplateId==id
                &&imported.Views["front"].Cells[0]!.Rgba==0xAA7733FF,
                $"large weapon share code {use} {size} {metal}");
        }
        var old=SimpleCrafting.Blank(SimpleCrafting.Sword);
        check(SimpleCrafting.ReachScale(old)==.5&&WeaponReach.Scale(100,100,20,20,100,100,1)==(100,100,20,20),
            "empty draft has minimum new range; legacy range transform stays available");
        foreach(var rect in new[]{(120,90,24,20),(56,90,24,20),(90,56,20,24),(90,120,20,24)})
        {
            var enlarged=WeaponReach.Scale(rect.Item1,rect.Item2,rect.Item3,rect.Item4,100,100,1.5);
            check(enlarged.Width>rect.Item3&&enlarged.Height>rect.Item4,"all four directions expand from wielder anchor");
            var doubled=WeaponReach.Scale(rect.Item1,rect.Item2,rect.Item3,rect.Item4,100,100,2);
            check(doubled.Width==rect.Item3*2&&doubled.Height==rect.Item4*2,"double range keeps original attack shape");
        }
        var source=SimpleCrafting.Blank(SimpleCrafting.Sword);
        source.Views["front"].Cells[0]=new(){Rgba=0xFFFFFFFF,ColorId="white",MaterialId="copper"};
        var converted=SimpleDesignConversion.Convert(source,SimpleCrafting.Sword24,out int clipped);
        check(clipped==0&&converted.Views["front"].Cells[4*24+4]?.Rgba==0xFFFFFFFF,
            "sixteen to twenty-four preserves center position");
        var back=SimpleDesignConversion.Convert(converted,SimpleCrafting.Sword,out clipped);
        check(clipped==0&&back.Views["front"].Cells[0]?.Rgba==0xFFFFFFFF,"size conversion is reversible for contained art");
        var picture=SimpleDesignConversion.Convert(converted,SimpleCrafting.Picture32,out clipped);
        check(clipped==0&&picture.Views["front"].Cells[8*32+8]?.Rgba==0xFFFFFFFF,
            "twenty-four pixel weapon centers in thirty-two pixel wall art");
        var editor=new EditorDocument(source,SimpleCrafting.Catalog(),"front");
        check(editor.ApplyCandidate(converted)&&editor.Undo()&&editor.ViewSize==(16,16)
            &&editor.Redo()&&editor.ViewSize==(24,24),"resizing is one undo step");
        var pixels=new uint[24*24];pixels[0]=0xAA7733FF;
        var importedImage=ImageBlueprintImport.Create(pixels,24,24,new(SimpleCrafting.Sword24,RestorePixels:false,Outline:false));
        check(importedImage.Design.Views["front"].Width==24&&importedImage.Design.Views["front"].Cells[0]?.Rgba==0xAA7733FF,
            "reference image preserves exact twenty-four pixel weapon");

        var vertical=SimpleCrafting.Blank(SimpleCrafting.Sword);
        for(int i=0;i<16;i++)vertical.Views["front"].Cells[(15-i)*16+i]=new(){Rgba=0xFFFFFFFF,ColorId="white",MaterialId="copper"};
        var diagonal=vertical.Copy();
        vertical.SwordOrientation=SwordOrientation.Vertical;
        check(SimpleCrafting.Supported(vertical)&&WeaponShape.ReachScale(vertical.Views["front"])==WeaponShape.ReachScale(diagonal.Views["front"]),
            "visual orientation does not alter measured sword length");
        foreach(int side in new[]{16,24,32})
        {
            var character=SimpleCrafting.Blank(SimpleCrafting.WeaponId(ProductUse.Sword,side));
            character.SwordOrientation=SwordOrientation.Vertical;
            character.Views["front"].Cells[0]=new(){Rgba=0x123456FF,ColorId="hair"};
            character.Views["front"].Cells[(side-1)*side+side/2]=new(){Rgba=0xABCDEF88,ColorId="boots"};
            var image=SceneProduct.Compose(character);
            var original=character.Views["front"];
            check(image.Width==side&&image.Height==side&&image.Cells.Count==original.Cells.Count
                &&image.Cells.Select(c=>(c?.Rgba,c?.ColorId,c?.MaterialId))
                    .SequenceEqual(original.Cells.Select(c=>(c?.Rgba,c?.ColorId,c?.MaterialId)))
                &&!ReferenceEquals(image,original),
                $"{side}-pixel character art remains byte-exact in static preview");
        }
        foreach(int side in new[]{16,24,32})
        foreach(bool flipH in new[]{false,true})foreach(bool flipV in new[]{false,true})
        foreach(float angle in new[]{-1.7671459f,-.5890486f,-5.105088f,.5890486f})
        foreach(var pivot in new[]{(1f,15f),(15f,15f),(0f,0f)})
        {
            var swing=WeaponShape.VerticalSwing(pivot.Item1,pivot.Item2,angle,4,side,flipH,flipV);
            check(Math.Abs(swing.Rotation-angle-MathF.PI/4)<.00001f,"all facings add clockwise 45 degrees");
            float hiltX=flipH?15:1,hiltY=flipV?1:15;
            float x=(hiltX-pivot.Item1)*4,y=(hiltY-pivot.Item2)*4;
            check(Math.Abs(swing.OffsetX-(x*MathF.Cos(angle)-y*MathF.Sin(angle)))<.0001f
                &&Math.Abs(swing.OffsetY-(x*MathF.Sin(angle)+y*MathF.Cos(angle)))<.0001f
                &&swing.OriginX==side/2f&&swing.OriginY==(flipV?0:side),
                "bottom center follows native hand anchor across size, mirror and pivot");
        }
        var npcPixels=new uint[16*32];
        for(int y=0;y<32;y++)for(int x=0;x<16;x++)npcPixels[y*16+x]=x is >2 and <13?0x123456FFu+(uint)y*0x01000000u:0;
        var npc=LiteTemplatePixels.Create("Sebastian",SimpleCrafting.Sword32,npcPixels,16,32,"copper",SwordOrientation.Vertical);
        check(npc.SwordOrientation==SwordOrientation.Vertical&&SimpleCrafting.Metal(npc)=="copper"
            &&SimpleCrafting.Cost(npc)==("(O)334",8),"standing character template is a copper vertical 32px sword");
        check(Enumerable.Range(0,1024).All(i=>(npc.Views["front"].Cells[i]?.Rgba??0)==
            (i%32>=8&&i%32<24?npcPixels[i/32*16+i%32-8]:0)),"16x32 sprite is centered without resampling");
        var npcCode=BlueprintSharing.Encode(npc);
        var npcRecipe=SimpleCrafting.Recipes().Single(r=>r.Template.Id==SimpleCrafting.Sword32);
        var npcMade=SimpleCrafting.Evaluate(npc,npcRecipe,catalog,
            new InventorySlot?[]{new("copper","(O)334",8,999,RawMaterial:"copper",Yield:1),null},0,"npc",false,true,2,"");
        check(npcMade.Plan is not null&&DesignStorage.TryReadSnapshot(DesignStorage.Serialize(npcMade.Plan.Product),out var storedNpc)
            &&storedNpc!.Design.SwordOrientation==SwordOrientation.Vertical,"crafted standing sword preserves mode after save reload");
        check(BlueprintSharing.TryDecode(npcCode,out var npcRoundTrip)&&npcRoundTrip!.SwordOrientation==SwordOrientation.Vertical
            &&npcRoundTrip.Views["front"].Cells.Select(c=>c?.Rgba).SequenceEqual(npc.Views["front"].Cells.Select(c=>c?.Rgba)),
            "standing character sharing preserves direction and pixels");
        var hidden=new SaveProgress();hidden.HiddenLiteTemplates.Add("builtin:sebastian");
        check(System.Text.Json.JsonSerializer.Deserialize<SaveProgress>(DesignStorage.Serialize(hidden))!
            .HiddenLiteTemplates.Contains("builtin:sebastian"),"Sebastian template hiding survives save roundtrip");
        check(DesignStorage.VisualKey(new ProductSnapshot{Design=vertical})
            !=DesignStorage.VisualKey(new ProductSnapshot{Design=diagonal}),"orientation invalidates visual cache");
        var editorVertical=new EditorDocument(diagonal,SimpleCrafting.Catalog(),"front");
        check(editorVertical.SetSwordOrientation(SwordOrientation.Vertical)&&editorVertical.Undo()
            &&editorVertical.Snapshot().SwordOrientation==SwordOrientation.Diagonal&&editorVertical.Redo()
            &&editorVertical.Snapshot().SwordOrientation==SwordOrientation.Vertical,
            "sword orientation is one undoable change");
        var resizedVertical=SimpleDesignConversion.Convert(vertical,SimpleCrafting.Sword24,out int verticalClipped);
        check(verticalClipped==0&&resizedVertical.SwordOrientation==SwordOrientation.Vertical
            &&SimpleCrafting.Supported(resizedVertical),"sword resizing preserves chosen direction");
        string verticalCode=BlueprintSharing.Encode(vertical);
        check(verticalCode.StartsWith("BB2.")&&BlueprintSharing.TryDecode(verticalCode,out var decoded)
            &&decoded?.SwordOrientation==SwordOrientation.Vertical&&BlueprintSharing.Encode(diagonal).StartsWith("BB1."),
            "new vertical code preserves orientation; diagonal code remains compatible");
        var dagger=SimpleCrafting.Blank(SimpleCrafting.Dagger);dagger.SwordOrientation=SwordOrientation.Vertical;
        check(!SimpleCrafting.Supported(dagger),"only swords may use vertical orientation");
        foreach(int count in new[]{1,64,65,256,257,576,577,1024})
        {
            var grid=SimpleCrafting.Blank(SimpleCrafting.Sword32).Views["front"];
            for(int i=0;i<count;i++)grid.Cells[i]=new(){Rgba=0xFFFFFFFF,ColorId="white",MaterialId="copper"};
            int penalty=count<=64?0:count<=256?1:count<=576?2:3;
            check(WeaponShape.SpeedPenalty(grid)==penalty,$"bead-count speed boundary {count}");
        }
        var connected=SimpleCrafting.Blank(SimpleCrafting.Sword32).Views["front"];
        for(int i=0;i<20;i++)connected.Cells[(31-i)*32+i]=new(){Rgba=0xFFFFFFFF,ColorId="white"};
        double mainLength=WeaponShape.ReachScale(connected);
        connected.Cells[31]=new(){Rgba=0xFFFFFFFF,ColorId="white"};
        check(WeaponShape.ReachScale(connected)==mainLength,"isolated decoration does not extend reach");
        var random=new Random(7321);
        for(int trial=0;trial<60;trial++)
        {
            var grid=SimpleCrafting.Blank(SimpleCrafting.Sword32).Views["front"];
            var positions=new HashSet<(int X,int Y)>();int x=16,y=16;
            for(int step=0;step<80;step++)
            {
                positions.Add((x,y));grid.Cells[y*32+x]=new(){Rgba=0xFFFFFFFF,ColorId="white"};
                x=Math.Clamp(x+random.Next(-1,2),0,31);y=Math.Clamp(y+random.Next(-1,2),0,31);
            }
            int farthest=0;
            foreach(var a in positions)foreach(var b in positions)
                farthest=Math.Max(farthest,(a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
            double expected=Math.Clamp((Math.Sqrt(farthest)+Math.Sqrt(2))/(16*Math.Sqrt(2)),.5,2);
            check(Math.Abs(WeaponShape.ReachScale(grid)-expected)<1e-9,$"connected silhouette diameter {trial}");
        }
        foreach(int facing in new[]{0,1,2,3})
        {
            var area=WeaponReach.ExtendForward(80,80,40,40,90,90,110,110,facing,1.5);
            check((facing is 0 or 2?area.Width==40:area.Height==40)
                &&(facing is 0 or 2?area.Height>40:area.Width>40),$"forward reach extends only along facing {facing}");
        }
        check(WeaponReach.ScaleCentered(0,0,384,384,.5)==(96,96,192,192)
            &&WeaponReach.ScaleCentered(0,0,384,384,2)==(-192,-192,768,768),
            "club special keeps one centered hit region scaled by new artwork reach");
    }
}
