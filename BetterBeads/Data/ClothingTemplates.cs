namespace BetterBeads.Data;

public sealed class ClothingSettings
{
    public int HatWool {get;set;}=24;
    public int ShirtWool {get;set;}=48;
    public int PantsWool {get;set;}=72;
    public bool IsValid=>HatWool is >0 and <=9999 && ShirtWool is >0 and <=9999 && PantsWool is >0 and <=9999;
}

public static class ClothingTemplates
{
    private static ClothingSettings settings=new();
    public static void Configure(ClothingSettings value){if(!value.IsValid)throw new ArgumentException("Invalid clothing budgets.");settings=value;}
    public const string Shirt="xinzh.BetterBeads.Shirt8";
    public const string Pants="xinzh.BetterBeads.PrintPants16";
    public static readonly string[] Directions={"front","right","left","back"};
    public static bool IsClothing(ProductUse use)=>use is ProductUse.Hat or ProductUse.Shirt or ProductUse.Pants;
    public static IReadOnlyList<ManufacturingRecipe> Recipes()=>new[]{
        Recipe(ProductTemplates.Hat,ProductUse.Hat,20,settings.HatWool,"(H)"),
        Recipe(Shirt,ProductUse.Shirt,8,settings.ShirtWool,"(S)"),
        Recipe(Pants,ProductUse.Pants,16,settings.PantsWool,"(P)")};
    private static ManufacturingRecipe Recipe(string id,ProductUse use,int size,int wool,string prefix)=>new(
        new(id,use,size,size,use==ProductUse.Pants?new[]{"front"}:(string[])Directions.Clone(),"front",WoolBudget:wool,ManufacturingAvailable:true),prefix+id,0);
    public static TemplateSpec? Find(string id)=>Recipes().FirstOrDefault(r=>r.Template.Id==id)?.Template;
    public static bool Matches(Blueprint d)=>Find(d.TemplateId) is {} t && t.Use==d.Use && d.Views.Count==t.Views.Length
        && t.Views.All(v=>d.Views.TryGetValue(v,out var g) && g.Width==t.Width && g.Height==t.Height && g.Cells.Count==t.Width*t.Height);
    public static string NameKey(string id)=>id==ProductTemplates.Hat?"product.hat":id==Shirt?"product.shirt":"product.pants";
    public static BeadGrid ShirtAtlas(Blueprint d)
    {
        var result=new BeadGrid{Width=256,Height=32,Cells=Enumerable.Repeat<BeadCell?>(null,256*32).ToList()};
        for(int v=0;v<4;v++)for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            result.Cells[(v*8+y)*256+x]=d.Views[Directions[v]].Cells[y*8+x]?.Copy();
        return result;
    }
}
