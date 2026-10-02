using BetterBeads.Data;
using System.Text.Json;

internal static class CreativeChecks
{
    internal static void Run(Action<bool,string> check)
    {
        FurnitureTemplates.Configure(DefaultManufacturing.Furniture().Concat(SimpleCrafting.Frames()).Concat(SimpleCrafting.Ornaments()).Concat(FurnitureFinish.Definitions()));
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        var d=SimpleCrafting.Blank(SimpleCrafting.Picture16);d.Name="source";d.Revision=3;
        var g=d.Views["front"];
        g.Cells[0]=new(){ColorId="shared",Rgba=0x123456FF,MaterialId="decoration"};
        g.Cells[1]=new(){ColorId="other-name",Rgba=0x123456FF,MaterialId="decoration"};
        g.Cells[2]=new(){ColorId="shared",Rgba=0xABCDEFff,MaterialId="decoration"};
        var colors=ArtworkColors.Read(d);
        check(colors.Length==2&&colors[0].Count==2&&colors[0].Rgba==0x123456FF,"group actual RGBA, not color identifiers");
        var candidate=ArtworkColors.Replace(d,0x123456FF,0xFFEEDDff);
        check(g.Cells[0]!.Rgba==0x123456FF&&candidate.Id==d.Id&&candidate.Revision==3,"replacement preview is detached and identity preserving");
        check(candidate.Views["front"].Cells[0]!.Rgba==0xFFEEDDff&&candidate.Views["front"].Cells[1]!.Rgba==0xFFEEDDff
            &&candidate.Views["front"].Cells[2]!.Rgba==0xABCDEFFF,"same name different color is not replaced; different names same color are");
        check(candidate.Views["front"].Cells[0]!.MaterialId=="decoration"&&candidate.Views["front"].Cells[3] is null,"replacement keeps material and empty cells");
        var doc=new EditorDocument(d,SimpleCrafting.Catalog(),"front");
        check(!doc.ReplaceExactColor(0x123456FF,0x123456FF)&&!doc.CanUndo&&!doc.IsDirty,"identical replacement is not an edit");
        check(!doc.ReplaceExactColor(0x000000FF,0xFFFFFFFF)&&!doc.CanUndo,"absent color is not an edit");
        check(doc.ReplaceExactColor(0x123456FF,0xFFEEDDff)&&doc.IsDirty&&doc.CanUndo,"replacement is one edit");
        check(doc.Undo()&&!doc.CanUndo&&doc.Snapshot().Views["front"].Cells[0]!.Rgba==0x123456FF,"one undo restores every matched bead");
        check(doc.Redo()&&doc.Snapshot().Views["front"].Cells[1]!.Rgba==0xFFEEDDff,"redo reapplies exact replacement");
        for(int i=3;i<44;i++)g.Cells[i]=new(){ColorId="shared",Rgba=((uint)i<<8)|255,MaterialId="decoration"};
        colors=ArtworkColors.Read(d);
        check(colors.Length==43&&colors.Skip(1).Zip(colors.Skip(2)).All(p=>p.First.Rgba<p.Second.Rgba),"more than twenty colors sort stably by count then RGBA");
        int reads=0;Blueprint Read(){reads++;return d.Copy();}
        var cache=new ArtworkPaletteCache();var cached=cache.Colors(1,Read);var preview=cache.Preview(1,Read,0x123456FF,0xFFFFFFFF);
        bool reused=true;for(int i=0;i<600;i++)reused&=ReferenceEquals(cached,cache.Colors(1,Read))&&ReferenceEquals(preview,cache.Preview(1,Read,0x123456FF,0xFFFFFFFF));
        check(reused&&reads==1,"600 static frames reuse artwork colors and replacement preview");
        check(!ReferenceEquals(preview,cache.Preview(1,Read,0x123456FF,0xFFFF00FF))&&reads==1,"new target rebuilds preview without rereading source");
        cache.Colors(2,Read);check(reads==2,"document revision refreshes colors");
        cache.Clear();cache.Colors(2,Read);check(reads==3,"closing palette releases source and candidate");

        var progress=new SaveProgress();var repo=new BlueprintRepository(progress);
        var original=d.Copy(true);original.Name="original";check(repo.Save(original),"saved source fixture");
        var copy=ArtworkColors.CopyDraft(original);var other=ArtworkColors.CopyDraft(original);
        check(copy.Id!=original.Id&&copy.Id!=other.Id&&copy.Revision==0&&copy.Name==""&&progress.BlueprintRecords.Count==1,"copy creates unnamed independent unsaved identity only");
        var blank=SimpleCrafting.Blank(SimpleCrafting.Picture16);var editor=new EditorDocument(blank,SimpleCrafting.Catalog(),"front");
        check(editor.TryOpenImported(copy)&&editor.IsDirty&&!editor.TryOpen(original),"copied draft blocks unconfirmed switching");
        editor.DiscardChanges();check(!editor.IsDirty&&progress.BlueprintRecords.Count==1,"discarding copy leaves library intact");
        check(editor.TryOpenImported(ArtworkColors.CopyDraft(blank))&&editor.IsDirty,"even empty copies remain explicitly unsaved");
        editor.DiscardChanges();editor.TryOpenImported(copy);editor.Rename("variant");
        var save=BlueprintAutoSave.Prepare(progress,editor.Snapshot())!;save.Apply(progress);editor.MarkSaved(save.Saved.Revision);
        check(!editor.IsDirty&&progress.BlueprintRecords.Count==2&&repo.TryOpen(original.Id,out var unchanged)&&unchanged!.Name=="original","first named save adds copy without changing source");
        editor.TryOpen(original);editor.ReplaceExactColor(0x123456FF,0xFFFFFFFF);
        var concurrent=original.Copy();concurrent.Name="changed elsewhere";repo.Save(concurrent);
        check(BlueprintAutoSave.Prepare(progress,editor.Snapshot()) is null&&editor.IsDirty,"save conflict preserves draft and blocks switching");

        var old=JsonSerializer.Deserialize<SaveProgress>("{}")!;LibraryPreferences.Normalize(old);
        check(old.FavoriteBlueprintIds.Count==0&&old.RecentBlueprintIds.Count==0,"legacy progress defaults optional preferences");
        old.FavoriteBlueprintIds=null!;old.RecentBlueprintIds=new(){"a","a","",null!,"b"};LibraryPreferences.Normalize(old);
        check(old.RecentBlueprintIds.SequenceEqual(new[]{"a","b"}),"malformed optional preference collection normalized");
        for(int i=0;i<110;i++)LibraryPreferences.Touch(old,i.ToString());LibraryPreferences.Touch(old,"50");
        check(old.RecentBlueprintIds.Count==100&&old.RecentBlueprintIds[0]=="50"&&old.RecentBlueprintIds.Distinct().Count()==100,"recent entries bounded and move to front");
        var a=SimpleCrafting.Blank(SimpleCrafting.Picture16);a.Name="A";
        var b=SimpleCrafting.Blank(SimpleCrafting.Sword);b.Name="B";
        var c=SimpleCrafting.Blank(SimpleCrafting.Hammer);c.Name="C";
        var list=new[]{c,a,b};var prefs=new SaveProgress();LibraryPreferences.Touch(prefs,b.Id);prefs.FavoriteBlueprintIds.Add(c.Id);
        Blueprint[] Query(ProductUse? use,bool favorites,bool recent)=>LibraryPreferences.Query(list,prefs,x=>x.Id,x=>x,use,favorites,recent).ToArray();
        check(Query(null,false,true).SequenceEqual(new[]{b,a,c}),"recent first and unseen alphabetical fallback");
        check(Query(null,false,false).SequenceEqual(new[]{a,b,c}),"name sort");
        check(Query(ProductUse.Sword,false,true).SequenceEqual(new[]{b})&&Query(null,true,true).SequenceEqual(new[]{c}),"kind and favorite filtering");
        LibraryPreferences.Remove(prefs,c.Id);LibraryPreferences.Remove(prefs,b.Id);
        check(prefs.FavoriteBlueprintIds.Count==0&&prefs.RecentBlueprintIds.Count==0,"deletion cleans preference references");
        prefs.FavoriteBlueprintIds.Add("builtin:chicken");LibraryPreferences.Touch(prefs,"builtin:chicken");
        var loaded=JsonSerializer.Deserialize<SaveProgress>(JsonSerializer.Serialize(prefs))!;
        check(loaded.FavoriteBlueprintIds.Contains("builtin:chicken")&&loaded.RecentBlueprintIds[0]=="builtin:chicken","template preferences round trip without blueprint changes");

        foreach(int size in new[]{16,32})
        {
            var picture=SimpleCrafting.Blank(size==16?SimpleCrafting.Picture16:SimpleCrafting.Picture32);picture.Views["front"].Cells[8*size+8]=g.Cells[0]!.Copy();
            var art=SceneProduct.Compose(picture);var fp=SceneProduct.Footprint(picture);
            check(art.Width==size&&art.Height==size&&art.Cells[8*art.Width+8]!.Rgba==0x123456FF&&fp==(size/16,size/16),"compact painting frame and native footprint "+size);
        }
        foreach(var (w,h) in new[]{(16,16),(17,16),(16,17),(17,17)})
        {
            var ornament=SimpleCrafting.Blank(SimpleCrafting.LargeOrnament);var grid=ornament.Views["front"];
            grid.Cells[0]=g.Cells[0]!.Copy();grid.Cells[(h-1)*32+w-1]=g.Cells[1]!.Copy();
            var scene=SceneProduct.Compose(ornament);var fp=SceneProduct.Footprint(ornament);
            var actual=ProductTemplates.CreateAtlas(SceneProduct.Snapshot(ornament));
            check(scene.Width==fp.Width*16&&scene.Height==actual.Height&&fp==((w+15)/16,1)
                &&scene.Cells.Select(x=>x?.Rgba).SequenceEqual(actual.Cells.Select(x=>x?.Rgba)),"scene uses actual ornament atlas and footprint "+(w,h));
        }
        foreach(string id in new[]{SimpleCrafting.Sword,SimpleCrafting.Dagger,SimpleCrafting.Hammer})
        {var weapon=SimpleCrafting.Blank(id);weapon.Views["front"].Cells[7]=g.Cells[0]!.Copy();check(SceneProduct.Compose(weapon).Cells[7]!.Rgba==0x123456FF,"weapon pattern position preserved "+id);}

        var layouts=new List<object>();
        foreach(var (width,height) in new[]{(480,420),(640,480),(1280,720),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(width,height,32);var main=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
            var dye=CreativeDialogLayout.Calculate(main.Frame);var scene=ScenePreviewLayout.Calculate(main.Frame);
            var library=LiteLibraryLayout.Calculate(LibraryLayout.LiteArea(main));var filter=LibraryFilterLayout.Calculate(main.Frame);
            check(main.Frame.Contains(dye.Dialog)&&dye.Body.Contains(dye.Left)&&dye.Body.Contains(dye.Right)&&!dye.Left.Overlaps(dye.Right),"palette columns inside modal "+width);
            check(dye.Cells.Length==20&&dye.Cells.All(r=>dye.Left.Contains(r)&&r.Width>=28)&&dye.Cells.Zip(dye.Cells.Skip(1)).All(p=>!p.First.Overlaps(p.Second)),"twenty usable non-overlapping color cells "+width);
            check(dye.Body.Bottom+8<=dye.Back.Y&&!dye.Picker.Hue.Overlaps(dye.Left)&&dye.Dialog.Contains(dye.Picker.Hue)
                &&!dye.CommonTab.Overlaps(dye.WorkTab)&&!dye.WorkTab.Overlaps(dye.Close),"gradient tabs footer clearances "+width);
            check(new[]{dye.Back,dye.Next,dye.Apply,dye.CommonTab,dye.WorkTab,dye.Close}.All(r=>dye.Dialog.Contains(r)&&r.Height>=40),"palette button bounds and minimum sizes "+width);
            check(scene.Dialog.Contains(scene.Stage)&&scene.Stage.Bottom+8<=scene.Info.Y&&scene.Note.Bottom+8<=scene.Toggle.Y&&scene.Toggle.Height>=40,"scene layout bounds and spacing "+width);
            check(library.Row(library.Count-1).Bottom+8<=library.Previous.Y&&library.Rows.Y>=library.Filter.Bottom+8&&library.Actions.All(r=>main.Frame.Contains(r)&&r.Height>=40),"library controls and rows do not overlap "+width);
            check(filter.Categories[^1].Bottom+8<=filter.Favorites.Y&&filter.Dialog.Contains(filter.Done),$"filter overlay clears all controls {width}: last={filter.Categories[^1]}, fav={filter.Favorites}, dialog={filter.Dialog}");
            check(dye.Cells.All(r=>!r.Overlaps(dye.Picker.SaturationValue)&&!r.Overlaps(dye.Picker.Hue))
                &&dye.Cells[^1].Bottom+8<=dye.PreviousPage.Y,"color scheme remains visible and selectable beside picker "+width);
            check(dye.Dialog.Contains(dye.BeforeCard)&&dye.Dialog.Contains(dye.AfterCard)&&dye.Dialog.Contains(dye.Preview)
                &&!dye.BeforeCard.Overlaps(dye.AfterCard)&&dye.AfterCard.Bottom+8<=dye.Apply.Y,"before after and preview clear footer "+width);
            check(dye.Back.Y==dye.Next.Y&&dye.Next.Y==dye.Apply.Y&&!dye.Back.Overlaps(dye.Next)&&!dye.Next.Overlaps(dye.Apply),"single aligned action row "+width);
            var opening=LibraryOpenLayout.Calculate(main.Frame);
            check(library.Actions.Length==4&&main.Frame.Contains(opening.Dialog)&&!opening.Edit.Overlaps(opening.Copy)&&!opening.Copy.Overlaps(opening.Cancel),"one open entrance with explicit save destination "+width);
            layouts.Add(new{Open=opening,Width=width,Height=height,Scale=scale,Main=main,Dye=dye,Scene=scene,Library=library,Filter=filter});
        }
        Directory.CreateDirectory("art/creative-0.16.26");File.WriteAllText("art/creative-0.16.26/layouts.json",JsonSerializer.Serialize(layouts));
        var controls=new FrameControls();int mainClicks=0,modalClicks=0;controls.Add((new(0,0,100,100),()=>mainClicks++));controls.Clear();controls.Add((new(20,20,40,40),()=>modalClicks++));
        check(!controls.TryInvoke(5,5)&&controls.TryInvoke(25,25)&&mainClicks==0&&modalClicks==1&&!controls.TryInvoke(25,25),"modal control replacement prevents gaps and duplicate activation");
    }
}
