using BetterBeads.Data;

internal static class DesignSwitchChecks
{
    public static void Run(Action<bool,string> check)
    {
        var source=SimpleCrafting.Blank(SimpleCrafting.Picture16);
        source.Name="原作品";source.Revision=3;
        source.Views["front"].Cells[0]=new(){ColorId="corner",Rgba=0x123456FF,MaterialId="decoration"};
        source.Views["front"].Cells[8*16+8]=new(){ColorId="center",Rgba=0xABCDEFff,MaterialId="decoration"};
        var grown=SimpleDesignConversion.Convert(source,SimpleCrafting.Picture32,out int grownLoss);
        check(grownLoss==0&&grown.Id==source.Id&&grown.Revision==3&&grown.Name==source.Name
            &&grown.Views["front"].Cells[8*32+8]?.Rgba==0x123456FF
            &&grown.Views["front"].Cells[16*32+16]?.Rgba==0xABCDEFFF
            &&source.Views["front"].Width==16,"growing canvas preserves centered colors and identity");
        var ornament=SimpleDesignConversion.Convert(grown,SimpleCrafting.LargeOrnament,out int ornamentLoss);
        check(ornamentLoss==0&&ornament.Views["front"].Cells[8*32+8]?.Rgba==0x123456FF
            &&ornament.Views["front"].Cells[8*32+8]?.MaterialId=="decoration","same-size category switch retains canvas");
        var weapon=SimpleDesignConversion.Convert(ornament,SimpleCrafting.Sword,out int weaponLoss);
        check(weaponLoss==0&&weapon.Views["front"].Cells[0]?.Rgba==0x123456FF
            &&weapon.Views["front"].Cells[8*16+8]?.MaterialId=="copper"
            &&weapon.SupplementaryMaterials.TryGetValue("copper",out int count)&&count==0,"ornament to weapon keeps centered artwork and sets metal");
        var iron=weapon.Copy();iron.SupplementaryMaterials=new(){{"iron",0}};
        foreach(var bead in iron.Views["front"].Cells.Where(c=>c is not null))bead!.MaterialId="iron";
        var dagger=SimpleDesignConversion.Convert(iron,SimpleCrafting.Dagger,out int daggerLoss);
        check(daggerLoss==0&&dagger.Views["front"].Cells[0]?.MaterialId=="iron"
            &&SimpleCrafting.Metal(dagger)=="iron","weapon variant keeps selected metal");
        grown.Views["front"].Cells[0]=new(){ColorId="edge",Rgba=0x010203FF,MaterialId="decoration"};
        var shrunk=SimpleDesignConversion.Convert(grown,SimpleCrafting.Picture16,out int clipped);
        check(clipped==1&&shrunk.Views["front"].Cells[0]?.Rgba==0x123456FF
            &&shrunk.Views["front"].Cells[8*16+8]?.Rgba==0xABCDEFFF,"shrinking retains center and reports clipped edge beads");
        var document=new EditorDocument(source,SimpleCrafting.Catalog(),"front");
        check(document.ApplyCandidate(grown)&&document.CanUndo&&document.IsDirty
            &&document.ViewSize.Width==32,"conversion is one dirty undoable edit");
        check(document.Undo()&&document.ViewSize.Width==16&&document.Snapshot().Views["front"].Cells[0]?.Rgba==0x123456FF
            &&document.Redo()&&document.ViewSize.Width==32,"undo and redo restore whole canvas conversion");
    }
}
