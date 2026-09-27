using BetterBeads.Data;
using System.Xml.Linq;

internal static class ShareChecks
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(string template in new[]{SimpleCrafting.Picture16,SimpleCrafting.Picture32,SimpleCrafting.Ornament,
            SimpleCrafting.LargeOrnament,SimpleCrafting.Sword,SimpleCrafting.Dagger,SimpleCrafting.Hammer})
        {
            var original=SimpleCrafting.Blank(template);original.Name="Secret save name";original.Revision=9;
            var grid=original.Views["front"];
            string metal=SimpleCrafting.IsWeapon(original.Use)?"iridium":"decoration";
            if(SimpleCrafting.IsWeapon(original.Use)){original.SupplementaryMaterials.Clear();original.SupplementaryMaterials[metal]=0;}
            grid.Cells[0]=new(){Rgba=0xFF2255FF,ColorId="red",MaterialId=metal};
            grid.Cells[^1]=new(){Rgba=0x112233FF,ColorId="blue",MaterialId=metal};
            string code=BlueprintSharing.Encode(original);
            check(code.StartsWith("BB1.")&&BlueprintSharing.TryDecode(code,out var imported)&&imported is not null,"share roundtrip "+template);
            BlueprintSharing.TryDecode(code,out var copy);
            check(copy!.Id!=original.Id&&copy.Name==""&&copy.Revision==0&&copy.TemplateId==template
                &&copy.Views["front"].Cells[0]!.Rgba==0xFF2255FF&&copy.Views["front"].Cells[^1]!.Rgba==0x112233FF
                &&copy.Views["front"].Cells[1] is null&&(!SimpleCrafting.IsWeapon(copy.Use)||SimpleCrafting.Metal(copy)=="iridium"),
                "share preserves artwork without save identity "+template);
            check(!code.Contains(original.Name,StringComparison.Ordinal),"share omits save name "+template);
            int changeAt=12;string changed=code[..changeAt]+(code[changeAt]=='A'?'B':'A')+code[(changeAt+1)..];
            check(!BlueprintSharing.TryDecode(changed,out _),"share detects changed bytes "+template);
            string svg=BlueprintColorChart.Create(original,MardPaletteReference.Current);
            var xml=XDocument.Parse(svg);
            check(xml.Root?.Name.LocalName=="svg"&&svg.Contains("#FF2255")&&svg.Contains("#112233")
                &&svg.Contains("MARD 221")&&svg.Contains(">1</text>")&&svg.Contains(">2</text>"),"color chart has exact swatches and references "+template);
        }
        check(!BlueprintSharing.TryDecode("BB1.bad",out _)&&!BlueprintSharing.TryDecode("other",out _),"reject invalid share code");
        foreach(var (width,height) in new[]{(480,420),(1280,720),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(width,height,32);
            var editor=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
            var library=LiteLibraryLayout.Calculate(LibraryLayout.LiteArea(editor));
            check(library.Actions.Length==4&&library.Actions.All(r=>r.Width>=40&&r.Height>=40)
                &&library.Actions.Zip(library.Actions.Skip(1)).All(pair=>!pair.First.Overlaps(pair.Second)),
                "four library actions fit "+width);
            var share=LibraryShareLayout.Calculate(editor.Frame);
            check(editor.Frame.Contains(share.Dialog)&&share.Dialog.Contains(share.Close)
                &&new[]{share.Copy,share.Chart,share.Paste,share.Note}.All(share.Dialog.Contains)
                &&!share.Copy.Overlaps(share.Chart)&&!share.Chart.Overlaps(share.Paste)&&!share.Paste.Overlaps(share.Note),
                "share dialog fits "+width);
        }
    }
}
