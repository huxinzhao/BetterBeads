using BetterBeads.Data;
using System.Text.Json;

internal static class PlayerExperienceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var catalog=SimpleCrafting.Catalog();var design=SimpleCrafting.Blank(SimpleCrafting.Picture16);
        design.Revision=2;design.Name="Saved source";
        var grid=design.Views["front"];
        BeadCell Bead(uint rgba)=>new(){ColorId="test",Rgba=rgba,MaterialId="decoration"};
        grid.Cells[2*16+2]=Bead(0x123456ff);grid.Cells[2*16+3]=Bead(0xffaabbff);
        grid.Cells[3*16+2]=Bead(0x998877ff);grid.Cells[2*16+5]=Bead(0x555555ff);
        var region=new UiRect(2,2,2,2);var doc=new EditorDocument(design,catalog,"front");
        check(PatternTransform.Overwritten(grid,region,2,0,false)==1,"move preview counts overwritten destination beads");
        var preview=design.Copy();PatternTransform.Apply(preview.Views["front"],region,2,0,false);
        check(grid.Cells[34] is not null&&preview.Views["front"].Cells[34] is null,"preview is independent of the original artwork");
        check(doc.TransformRegion(region,2,0,false)&&doc.IsDirty&&doc.CanUndo,"move is an unsaved undoable edit");
        var moved=doc.Snapshot();
        check(moved.Id==design.Id&&moved.Revision==2&&moved.Name==design.Name,"move preserves identity, revision and name");
        check(moved.Views["front"].Cells[36]!.Rgba==0x123456ff&&moved.Views["front"].Cells[37]!.Rgba==0xffaabbff,
            "move preserves exact colors and bead positions relative to the selection");
        check(doc.Undo()&&!doc.IsDirty&&!doc.CanUndo&&doc.Snapshot().Views["front"].Cells[37]!.Rgba==0x555555ff,"one undo restores both source and overwritten destination");
        check(doc.Redo()&&doc.Snapshot().Views["front"].Cells[34] is null,"redo restores the entire move");
        doc.Undo();long version=doc.ChangeVersion;
        check(!doc.TransformRegion(region,0,0,true)&&doc.ChangeVersion==version&&!doc.CanUndo,"same-position copy is not a spurious undo step");
        check(!doc.TransformRegion(region,int.MaxValue,0,false)&&doc.ChangeVersion==version,"overflowing destination is rejected without modifications");
        check(!doc.TransformRegion(new(-1,0,2,2),1,0,false),"invalid source selection is rejected");
        check(doc.TransformRegion(region,5,5,true),"copy applies to a valid destination");
        var copied=doc.Snapshot();
        check(copied.Views["front"].Cells[34] is not null&&copied.Views["front"].Cells[7*16+7]!.Rgba==0x123456ff,
            "copy retains source and writes destination");
        copied.Views["front"].Cells[7*16+7]!.Rgba=1;
        check(doc.Snapshot().Views["front"].Cells[34]!.Rgba==0x123456ff,"source and destination remain independent snapshots");
        var holes=design.Copy();holes.Views["front"].Cells[6*16+6]=Bead(0xabcdefFF);
        PatternTransform.Apply(holes.Views["front"],region,3,3,true);
        check(holes.Views["front"].Cells[6*16+6]!.Rgba==0xabcdefFF,"empty selected cells do not erase existing destination beads");
        var recovery=doc.RecoverySnapshot();
        check(recovery.Valid,"changed saved artwork produces a valid recovery record");
        var restored=JsonSerializer.Deserialize<DraftRecoveryEntry>(DesignStorage.Serialize(recovery))!;
        var fresh=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),catalog,"front");
        check(fresh.RestoreRecovery(restored)&&fresh.IsDirty&&!fresh.CanUndo,"recovery restores an unsaved draft without inventing old undo history");
        check(fresh.Snapshot().Id==design.Id&&fresh.Snapshot().Revision==2,"saved identity and revision survive recovery for conflict checking");
        restored.Draft.Name="External mutation";
        check(fresh.Snapshot().Name==design.Name,"restored artwork is independent of the recovery object");
        check(!fresh.RestoreRecovery(recovery),"recovery cannot overwrite an already dirty current draft");
        fresh.DiscardChanges();
        check(!fresh.IsDirty&&fresh.Snapshot().Views["front"].Cells[7*16+7] is null,"discard after recovery restores the saved baseline");
        var imported=SimpleCrafting.Blank(SimpleCrafting.Sword32);imported.Views["front"].Cells[500]=new(){ColorId="test",Rgba=0x112233ff,MaterialId="copper"};
        fresh.TryOpenImported(imported);var importedRecovery=fresh.RecoverySnapshot();
        check(importedRecovery.Valid&&importedRecovery.UnsavedIdentity,"unnamed imported artwork is recoverable without saving to the library");
        var recoveredImport=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),catalog,"front");
        check(recoveredImport.RestoreRecovery(importedRecovery)&&recoveredImport.IsDirty&&recoveredImport.ViewSize==(32,32),"import recovery retains dimensions and unsaved identity");
        importedRecovery.Version=99;
        check(!new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),catalog,"front").RestoreRecovery(importedRecovery),"unknown recovery format does not replace the current work");
        check(GiftFeedback.Key(GallerySiteState.Ready,5)=="gift.display-hearts","five-heart feedback does not promise next-day display");
        check(GiftFeedback.Key(GallerySiteState.Ready,6)=="gift.display-tomorrow","six-heart feedback reflects next-day eligibility");
        check(GiftFeedback.Key(GallerySiteState.Unsupported,10)=="gift.display-unsupported","high friendship does not override unsupported housing");
        check(GiftFeedback.Key(GallerySiteState.Unavailable,10)=="gift.display-unavailable","unavailable wall feedback is distinct from unsupported housing");
        var layouts=new List<object>();
        foreach(var size in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(size.Item1,size.Item2,40);var editor=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
            var p=PatternMoveLayout.Calculate(editor.Frame);var buttons=new[]{p.Up,p.Left,p.Down,p.Right,p.Mode,p.Apply,p.Cancel};
            check(buttons.All(r=>p.Dialog.Contains(r)&&r.Width>=44&&r.Height>=44),"move buttons retain minimum sizes and fit "+size);
            check(buttons.SelectMany((a,i)=>buttons.Skip(i+1).Select(b=>(a,b))).All(pair=>!Overlap(pair.a,pair.b))
                &&!Overlap(p.Preview,p.Description)&&p.Description.Bottom<=p.Up.Y,"preview, text and controls do not overlap "+size);
            var scene=ScenePreviewLayout.Calculate(editor.Frame);
            layouts.Add(new{Width=size.Item1,Height=size.Item2,Scale=scale,Move=p,Recovery=RecoveryLayout.Calculate(editor.Frame),Scene=scene,Explanation=WeaponExplanationLayout.Calculate(scene)});
        }
        foreach(var use in new[]{ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer})foreach(int size in new[]{16,24,32})
        {
            var d=GeometryChecks.Shape(use,size,(x,y)=>x>=size/2-1&&x<=size/2&&y>=3&&y<=size-3||x==0&&y==0);
            var analysis=WeaponGeometry.Evaluate(d);var main=WeaponGeometry.MainComponent(d.Views["front"],use,d.SwordOrientation);
            check(main.Count==analysis.Geometry.MainCount&&!main.Contains(0),"explanation uses the same main component as actual stats "+use+size);
        }
        using var zh=JsonDocument.Parse(File.ReadAllText("BetterBeads/i18n/zh.json"));using var en=JsonDocument.Parse(File.ReadAllText("BetterBeads/i18n/default.json"));
        var keys=File.ReadAllLines("docs/RC4_PLAYER_COPY.txt").Where(l=>l.StartsWith('[')&&l.EndsWith(']')).Select(l=>l[1..^1]).ToArray();
        check(keys.Length==37&&keys.Distinct().Count()==keys.Length&&keys.All(k=>zh.RootElement.TryGetProperty(k,out _)&&en.RootElement.TryGetProperty(k,out _)),"editable copy document includes every new Chinese and English key");
        Directory.CreateDirectory("art/player-experience-1.1.0-RC4");
        File.WriteAllText("art/player-experience-1.1.0-RC4/layouts.json",JsonSerializer.Serialize(layouts));
        File.WriteAllText("art/player-experience-1.1.0-RC4/pattern.json",DesignStorage.Serialize(design));
        var example=GeometryChecks.Shape(ProductUse.Sword,32,(x,y)=>x>=14&&x<=16&&y>=7&&y<=29||x==28&&y==5);
        File.WriteAllText("art/player-experience-1.1.0-RC4/shape.json",DesignStorage.Serialize(new{Design=example,Shape=WeaponGeometry.Evaluate(example),Main=WeaponGeometry.MainComponent(example.Views["front"],example.Use,example.SwordOrientation)}));
    }
    private static bool Overlap(UiRect a,UiRect b)=>a.X<b.Right&&b.X<a.Right&&a.Y<b.Bottom&&b.Y<a.Bottom;
}
