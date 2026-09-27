using System.Text.Json;
using BetterBeads.Data;

int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
FurnitureTemplates.Configure(DefaultManufacturing.Furniture().Concat(SimpleCrafting.Frames()).Concat(SimpleCrafting.Ornaments()).Concat(FurnitureFinish.Definitions()));
var art=JsonSerializer.Deserialize<ArtDefinition>(File.ReadAllText("BetterBeads/assets/art.json"))!;
Check(art.IsValid(),"editable finish resource valid");
Blueprint Block(string template,int width,int height)
{
    var d=SimpleCrafting.Blank(template);var grid=d.Views["front"];
    for(int y=0;y<height;y++)for(int x=0;x<width;x++)grid.Cells[y*grid.Width+x]=new(){ColorId="test",MaterialId="decoration",Rgba=0xA8B3D5FF};
    return d;
}
ProductSnapshot Make(Blueprint d)
{
    var recipe=SimpleCrafting.Recipes().Single(r=>r.Template.Id==d.TemplateId);
    var made=SimpleCrafting.Evaluate(d,recipe,SimpleCrafting.Catalog(),new InventorySlot?[]{null},0,Guid.NewGuid().ToString(),true,true,1,"");
    Check(made.Plan is not null,"manufacturable "+d.TemplateId);var p=made.Plan!.Product;
    Check(ProductTemplates.Matches(p)&&made.Plan.OutputItemId==ProductTemplates.QualifiedId(p),"valid new snapshot and actual output");
    Check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(p),out var read)&&ProductTemplates.Matches(read!),"new snapshot round trip");
    return p;
}
foreach(int size in new[]{16,32})
{
    var d=Block(size==16?SimpleCrafting.Picture16:SimpleCrafting.Picture32,size,size);string original=DesignStorage.Serialize(d);
    var p=Make(d);var composed=FurnitureFinish.Compose(p);var f=FurnitureTemplates.All.Single(f=>f.Id==p.FurnitureVariantId);
    Check(f.FootprintWidth==size/16&&f.FootprintHeight==size/16&&composed.Width==size,"compact painting dimensions");
    Check(composed.Cells.Count(c=>c?.ColorId=="__frame")==size*4-4,"one pixel frame only");
    Check(composed.Cells[size+1]!.Rgba==d.Views["front"].Cells[size+1]!.Rgba,"interior colors unchanged");
    Check(original==DesignStorage.Serialize(d),"source pixels never mutated");
    var old=p.Copy();old.FurnitureVariantId=null;
    Check(ProductTemplates.Matches(old)&&FurnitureFinish.Compose(old).Width==size+16,"old paintings retain padding");
    Check(GiftGallery.Eligible(p)&&GiftGallery.Eligible(old),"old and new paintings remain giftable");
    var baked=BeadFinish.Bake(composed,art);
    Check(baked.Width==size*4&&baked.Height==size*4,"texture density independent of footprint");
    Check(baked.Pixels[0]==composed.Cells[0]!.Rgba&&baked.Pixels[1]==baked.Pixels[0],"frame is not bead shaded");
}
foreach(var (w,h) in new[]{(16,16),(32,16),(16,32),(32,32)})
{
    var p=Make(Block(SimpleCrafting.LargeOrnament,w,h));var f=FurnitureTemplates.All.Single(f=>f.Id==p.FurnitureVariantId);
    var grid=FurnitureFinish.Compose(p);Check(f.FootprintWidth==w/16&&f.FootprintHeight==1,"one ground row");
    Check(grid.Width==w&&grid.Height==h&&grid.Cells.Count(c=>c is not null)==w*h,"complete tall sprite");
    var old=p.Copy();old.FurnitureVariantId=SimpleCrafting.OrnamentVariant(old.Design);
    Check(ProductTemplates.Matches(old)&&FurnitureTemplates.All.Single(f=>f.Id==old.FurnitureVariantId).FootprintHeight==h/16,"legacy footprint preserved");
}
var sparse=Block(SimpleCrafting.Ornament,1,1);var small=Make(sparse);var surface=BeadFinish.Bake(FurnitureFinish.Compose(small),art);
Check(surface.Pixels[4]==0,"transparent cells stay transparent");
Check(surface.Pixels.Take(4).Distinct().Count()>1,"bead edge shading visible");
var broken=small.Copy();broken.FurnitureVariantId=FurnitureFinish.Picture16;
Check(!ProductTemplates.Matches(broken),"mismatched physical variant rejected");

// Offline review images use the module's actual composition and actual game sprite pixels.
var input=JsonDocument.Parse(File.ReadAllText("art/vertical-sword-1.0-RC2/sebastian.json"));
byte[] bytes=Convert.FromBase64String(input.RootElement.GetProperty("rgba").GetString()!);
Blueprint Person(string template)
{
    var d=SimpleCrafting.Blank(template);var g=d.Views["front"];int ox=(g.Width-16)/2;
    for(int y=0;y<Math.Min(32,g.Height);y++)for(int x=0;x<16;x++)
    {int i=(y*16+x)*4;if(bytes[i+3]==0)continue;uint rgba=((uint)bytes[i]<<24)|((uint)bytes[i+1]<<16)|((uint)bytes[i+2]<<8)|bytes[i+3];g.Cells[y*g.Width+ox+x]=new(){ColorId="sprite",Rgba=rgba,MaterialId="decoration"};}
    return d;
}
object Image(string label,Blueprint d,bool old)
{
    var p=new ProductSnapshot{Design=d,FurnitureVariantId=old?(d.TemplateId==SimpleCrafting.LargeOrnament?SimpleCrafting.OrnamentVariant(d):null):FurnitureFinish.Variant(d)};
    var grid=FurnitureFinish.Compose(p);var pixels=old?new FinishedPixels(grid.Width,grid.Height,grid.Cells.Select(c=>c?.Rgba??0).ToArray()):BeadFinish.Bake(grid,art);
    var f=FurnitureTemplates.All.Single(f=>f.Id==(p.FurnitureVariantId??d.TemplateId));
    return new{label,logicalWidth=grid.Width,logicalHeight=grid.Height,footWidth=f.FootprintWidth,footHeight=f.FootprintHeight,pixels};
}
var review=new[]{Image("16格画 · 旧版",Person(SimpleCrafting.Picture16),true),Image("16格画 · 新版",Person(SimpleCrafting.Picture16),false),
    Image("32格画 · 旧版",Person(SimpleCrafting.Picture32),true),Image("32格画 · 新版",Person(SimpleCrafting.Picture32),false),
    Image("人物摆件 · 旧版",Person(SimpleCrafting.LargeOrnament),true),Image("人物摆件 · 新版",Person(SimpleCrafting.LargeOrnament),false),
    Image("32×32摆件 · 旧版",Block(SimpleCrafting.LargeOrnament,32,32),true),Image("32×32摆件 · 新版",Block(SimpleCrafting.LargeOrnament,32,32),false)};
Directory.CreateDirectory("art/furniture-finish-RC5");
File.WriteAllText("art/furniture-finish-RC5/pixels.json",JsonSerializer.Serialize(review));
Console.WriteLine($"PASS {checks} furniture finish checks; module-generated preview pixels exported.");
