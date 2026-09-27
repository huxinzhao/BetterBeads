using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using BetterBeads.Data;
using HarmonyLib;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;

internal static class SaleRuntimeChecks
{
    private static Dictionary<string,string> furniture=new();
    private static Texture2D texture=null!;
    private static bool Texture(ref Texture2D __result){__result=texture;return false;}
    private static bool FurnitureData(ref Dictionary<string,string> __result){__result=furniture;return false;}
    private static bool ParseText(string text,ref string __result){__result=text;return false;}
    private static bool SinglePlayer(ref bool __result){__result=false;return false;}
    private static bool DescriptionWidth(ref int __result){__result=320;return false;}
    private static bool NativeStatText(string path,object sub1,object sub2,ref string __result){__result=$"native stats: {sub1} / {sub2}";return false;}
    private static bool NativeText(ref string __result){__result="native stats: {0}";return false;}
    internal static void Run()
    {
        int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var assembly=typeof(Blueprint).Assembly;
        FurnitureTemplates.Configure(DefaultManufacturing.Furniture().Concat(SimpleCrafting.Frames()).Concat(SimpleCrafting.Ornaments()).Concat(FurnitureFinish.Definitions()));
        WeaponTemplates.Configure(DefaultWeapons.Templates());
        var fixture=new Harmony("BetterBeads.SalesFixture");
        texture=(Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        foreach(var f in typeof(Texture2D).GetFields(flags).Where(f=>f.FieldType==typeof(int)&&f.Name.TrimStart('_') is "width" or "height"))f.SetValue(texture,64);
        fixture.Patch(AccessTools.Method(typeof(ParsedItemData),"GetTexture"),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(Texture)));
        fixture.Patch(AccessTools.Method(typeof(DataLoader),"Furniture"),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(FurnitureData)));
        fixture.Patch(AccessTools.Method(typeof(Game1),"parseText",new[]{typeof(string),typeof(SpriteFont),typeof(int)}),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(ParseText)));
        fixture.Patch(AccessTools.Method(typeof(Item),"getDescriptionWidth"),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(DescriptionWidth)));
        fixture.Patch(AccessTools.Method(typeof(LocalizedContentManager),"LoadString",new[]{typeof(string),typeof(object),typeof(object)}),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(NativeStatText)));
        Game1.content=(LocalizedContentManager)FormatterServices.GetUninitializedObject(typeof(LocalizedContentManager));
        fixture.Patch(AccessTools.Method(typeof(LocalizedContentManager),"LoadString",new[]{typeof(string)}),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(NativeText)));
        fixture.Patch(AccessTools.PropertyGetter(Assembly.Load("StardewModdingAPI").GetType("StardewModdingAPI.Context")!,"IsMultiplayer"),prefix:new HarmonyMethod(typeof(SaleRuntimeChecks),nameof(SinglePlayer)));
        foreach(var f in FurnitureTemplates.All)furniture[f.Id]=f.FurnitureData("Stale English furniture");
        Game1.weaponData=WeaponTemplates.All.ToDictionary(w=>w.Id,w=>new StardewValley.GameData.Weapons.WeaponData{Name=w.Id,DisplayName="Stale English weapon",Description="Stale description",Type=w.NativeType,Texture=w.TextureAsset});
        Game1.objectData=new Dictionary<string,StardewValley.GameData.Objects.ObjectData>{["388"]=new(){Price=2},["334"]=new(){Price=60}};
        ItemRegistry.AddTypeDefinition(new FurnitureDataDefinition());ItemRegistry.AddTypeDefinition(new WeaponDataDefinition());ItemRegistry.AddTypeDefinition(new ObjectDataDefinition());
        var products=Activator.CreateInstance(assembly.GetType("BetterBeads.Runtime.ProductItems")!,true)!;
        assembly.GetType("BetterBeads.Runtime.ProductPatches")!.GetMethod("Apply",flags)!.Invoke(null,new object?[]{"BetterBeads.SalesChecks",products,null,null});
        var create=products.GetType().GetMethod("Create")!;
        Item Create(ProductSnapshot s)=>(Item)create.Invoke(products,new object[]{s})!;
        var zh=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/zh.json"))!;
        var en=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("BetterBeads/i18n/default.json"))!;
        var locale=zh;ContentText.Resolve=k=>locale.GetValueOrDefault(k);
        var shop=(ShopMenu)FormatterServices.GetUninitializedObject(typeof(ShopMenu));
        shop.categoriesToSellHere=new();shop.tagsToSellHere=new();
        foreach(var template in new[]{SimpleCrafting.Picture16,SimpleCrafting.Picture32,SimpleCrafting.Ornament,SimpleCrafting.LargeOrnament,SimpleCrafting.Sword,SimpleCrafting.Dagger,SimpleCrafting.Hammer})
        {
            var d=SimpleCrafting.Blank(template);d.Name="My custom artwork 自定义";
            d.Views["front"].Cells[0]=new(){ColorId="test",Rgba=0xAABBCCFF,MaterialId=SimpleCrafting.IsWeapon(d.Use)?"copper":"decoration"};
            var recipe=SimpleCrafting.Recipes().Single(r=>r.Template.Id==template);
            var snapshot=SimpleCrafting.Evaluate(d,recipe,SimpleCrafting.Catalog(),new InventorySlot?[]{null},0,"fixture",true,true,1,"").Plan!.Product;
            foreach(bool legacy in new[]{false,true})
            foreach(string priceCase in new[]{"creative","legacy","zero","normal","collector"})
            {
                var s=snapshot.Copy();if(legacy&&!SimpleCrafting.IsWeapon(d.Use))s.FurnitureVariantId=template==SimpleCrafting.LargeOrnament?SimpleCrafting.OrnamentVariant(d):null;
                s.CreatedInCreativeMode=priceCase=="creative";
                var cost=SimpleCrafting.Cost(d);if(!s.CreatedInCreativeMode)s.ActualMaterials=new(){{cost.Item,cost.Count}};
                if(priceCase is "zero" or "normal" or "collector")
                {
                    int percent=priceCase=="zero"?0:priceCase=="normal"?80:500;
                    s.Valuation=new(){MaterialValue=120,Percent=percent,Collector=priceCase=="collector",CollectorMultiplier=priceCase=="collector"?10:0,FinalPrice=ArtworkMarket.Price(120,percent,priceCase=="collector"?10:1)};
                }
                int expected=priceCase switch{"creative"=>1,"zero"=>0,"normal"=>96,"collector"=>6000,_=>2*cost.Count*(cost.Item=="(O)388"?2:60)};
                Item item=Create(s);
                Check(item.salePrice()==expected,"native salePrice "+template+priceCase);
                Check(item.sellToStorePrice()==expected,"native store price "+template+priceCase);
                Check(Utility.getSellToStorePriceOfItem(item)==expected,"native settlement helper "+template+priceCase);
                Check(item.canBeShipped()&&Utility.highlightShippableObjects(item),"shipping "+template+priceCase);
                shop.categoriesToSellHere.Clear();Check(!shop.highlightItemToSell(item),"shop excludes other categories");
                shop.categoriesToSellHere.Add(item.Category);Check(shop.highlightItemToSell(item),"shop accepts own category");
                locale=zh;Check(item.DisplayName.Contains("拼豆"),"Chinese name overrides native cache");
                locale=en;Check(!item.DisplayName.Contains("拼豆")&&!item.DisplayName.Contains("Stale"),"live English name");
                locale=zh;
                var description=AccessTools.PropertyGetter(item is Furniture?typeof(Furniture):typeof(Tool),"description").Invoke(item,null)!.ToString()!;
                Check(description.Contains("作品")&&!description.Contains("Stale"),"localized base description");
                string line=(string)assembly.GetType("BetterBeads.Runtime.ProductLabels")!.GetMethod("SaleLine",flags)!.Invoke(null,new[]{s})!;
                Check(line.Contains(expected+"金")&&(priceCase!="creative"||line.Contains("创造模式作品")),"display agrees with native settlement");
                string a=item.getDescription(),b=item.getDescription();Check(a==b&&a.Split(line).Length==2,"native tooltip no duplicate");
                locale=en;Check(item.getDescription().Contains("Sale price:")&&!item.getDescription().Contains("售价"),"native tooltip language switch");locale=zh;
                string raw=item.modData["xinzh.BetterBeads/snapshot"];
                Check(DesignStorage.TryReadSnapshot(raw,out var saved)&&saved!.Design.Name==d.Name,"user title unchanged");
                var reload=Create(saved!);Check(reload.salePrice()==expected&&reload.DisplayName.Contains("拼豆"),"reload and picked-up snapshot price/localization");
                item.modData["xinzh.BetterBeads/snapshot"]="invalid";
                Check(!item.canBeShipped()&&!shop.highlightItemToSell(item)&&item.sellToStorePrice()==0,"invalid product not sellable");
            }
        }
        Check(OnlineRules.Protocol=="1.0.0-RC7.1","incompatible pricing protocol bumped");
        Console.WriteLine($"{checks} native sale/localization checks passed. No game instance or graphics device started.");
    }
}

