using BetterBeads.Data;
using System.Text.Json;

internal static class BlueprintContentOptimizationChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var random=new Random(41607);
        foreach(var use in Enum.GetValues<ProductUse>())
        foreach(var orientation in Enum.GetValues<SwordOrientation>())
        {
            var source=Fixture(random,use,orientation);
            string before=DesignStorage.Serialize(source),expected=LegacyKey(source);
            check(BlueprintContent.Key(source)==expected,$"blueprint content key preserves all legacy fields: {use}/{orientation}");
            check(DesignStorage.Serialize(source)==before,"content comparison leaves the source and its reference pixels untouched");

            var reordered=source.Copy(true);reordered.Name="another name";
            reordered.Reference=new(){Width=1,Height=1,Pixels=new[]{0x102030FFu}};
            reordered.Views=reordered.Views.Reverse().ToDictionary(p=>p.Key,p=>p.Value);
            reordered.WoolMaterials=reordered.WoolMaterials.Reverse().ToDictionary(p=>p.Key,p=>p.Value);
            reordered.SupplementaryMaterials=reordered.SupplementaryMaterials.Reverse().ToDictionary(p=>p.Key,p=>p.Value);
            reordered.SelectedEffects.Reverse();
            foreach(var cell in reordered.Views.Values.SelectMany(grid=>grid.Cells))if(cell is not null)cell.ColorId="another label";
            check(BlueprintContent.Key(reordered)==expected,"content key still ignores identity, reference, color labels and collection insertion order");
        }

        var empty=new Blueprint();
        check(BlueprintContent.Key(empty)==LegacyKey(empty),"empty content retains the original canonical representation");
        var original=Fixture(random,ProductUse.Sword,SwordOrientation.Vertical);
        var progress=new SaveProgress();
        foreach(string id in new[]{"z-design","A-design"})
        {
            var duplicate=original.Copy();duplicate.Id=id;
            progress.BlueprintRecords[id]=DesignStorage.Serialize(duplicate);
        }
        progress.BlueprintRecords["0-corrupt"]="{";
        progress.BlueprintRecords["0-mismatched-id"]=DesignStorage.Serialize(original);
        check(BlueprintContent.Find(progress,original)?.Id=="A-design","canonical comparison preserves ordinal duplicate selection and ignores invalid records");
        progress.BlueprintRecords[original.Id]=DesignStorage.Serialize(original);
        check(BlueprintContent.Find(progress,original)?.Id==original.Id,"an equivalent current record still takes precedence");

        original.Reference=new(){Width=256,Height=256,Pixels=Enumerable.Repeat(0xABCDEF88u,256*256).ToArray()};
        string originalRaw=DesignStorage.Serialize(original);
        long oldBytes=Allocated(()=>LegacyKey(original)),newBytes=Allocated(()=>BlueprintContent.Key(original));
        check(newBytes<oldBytes-256*256*sizeof(uint),"canonical key avoids cloning the entire ignored 256x256 reference image");
        check(BlueprintContent.Key(original)==LegacyKey(original)&&DesignStorage.Serialize(original)==originalRaw,
            "large-reference optimization preserves exact key and full source contents");
        Console.WriteLine($"Blueprint content key with 256x256 reference: previous {oldBytes:N0} B/op; optimized {newBytes:N0} B/op.");
    }

    private static Blueprint Fixture(Random random,ProductUse use,SwordOrientation orientation)
    {
        BeadGrid Grid(int width,int height)=>new()
        {
            Width=width,Height=height,
            Cells=Enumerable.Range(0,width*height).Select(i=>i%4==0?null:new BeadCell
            {
                ColorId="legacy-color-"+i,Rgba=(uint)random.NextInt64(1L<<32),
                MaterialId=i%3==0?null:i%2==0?"old-material":"iridium"
            }).ToList()
        };
        return new()
        {
            Id="draft",Name="Legacy \"作品\"",Revision=17,Use=use,TemplateId="legacy.template",
            SwordOrientation=orientation,BackgroundRgba=(uint)random.NextInt64(1L<<32),
            Views=new(){{"front",Grid(4,5)},{"Back",Grid(3,4)},{"侧面",Grid(1,7)}},
            WoolMaterials=new(){{"wool.z",3},{"wool.A",19}},
            SupplementaryMaterials=new(){{"iridium",0},{"copper",7}},
            SelectedEffects=new(){"z-effect","A-effect","z-effect"},
            Reference=new(){SourceItemId="legacy-reference",Width=2,Height=2,Pixels=new uint[]{0,0x01020304,0xFFFFFFFF,0x000000FF}}
        };
    }

    // Frozen pre-optimization implementation: byte-for-byte canonical-key compatibility oracle.
    private static string LegacyKey(Blueprint source)
    {
        var design=source.Copy();
        design.Id="";design.Name="";design.Revision=0;design.Reference=null;
        design.Views=design.Views.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
        design.WoolMaterials=design.WoolMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
        design.SupplementaryMaterials=design.SupplementaryMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
        design.SelectedEffects.Sort(StringComparer.Ordinal);
        foreach(var cell in design.Views.Values.SelectMany(grid=>grid.Cells))
            if(cell is not null)cell.ColorId="rgba:"+cell.Rgba.ToString("X8");
        return JsonSerializer.Serialize(design);
    }

    private static long Allocated(Func<string> key)
    {
        const int iterations=16;
        for(int i=0;i<8;i++)key();
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<iterations;i++)key();
        return (GC.GetAllocatedBytesForCurrentThread()-before)/iterations;
    }
}
