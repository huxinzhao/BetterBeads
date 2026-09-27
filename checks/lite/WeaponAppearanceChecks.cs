using BetterBeads.Data;
using System.Text.Json.Nodes;

internal static class WeaponAppearanceChecks
{
    public static void Run(Action<bool,string> check)
    {
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        foreach(string id in DefaultWeapons.Templates().Select(t=>t.Id))
        {
            var source=SimpleCrafting.Blank(id);source.Name="星光";
            if(source.Use==ProductUse.Sword)source.SwordOrientation=SwordOrientation.Vertical;
            source.Views["front"].Cells[10]=new BeadCell{ColorId="free",Rgba=0xAABBCCFF,MaterialId="iridium"};
            source.SelectedEffects.Add("ruby");source.SupplementaryMaterials["ruby"]=5;
            var appearance=WeaponAppearance.Create("(W)4","(W)"+id,source);
            string raw=DesignStorage.Serialize(appearance);
            check(WeaponAppearance.TryRead(raw,out var saved),"appearance round trip "+id);
            check(saved!.Pattern!.SelectedEffects.Count==0&&saved.Pattern.SupplementaryMaterials.Count==0,"only artwork copied "+id);
            check(saved.Pattern.Name=="星光"&&saved.Pattern.Views["front"].Cells[10]!.Rgba==0xAABBCCFF,"name and exact pixels retained "+id);
            check(saved.Pattern.SwordOrientation==source.SwordOrientation,"visual orientation retained "+id);
            source.Views["front"].Cells[10]!.Rgba=0;source.Name="changed";
            check(appearance.Pattern!.Name=="星光"&&appearance.Pattern.Views["front"].Cells[10]!.Rgba==0xAABBCCFF,"source edits detached "+id);
            check(saved.AppliesTo("(W)4","(W)"+id)&&!saved.AppliesTo("(W)62","(W)"+id)&&!saved.AppliesTo("(W)4",null),"base upgrade and native reset invalidate "+id);
            foreach(string field in new[]{"SchemaVersion","BaseItemId","DrawnItemId","Pattern"})
            {
                var node=JsonNode.Parse(raw)!;
                node[field]=field=="SchemaVersion"?JsonValue.Create(99):field=="Pattern"?null:JsonValue.Create("invalid");
                check(!WeaponAppearance.TryRead(node.ToJsonString(),out _),"invalid record rejected "+field+id);
            }
            var bad=JsonNode.Parse(raw)!;bad["Pattern"]!["Views"]!["front"]!["Width"]=source.Views["front"].Width+1;
            check(!WeaponAppearance.TryRead(bad.ToJsonString(),out _),"mismatched weapon dimensions rejected "+id);
        }
        var native=WeaponAppearance.Create("(W)"+SimpleCrafting.Sword,"(W)4",null);
        check(WeaponAppearance.TryRead(DesignStorage.Serialize(native),out var n)&&n!.Pattern is null,"native skin record needs no pattern");
        foreach(string invalid in new[]{"","{broken","null",new string(' ',256_001)})check(!WeaponAppearance.TryRead(invalid,out _),"invalid payload safe");
        int layouts=0;var font=new object();
        var layout=new LastTextLayout<object>((text,_,width)=>{layouts++;return text+width;});
        for(int i=0;i<600;i++)layout.Get("confirm",font,320);
        check(layouts==1,"600 unchanged descriptions laid out once");
        layout.Get("changed",font,320);check(layouts==2,"translated text change refreshes layout");
        font=new object();layout.Get("changed",font,320);check(layouts==3,"font replacement refreshes layout");
        layout.Get("changed",font,300);check(layouts==4,"width change refreshes layout");
        layout.Clear();layout.Get("changed",font,300);check(layouts==5,"session clear refreshes layout");
        int attempts=0;
        var retry=new LastTextLayout<object>((text,_,_)=>++attempts==1?throw new InvalidOperationException():text);
        try{retry.Get("retry",font,320);}catch(InvalidOperationException){}
        check(retry.Get("retry",font,320)=="retry"&&attempts==2,"failed layout can be retried");
    }
}
