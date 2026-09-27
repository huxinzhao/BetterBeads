namespace BetterBeads.Data;

public static class SimpleCrafting
{
    public const string Version="simple-2";
    public const string Picture16="xinzh.BetterBeads.WallPicture16",Picture32="xinzh.BetterBeads.WallPicture32",Sword="xinzh.BetterBeads.Sword16";
    public const string Dagger="xinzh.BetterBeads.Dagger16",Hammer="xinzh.BetterBeads.Hammer16",Ornament=DefaultManufacturing.WoodOrnament;
    public const string Sword24="xinzh.BetterBeads.Sword24",Sword32="xinzh.BetterBeads.Sword32";
    public const string Dagger24="xinzh.BetterBeads.Dagger24",Dagger32="xinzh.BetterBeads.Dagger32";
    public const string Hammer24="xinzh.BetterBeads.Hammer24",Hammer32="xinzh.BetterBeads.Hammer32";
    public const string LargeOrnament="xinzh.BetterBeads.FloorOrnament32";
    public const string OrnamentFromLarge11="xinzh.BetterBeads.FloorOrnament32_1x1";
    public const string OrnamentFromLarge21="xinzh.BetterBeads.FloorOrnament32_2x1";
    public const string OrnamentFromLarge12="xinzh.BetterBeads.FloorOrnament32_1x2";
    public static readonly string[] Metals={"copper","iron","iridium"};
    public static bool IsWeapon(ProductUse use)=>use is ProductUse.Sword or ProductUse.Dagger or ProductUse.Hammer;
    public static bool IsWoodwork(ProductUse use)=>use is ProductUse.Picture or ProductUse.WoodFurniture;
    public static bool IsWeaponId(string id)=>id is Sword or Sword24 or Sword32 or Dagger or Dagger24 or Dagger32 or Hammer or Hammer24 or Hammer32;
    public static int WeaponSize(string id)=>id is Sword24 or Dagger24 or Hammer24?24:id is Sword32 or Dagger32 or Hammer32?32:16;
    public static string WeaponId(ProductUse use,int size)=>(use,size) switch
    {
        (ProductUse.Sword,24)=>Sword24,(ProductUse.Sword,32)=>Sword32,
        (ProductUse.Dagger,24)=>Dagger24,(ProductUse.Dagger,32)=>Dagger32,
        (ProductUse.Hammer,24)=>Hammer24,(ProductUse.Hammer,32)=>Hammer32,
        (ProductUse.Sword,_)=>Sword,(ProductUse.Dagger,_)=>Dagger,(ProductUse.Hammer,_)=>Hammer,
        _=>throw new ArgumentException("Not a weapon")
    };
    public static bool Wall(string id)=>id is Picture16 or Picture32;
    public static bool IsOrnamentVariant(string id)=>id is LargeOrnament or OrnamentFromLarge11 or OrnamentFromLarge21 or OrnamentFromLarge12;
    public static (int X,int Y,int Width,int Height) OccupiedBounds(BeadGrid grid)
    {
        int left=grid.Width,top=grid.Height,right=-1,bottom=-1;
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)if(grid.Cells[y*grid.Width+x] is not null)
        {left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        return right<0?(0,0,0,0):(left,top,right-left+1,bottom-top+1);
    }
    public static string OrnamentVariant(Blueprint design)
    {
        if(design.TemplateId!=LargeOrnament||!design.Views.TryGetValue("front",out var grid)||grid.Width!=32||grid.Height!=32||grid.Cells.Count!=1024)return "";
        var bounds=OccupiedBounds(grid);
        if(bounds.Width==0)return "";
        return (bounds.Width>16,bounds.Height>16) switch
        {
            (false,false)=>OrnamentFromLarge11,
            (true,false)=>OrnamentFromLarge21,
            (false,true)=>OrnamentFromLarge12,
            _=>LargeOrnament
        };
    }
    public static bool Supported(Blueprint d)
    {
        if(!DesignStorage.IsStructurallyValid(d)||d.Use!=ProductUse.Sword&&d.SwordOrientation!=SwordOrientation.Diagonal
            ||!(d.Use==ProductUse.Picture&&(Wall(d.TemplateId)||ProductTemplates.IsPicture(d.TemplateId))
            ||d.Use==ProductUse.WoodFurniture&&d.TemplateId is Ornament or LargeOrnament
            ||d.Use==ProductUse.Sword&&d.TemplateId is Sword or Sword24 or Sword32
            ||d.Use==ProductUse.Dagger&&d.TemplateId is Dagger or Dagger24 or Dagger32
            ||d.Use==ProductUse.Hammer&&d.TemplateId is Hammer or Hammer24 or Hammer32))return false;
        int size=d.TemplateId is Picture32 or ProductTemplates.DetailedPicture or LargeOrnament?32:IsWeaponId(d.TemplateId)?WeaponSize(d.TemplateId):16;
        return d.Views.Count==1&&d.Views.TryGetValue("front",out var grid)&&grid.Width==size&&grid.Height==size;
    }
    public static Blueprint Blank(string id)=>new(){TemplateId=id,Use=id switch{Sword or Sword24 or Sword32=>ProductUse.Sword,Dagger or Dagger24 or Dagger32=>ProductUse.Dagger,Hammer or Hammer24 or Hammer32=>ProductUse.Hammer,Ornament or LargeOrnament=>ProductUse.WoodFurniture,_=>ProductUse.Picture},
        SupplementaryMaterials=IsWeaponId(id)?new(){{"copper",0}}:new(),Views=new(){["front"]=new(){Width=CanvasSize(id),Height=CanvasSize(id),Cells=Enumerable.Repeat<BeadCell?>(null,CanvasSize(id)*CanvasSize(id)).ToList()}}};
    private static int CanvasSize(string id)=>id is Picture32 or LargeOrnament?32:IsWeaponId(id)?WeaponSize(id):16;
    public static string Metal(Blueprint d)
    {
        var ids=d.Views.Values.SelectMany(g=>g.Cells).Where(c=>c is not null).Select(c=>c!.MaterialId).Distinct().ToArray();
        if(ids.Length==1 && ids[0] is {} id && Metals.Contains(id))return id;
        if(ids.Length==0 && d.SupplementaryMaterials.Count==1 && d.SupplementaryMaterials.First() is var p && p.Value==0 && Metals.Contains(p.Key))return p.Key;
        return "";
    }
    public static Blueprint Normalize(Blueprint source)
    {
        var d=source.Copy();
        var legacyMaterials=d.Views.Values.SelectMany(g=>g.Cells).Where(c=>c is not null).Select(c=>c!.MaterialId).Concat(d.SupplementaryMaterials.Where(p=>p.Value>0).Select(p=>(string?)p.Key)).Distinct().ToArray();
        string metal=legacyMaterials.Length==0?Metal(d):legacyMaterials.Length==1 && legacyMaterials[0] is {} id && Metals.Contains(id)?id:"";
        d.SelectedEffects.Clear();d.WoolMaterials.Clear();d.SupplementaryMaterials.Clear();
        if(d.TemplateId==ProductTemplates.Picture)d.TemplateId=Picture16;
        if(d.TemplateId==ProductTemplates.DetailedPicture)d.TemplateId=Picture32;
        if(IsWeapon(d.Use) && metal.Length>0)d.SupplementaryMaterials[metal]=0;
        foreach(var c in d.Views.Values.SelectMany(g=>g.Cells).Where(c=>c is not null))c!.MaterialId=IsWoodwork(d.Use)?"decoration":metal.Length>0?metal:null;
        return d;
    }
    public static (string Item,int Count) Cost(Blueprint d)=>IsWoodwork(d.Use)
        ?("(O)388",(d.Views.Values.Sum(g=>g.Cells.Count(c=>c is not null))+3)/4)
        :(Metal(d) switch{"copper"=>"(O)334","iron"=>"(O)335","iridium"=>"(O)337",_=>""},
            (int)Math.Ceiling((d.Use switch{ProductUse.Dagger=>3,ProductUse.Hammer=>6,_=>4})*WeaponSize(d.TemplateId)/16d));
    public static double ReachScale(Blueprint d)=>IsWeapon(d.Use)?WeaponShape.ReachScale(d.Views["front"]):1;
    public static WeaponStatValues Stats(Blueprint d)
    {
        var baseStats=Stats(d.Use,Metal(d));int size=WeaponSize(d.TemplateId);
        double damage=size switch{24=>1.1,32=>1.2,_=>1};
        int Round(int value)=>(int)Math.Round(value*damage,MidpointRounding.AwayFromZero);
        return baseStats with{MinDamage=Round(baseStats.MinDamage),MaxDamage=Round(baseStats.MaxDamage),
            Speed=baseStats.Speed-WeaponShape.SpeedPenalty(d.Views["front"])};
    }
    // Use the frozen manufacturing bill, but reject malformed bills rather than trusting editable item metadata.
    public static bool CanSell(ProductSnapshot snapshot)
    {
        if(!Supported(snapshot.Design)||!snapshot.Design.Views.Values.Any(g=>g.Cells.Any(c=>c is not null)))return false;
#if BEADS_LITE
        if(!ArtworkMarket.Valid(snapshot.Valuation))return false;
#endif
        if(snapshot.CreatedInCreativeMode)return true;
        var cost=Cost(snapshot.Design);
        return cost.Item.Length>0&&cost.Count>0&&snapshot.ActualMaterials.Count==1
            &&snapshot.ActualMaterials.TryGetValue(cost.Item,out int used)&&used==cost.Count;
    }
    public static int SaleValue(ProductSnapshot snapshot,Func<string,int?> baseMaterialPrice)
    {
        if(!CanSell(snapshot))return 0;
        if(snapshot.CreatedInCreativeMode)return 1;
        var cost=Cost(snapshot.Design);
        if(cost.Item.Length==0||cost.Count<=0||snapshot.ActualMaterials.Count!=1
            ||!snapshot.ActualMaterials.TryGetValue(cost.Item,out int used)||used!=cost.Count)return 0;
#if BEADS_LITE
        if(snapshot.Valuation is {} valuation)return ArtworkMarket.Valid(valuation)?valuation.FinalPrice:0;
#endif
        int price=baseMaterialPrice(cost.Item)??0;
        return price<=0?0:(int)Math.Min(int.MaxValue,2L*price*used);
    }
    public static WeaponStatValues Stats(string metal)=>Stats(ProductUse.Sword,metal);
    public static WeaponStatValues Stats(ProductUse use,string metal)=>(use,metal) switch
    {
        (ProductUse.Sword,"copper")=>new(20,28,0,0.8f,0.02f,3),
        (ProductUse.Sword,"iron")=>new(34,47,0,0.84f,0.02f,3),
        (ProductUse.Sword,"iridium")=>new(61,85,-1,0.92f,0.02f,3),
        (ProductUse.Dagger,"copper")=>new(14,20,2,0.65f,0.04f,4),
        (ProductUse.Dagger,"iron")=>new(24,34,2,0.68f,0.04f,4),
        (ProductUse.Dagger,"iridium")=>new(42,58,1,0.72f,0.05f,4),
        (ProductUse.Hammer,"copper")=>new(26,36,-2,1.3f,0.02f,3),
        (ProductUse.Hammer,"iron")=>new(43,60,-2,1.4f,0.02f,3),
        (ProductUse.Hammer,"iridium")=>new(75,105,-3,1.5f,0.02f,3),
        _=>throw new ArgumentException("Choose a supported weapon and metal")
    };
    public static ProcessingCatalog Catalog()
    {
        var c=DefaultProcessing.Create();c.RulesVersion=Version;
        c.Materials=c.Materials.Where(m=>m.Id=="decoration"||Metals.Contains(m.Id)).ToList();
        c.Sources=c.Sources.Where(s=>s.ItemId is "(O)388" or "(O)334" or "(O)335" or "(O)337").ToList();
        c.Sources.RemoveAll(s=>s.ItemId=="(O)388");c.Sources.Add(new(){Id="wood-direct",ItemId="(O)388",MaterialId="decoration",Yield=1});
        return c;
    }
    public static IEnumerable<FurnitureTemplateDefinition> Frames()=>new[]{16,32}.Select(n=>new FurnitureTemplateDefinition(n==16?Picture16:Picture32,ProductUse.Picture,n,n,(n+16)/16,(n+16)/16,
        n==16?"product.picture":"product.picture-detailed","Mods/xinzh.BetterBeads/WallPicture"+n,"painting",n+16,n+16));
    public static IEnumerable<FurnitureTemplateDefinition> Ornaments()=>new[]{
        new FurnitureTemplateDefinition(LargeOrnament,ProductUse.WoodFurniture,32,32,2,2,"product.ornament-large","Mods/xinzh.BetterBeads/FloorOrnament32","other"),
        new FurnitureTemplateDefinition(OrnamentFromLarge11,ProductUse.WoodFurniture,32,32,1,1,"product.ornament-large","Mods/xinzh.BetterBeads/FloorOrnament32_1x1","other",16,16),
        new FurnitureTemplateDefinition(OrnamentFromLarge21,ProductUse.WoodFurniture,32,32,2,1,"product.ornament-large","Mods/xinzh.BetterBeads/FloorOrnament32_2x1","other",32,16),
        new FurnitureTemplateDefinition(OrnamentFromLarge12,ProductUse.WoodFurniture,32,32,1,2,"product.ornament-large","Mods/xinzh.BetterBeads/FloorOrnament32_1x2","other",16,32)};
    public static IEnumerable<ManufacturingRecipe> Recipes()
    {
        foreach(int n in new[]{16,32}){string id=n==16?Picture16:Picture32;yield return new(new(id,ProductUse.Picture,n,n,new[]{"front"},"front",ManufacturingAvailable:true),"(F)"+id,0,DirectItems:true);}
        yield return new(new(Ornament,ProductUse.WoodFurniture,16,16,new[]{"front"},"front",ManufacturingAvailable:true),"(F)"+Ornament,0,DirectItems:true);
        yield return new(new(LargeOrnament,ProductUse.WoodFurniture,32,32,new[]{"front"},"front",ManufacturingAvailable:true),"(F)"+LargeOrnament,0,DirectItems:true);
        foreach(var weapon in DefaultWeapons.Recipes().Where(r=>IsWeaponId(r.Template.Id)))
            yield return weapon with{Template=weapon.Template with{MinimumMaterials=0},SpecialEffectsEnabled=false,DirectItems=true};
    }
    public static ManufacturingPreview Evaluate(Blueprint d,ManufacturingRecipe recipe,ProcessingCatalog catalog,IReadOnlyList<InventorySlot?> inventory,int money,string request,bool creative,bool create,int bag,string scope)
    {
        if(!ManufacturingCatalog.ValidRecipe(recipe)||!recipe.Template.ManufacturingAvailable||recipe.OutputItemId!=(IsWeapon(d.Use)?"(W)":"(F)")+d.TemplateId)return new(ManufacturingFailure.InvalidRecipe,null,null);
        if(bag<0)bag=inventory.Count;
        if(bag>inventory.Count || money<0 || inventory.Any(s=>s is not null && (s.Count<=0||s.MaxStack<=0||string.IsNullOrWhiteSpace(s.Identity)||string.IsNullOrWhiteSpace(s.ItemId)||s.RawMaterial is not null && (s.Yield<=0||s.CanReceiveBeads))))return new(ManufacturingFailure.InvalidInventory,null,null);
        if(inventory.Where(s=>s is not null).Select(s=>s!.Identity).Distinct().Count()!=inventory.Count(s=>s is not null))return new(ManufacturingFailure.InvalidInventory,null,null);
        int n=CanvasSize(d.TemplateId);
        if(!Supported(d)||!(Wall(d.TemplateId)||d.TemplateId is Ornament or LargeOrnament||IsWeaponId(d.TemplateId)) || !DesignStorage.IsStructurallyValid(d) || d.Views.Count!=1 || !d.Views.TryGetValue("front",out var grid)
            ||grid.Width!=n||grid.Height!=n||grid.Cells.All(c=>c is null)||grid.Cells.Any(c=>c is not null && (c.Rgba&255)!=255)
            ||d.SupplementaryMaterials.Count>1||IsWoodwork(d.Use)&&d.SupplementaryMaterials.Count>0
            ||d.SelectedEffects.Count>0||d.WoolMaterials.Count>0||d.SupplementaryMaterials.Any(p=>p.Value!=0 || !Metals.Contains(p.Key))
            ||recipe.Template.Id!=d.TemplateId)return new(ManufacturingFailure.InvalidDesign,null,null);
        string metal=Metal(d);var cost=Cost(d);
        if(cost.Item.Length==0 || IsWoodwork(d.Use) && grid.Cells.Any(c=>c is not null && c.MaterialId!="decoration"))return new(ManufacturingFailure.InvalidDesign,null,null);
        int owned=(int)Math.Min(int.MaxValue,inventory.Where(s=>s?.ItemId==cost.Item && s.RawMaterial is not null).Sum(s=>(long)s!.Count));
        var report=new DraftReport(creative||owned>=cost.Count?Array.Empty<DraftIssue>():new[]{new DraftIssue("insufficient-material")},new[]{new MaterialAmount(cost.Item,cost.Count,owned,creative)});
        if(!report.CanMake)return new(ManufacturingFailure.InvalidDesign,report,null);
        var after=inventory.ToArray();int remaining=creative?0:cost.Count;
        for(int i=0;i<after.Length && remaining>0;i++)if(after[i] is {} slot && slot.ItemId==cost.Item && slot.RawMaterial is not null)
        {int take=Math.Min(remaining,slot.Count);remaining-=take;after[i]=take==slot.Count?null:slot with{Count=slot.Count-take};}
        int output=Array.FindIndex(after,0,bag,s=>s is null);if(output<0)return new(ManufacturingFailure.NoSpace,report,null);
        if(!create)return new(ManufacturingFailure.None,report,null);
        string variant=FurnitureFinish.Variant(d);
        string outputId=variant.Length>0?"(F)"+variant:recipe.OutputItemId;
        after[output]=new("manufactured:"+request,outputId,1,1);
        var stats=IsWeapon(d.Use)?Stats(d).ToSnapshot():new Dictionary<string,double>();
        if(IsWeapon(d.Use))stats["reachScale"]=ReachScale(d);
        var product=new ProductSnapshot{Design=d.Copy(),RulesVersion=Version,FurnitureVariantId=variant.Length>0?variant:null,CreatedInCreativeMode=creative,ActualMaterials=new(){{cost.Item,cost.Count}},
            WeaponRulesVersion=IsWeapon(d.Use)?Version:null,FinalStats=stats};
        return new(ManufacturingFailure.None,report,new(request,outputId,output,money,0,inventory,Enumerable.Range(0,after.Length).Where(i=>after[i]!=inventory[i]).Select(i=>new InventoryChange(i,inventory[i],after[i])),product,recipe,catalog,bag,scope));
    }
}
