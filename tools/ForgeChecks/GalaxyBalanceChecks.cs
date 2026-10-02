using System.Reflection;
using System.Text.Json;
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using StardewValley;
using StardewValley.GameData.Weapons;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Tools;

// Read-only comparison with the installed game's unmodified assets. No Game or save is created.
internal static class GalaxyBalanceChecks
{
    public static void InspectTiming()
    {
        Directory.CreateDirectory(".tools/weapon-speed-reference");
        foreach(var method in typeof(MeleeWeapon).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
            .Where(m=>!m.IsAbstract&&!m.ContainsGenericParameters))
        {
            var instructions=HarmonyLib.PatchProcessor.GetOriginalInstructions(method).ToArray();
            if(!instructions.Any(c=>c.operand is FieldInfo f&&f.DeclaringType==typeof(MeleeWeapon)&&f.Name=="speed"))continue;
            Console.WriteLine(method);
            File.WriteAllLines($".tools/weapon-speed-reference/{method.Name}-{method.GetParameters().Length}.txt",instructions.Select(c=>c.ToString()));
        }
    }

    public static void Run(string game,bool survey=false)
    {
        using var content=new ContentManager(new GameServiceContainer(),Path.Combine(game,"Content"));
        var weapons=content.Load<Dictionary<string,WeaponData>>("Data/Weapons");
        Game1.content=new LocalizedContentManager(new GameServiceContainer(),Path.Combine(game,"Content"));
        Game1.weaponData=weapons;
        Game1.objectData=new Dictionary<string,StardewValley.GameData.Objects.ObjectData>();
        ItemRegistry.AddTypeDefinition(new WeaponDataDefinition());ItemRegistry.AddTypeDefinition(new ObjectDataDefinition());
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        foreach(var template in WeaponTemplates.All)weapons[template.Id]=new WeaponData{Name=template.Id,
            DisplayName=template.Id,Type=template.NativeType,Texture=template.TextureAsset,SpriteIndex=0};
        var productType=typeof(Blueprint).Assembly.GetType("BetterBeads.Runtime.ProductItems",true)!;
        var products=Activator.CreateInstance(productType,true)!;
        var pixels=ReadTexture(Path.Combine(game,"Content","TileSheets","weapons.xnb"));
        var rows=new List<object>();
        var ids=survey?weapons.Where(p=>int.TryParse(p.Key,out _)&&p.Value.SpriteIndex>=0
            &&p.Value.SpriteIndex<pixels.Width/16*(pixels.Height/16)&&p.Value.Texture=="TileSheets/weapons")
            .OrderBy(p=>int.Parse(p.Key)).Select(p=>(p.Value.Type switch{1=>ProductUse.Dagger,2=>ProductUse.Hammer,_=>ProductUse.Sword},p.Key)).ToArray()
            :new[]{(ProductUse.Sword,"4"),(ProductUse.Dagger,"23"),(ProductUse.Hammer,"29")};
        foreach(var (use,id) in ids)
        {
            var native=weapons[id];
            var item=new MeleeWeapon(id);
            var design=SimpleCrafting.Blank(SimpleCrafting.WeaponId(use,16));
            design.SupplementaryMaterials.Clear();design.SupplementaryMaterials["iridium"]=0;
            var grid=design.Views["front"];int columns=pixels.Width/16,index=native.SpriteIndex;
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            {
                int at=((index/columns*16+y)*pixels.Width+index%columns*16+x)*4;
                if(pixels.Rgba[at+3]>=128)grid.Cells[y*16+x]=new(){MaterialId="iridium",ColorId="galaxy-reference",
                    Rgba=(uint)(pixels.Rgba[at]<<24|pixels.Rgba[at+1]<<16|pixels.Rgba[at+2]<<8|255)};
            }
            var result=WeaponGeometry.Evaluate(design);var reference=WeaponGeometry.Reference(use);
            if(!survey&&(result.Geometry.Count!=reference.Count||Math.Abs(result.Reach-1)>1e-10||result.Serration!=0))
                throw new Exception("Original Galaxy silhouette no longer matches its measured geometry: "+id);
            if(!survey&&(result.Stats.Speed!=item.speed.Value||result.SwingTimeScale!=.95||result.Stats.MinDamage>item.minDamage.Value
                ||result.Stats.MaxDamage>item.maxDamage.Value||result.Stats.Knockback>item.knockback.Value*1.1))
                throw new Exception("Galaxy shape missed its damage, speed or knockback calibration: "+id);
            if(survey&&id is "47" or "53" or "66"&&result.Serration!=0)
                throw new Exception("A smooth vanilla scythe falsely triggers serration: "+id);
            if(survey&&id=="45"&&(result.Geometry.Wave<=0||result.Bleeding<=0||result.Bleeding>.5))
                throw new Exception("Wicked Kris must receive bounded wave bleeding.");
            if(survey&&id is "4" or "23" or "29" or "44" or "47" or "53" or "66"&&result.Geometry.Wave!=0)
                throw new Exception("A straight/curved reference falsely triggers wave bleeding: "+id);
            var cost=SimpleCrafting.Cost(design);
            var inventory=new InventorySlot?[]{new("bars",cost.Item,cost.Count,999,RawMaterial:"iridium",Yield:1),null};
            var made=SimpleCrafting.Evaluate(design,SimpleCrafting.Recipes().Single(r=>r.Template.Id==design.TemplateId),
                SimpleCrafting.Catalog(),inventory,0,"galaxy-reference-"+id,false,true,2,"");
            if(made.Plan is null)throw new Exception("Native reference could not be manufactured: "+id+" "+native.Name+" "+made.Failure);
            var snapshot=made.Plan.Product;
            if(!DesignStorage.TryReadSnapshot(DesignStorage.Serialize(snapshot),out var loaded))
                throw new Exception("Galaxy reference snapshot failed to round-trip: "+id);
            foreach(var frozen in new[]{snapshot,loaded!})
            {
                var product=(MeleeWeapon)productType.GetMethod("Create")!.Invoke(products,new object[]{frozen})!;
                var actual=new WeaponStatValues(product.minDamage.Value,product.maxDamage.Value,product.speed.Value,
                    product.knockback.Value,product.critChance.Value,product.critMultiplier.Value);
                if(actual!=result.Stats||frozen.FinalStats["reachScale"]!=result.Reach||frozen.FinalStats["shapeSwingTimeScale"]!=result.SwingTimeScale
                    ||frozen.FinalStats["shapeWave"]!=result.Geometry.Wave||WeaponGeometry.BleedingStrength(frozen)!=result.Bleeding)
                    throw new Exception("Created/reloaded Galaxy reference changed frozen stats: "+id);
            }
            // Center the unchanged sprite in a larger canvas. Empty margins must not buy stats.
            foreach(int size in new[]{24,32})
            {
                var enlarged=SimpleDesignConversion.Convert(design,SimpleCrafting.WeaponId(use,size),out int clipped);
                var other=WeaponGeometry.Evaluate(enlarged);
                if(clipped!=0||other.Stats!=result.Stats||Math.Abs(other.Reach-result.Reach)>1e-10||other.Serration!=result.Serration
                    ||other.Geometry.Wave!=result.Geometry.Wave||other.Bleeding!=result.Bleeding)
                    throw new Exception("Empty margins changed shape stats: "+id+" / "+size);
            }
            rows.Add(new{Type=use.ToString(),Id=id,Name=native.Name,Mode=design.SwordOrientation.ToString(),
                Beads=result.Geometry.Count,
                Original=new{MinDamage=item.minDamage.Value,MaxDamage=item.maxDamage.Value,Speed=item.speed.Value,
                    Knockback=item.knockback.Value,CritChance=item.critChance.Value,CritMultiplier=item.critMultiplier.Value,
                    AreaOfEffect=item.addedAreaOfEffect.Value},
                BeadsStats=result.Stats,Range=result.Reach,Serration=result.Serration,Bleeding=result.Bleeding,Geometry=result.Geometry,SwingTimeScale=result.SwingTimeScale,
                Size=16,Pixels=grid.Cells.Select(c=>c?.Rgba??0),
                Materials=SimpleCrafting.Metals.Select(m=>{var d=design.Copy();d.SupplementaryMaterials=new(){{m,0}};
                    foreach(var c in d.Views["front"].Cells.Where(c=>c is not null))c!.MaterialId=m;return new{Metal=m,Result=WeaponGeometry.Evaluate(d)};}),
                MeanDamageRatio=(result.Stats.MinDamage+result.Stats.MaxDamage)/(double)(native.MinDamage+native.MaxDamage)});
        }
        string json=JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true});
        if(!survey)Console.WriteLine(json);
        else foreach(var row in rows)Console.WriteLine(JsonSerializer.Serialize(row,new JsonSerializerOptions()));
        string target=Path.Combine("art","weapon-shape-1.1.0-RC4",survey?"native-shape-survey.json":"galaxy-comparison.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllText(target,json);
        Console.WriteLine($"PASS {rows.Count} native asset comparisons, manufacturing and reloaded item stats (values reported without assuming parity).");
    }

    private static (int Width,int Height,byte[] Rgba) ReadTexture(string path)
    {
        byte[] bytes=File.ReadAllBytes(path);
        if(bytes.Length<14||bytes[0]!='X'||bytes[1]!='N'||bytes[2]!='B'||(bytes[5]&128)==0)
            throw new InvalidDataException("Expected local LZX-compressed weapon texture.");
        using var input=new MemoryStream(bytes);input.Position=14;
        var type=typeof(Color).Assembly.GetType("MonoGame.Framework.Utilities.LzxDecoderStream",true)!;
        using var decoder=(Stream)Activator.CreateInstance(type,input,BitConverter.ToInt32(bytes,10),bytes.Length-14)!;
        using var decoded=new MemoryStream();decoder.CopyTo(decoded);decoded.Position=0;
        using var reader=new BinaryReader(decoded);
        int count=reader.Read7BitEncodedInt();
        for(int i=0;i<count;i++){reader.ReadString();reader.ReadInt32();}
        if(reader.Read7BitEncodedInt()!=0)throw new InvalidDataException("Unexpected shared texture resources.");
        reader.Read7BitEncodedInt();
        if(reader.ReadInt32()!=0)throw new InvalidDataException("Expected Color texture.");
        int width=reader.ReadInt32(),height=reader.ReadInt32();reader.ReadInt32();int length=reader.ReadInt32();
        if(length!=width*height*4)throw new InvalidDataException("Unexpected weapon texture size.");
        byte[] rgba=reader.ReadBytes(length);
        if(rgba.Length!=length)throw new EndOfStreamException();
        return(width,height,rgba);
    }
}
