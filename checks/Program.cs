using BetterBeads.Data;

if(args.Contains("--balance")){BalanceReport.Write();return;}
if(args.Length==1 && args[0]=="--benchmark")
{
    PerformanceChecks.Run();return;
}

if(args.Length==1 && args[0]=="--workshop-preview")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Wide=PatternDetailLayout.Calculate(1280,900),Compact=PatternDetailLayout.Calculate(480,420),
        Patterns=ReferencePatterns.All.Select(p=>new{p.Id,p.Name,p.Reward,p.Source,Design=ReferencePatterns.Create(p)})},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return;
}
if(args.Length==3 && args[0]=="--layout")
{
    var layout=EditorLayout.Calculate(int.Parse(args[1]),int.Parse(args[2]));
    var palette=ColorPickerLayout.FavoriteCells(layout.Tools);
    var materials=PickerLayout.Materials(layout.Materials,27,0);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Layout=layout,
        Palette=palette.Select((r,i)=>new{Index=i,Rect=r}),
        PaletteCaption=ColorPickerLayout.FavoriteCaption(layout.Tools),
        PaletteAdjust=ColorPickerLayout.FavoriteAction(layout.Tools),
        MaterialSummary=MaterialPanelLayout.Calculate(layout.Materials,false),
        ClothingSummary=MaterialPanelLayout.Calculate(layout.Materials,true),
        ColorPicker=ColorPickerLayout.Calculate(layout.Frame),
        DyeSlots=ColorPickerLayout.FavoriteCells(ColorPickerLayout.Calculate(layout.Frame).Slots,false),
        Materials=materials.Cells.Select(c=>new{c.Index,c.Rect}),MaterialCaption=materials.Caption,MaterialPrevious=materials.Previous,MaterialNext=materials.Next},
        new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    return;
}

int count = 0, failures = 0;
void Check(bool condition, string name)
{
    if (!condition) { failures++;Console.Error.WriteLine("FAIL: "+name);return; }
    Console.WriteLine($"PASS {++count}: {name}");
}
if(args.Contains("--content-editing-only")){ContentEditingChecks.Run(Check);Console.WriteLine($"内容接口检查 {count} 通过、{failures} 失败。");Environment.ExitCode=failures==0?0:1;return;}
if(args.Contains("--workshop-polish-only")){WorkshopPolishChecks.Run(Check);Console.WriteLine($"工坊打磨检查 {count} 通过、{failures} 失败。");Environment.ExitCode=failures==0?0:1;return;}
if(args.Contains("--pattern-art-only")){PatternArtChecks.Run(Check);Console.WriteLine($"图纸检查 {count} 通过、{failures} 失败。");Environment.ExitCode=failures==0?0:1;return;}
if(args.Contains("--gameplay-only")){GameplayChecks.Run(Check);Console.WriteLine($"玩法检查 {count} 通过、{failures} 失败。");Environment.ExitCode=failures==0?0:1;return;}
if(args.Contains("--gameplay-regression"))
{
    try { CatalogChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL CatalogChecks: "+ex); }
    try { FirstProductsChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL FirstProductsChecks: "+ex); }
    try { DefaultWeaponChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL DefaultWeaponChecks: "+ex); }
    try { SpecialEffectChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL SpecialEffectChecks: "+ex); }
    try { EffectIntegrationChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL EffectIntegrationChecks: "+ex); }
    try { RefinementSoulCreativeChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL RefinementSoulCreativeChecks: "+ex); }
    try { SimpleWorkbenchChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL SimpleWorkbenchChecks: "+ex); }
    try { JourneyChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL JourneyChecks: "+ex); }
    try { WorkbenchSupplyChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL WorkbenchSupplyChecks: "+ex); }
    try { GameplayChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL GameplayChecks: "+ex); }
    Console.WriteLine($"受影响检查组：{count} 项通过，{failures} 项失败。未加载或运行游戏。");
    Environment.ExitCode=failures==0?0:1;return;
}
var a = new Blueprint
{
    Name = "同名图纸", TemplateId = "debug.picture", Use = ProductUse.Picture,
    Views = new() { ["front"] = new() { Width = 2, Height = 1, Cells = new() {
        new() { ColorId = "unknown-retained", Rgba = 0xFF0000FF, MaterialId = "unknown-material" }, null } } },
    Reference = new() { Width = 1, Height = 1, Pixels = new uint[] { 0x123456FF } }
};
var b = a.Copy(true);
b.Views["front"].Cells[0]!.Rgba = 0x00FF00FF;
b.Reference!.Pixels[0] = 0;
Check(a.Id != b.Id && a.Name == b.Name && a.Views["front"].Cells[0]!.Rgba == 0xFF0000FF
    && a.Reference!.Pixels[0] == 0x123456FF, "复制后修改网格和原图互不影响，同名合法");
var progress = new SaveProgress();
var library = new BlueprintRepository(progress);
Check(library.Save(a) && library.Save(b), "保存两份独立图纸");
Check(library.TryOpen(a.Id, out var opened) && opened!.Views["front"].Cells[0]!.MaterialId == "unknown-material",
    "未知材质和颜色往返保留");
opened!.Name = "未保存";
Check(library.TryOpen(a.Id, out var untouched) && untouched!.Name == "同名图纸", "打开编辑不会修改存储");
var stale = a.Copy();
Check(library.Save(a) && !library.Save(stale), "过期修订不能覆盖新修订");
progress.BlueprintRecords["broken"] = "{broken";
Check(!library.TryOpen("broken", out _) && library.TryOpen(b.Id, out _)
    && progress.BlueprintRecords["broken"] == "{broken", "损坏单项隔离且原文保留");
var product = new ProductSnapshot { Design = a.Copy(), ActualMaterials = new() { ["decoration"] = 1 } };
var originalKey = DesignStorage.VisualKey(product);
a.Views["front"].Cells[0]!.Rgba = 1;
progress.BlueprintRecords.Remove(a.Id);
Check(product.Design.Views["front"].Cells[0]!.Rgba == 0xFF0000FF, "删除和改图不改变成品快照");
Check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(product), out var restored)
    && DesignStorage.VisualKey(restored!) == originalKey, "成品序列化后可重建视觉缓存键");
var changed = product.Copy();
changed.Design.Views["front"].Cells[0]!.Rgba = 0x00FF00FF;
Check(DesignStorage.VisualKey(changed) != originalKey, "同图纸同修订不同像素不会命中同一视觉缓存");
var malformed = b.Copy(); malformed.Views["front"].Width = int.MaxValue;
Check(!DesignStorage.IsStructurallyValid(malformed), "异常尺寸拒绝且不发生乘法溢出");
malformed = b.Copy(); malformed.Views["front"].Cells.Clear();
Check(!DesignStorage.IsStructurallyValid(malformed), "网格长度不匹配拒绝");
Check(DesignStorage.IsStructurallyValid(new Blueprint()), "允许未完成的空草稿保存");
Check(!DesignStorage.TryReadSnapshot("{\"SchemaVersion\":999}", out _), "未知快照版本不静默使用");
var square = ProductTemplates.DebugSample(false, false, "square");
var diamond = ProductTemplates.DebugSample(false, true, "diamond");
var hat = ProductTemplates.DebugSample(true, false, "hat");
Check(ProductTemplates.Matches(square) && ProductTemplates.Matches(diamond) && ProductTemplates.Matches(hat), "样例对应稳定模板及尺寸");
Check(DesignStorage.VisualKey(square) != DesignStorage.VisualKey(diamond), "两件同模板样例图案独立");
var atlas = ProductTemplates.CreateAtlas(hat);
Check(atlas.Width == 20 && atlas.Height == 80 && atlas.Cells.Count == 1600
    && atlas.Cells[3 * 20 + 4]!.Rgba != atlas.Cells[63 * 20 + 4]!.Rgba, "帽子四方向按前右左后合成且保留不同背面");
atlas.Cells[3 * 20 + 4]!.Rgba = 0;
Check(hat.Design.Views["front"].Cells[3 * 20 + 4]!.Rgba != 0, "图集生成不共享可变像素");
hat.Design.Views.Remove("back");
Check(!ProductTemplates.Matches(hat), "缺少帽子方向拒绝生成");
var costs = new WorkbenchSettings();
Check(costs.IsConfigured && costs.RecipePrice == 5000
    && costs.Recipe("test.bench") == "388 250 390 100 334 5/Home/test.bench/true/none", "已确认配方价乘10、全部材料乘5");
costs = new() { RecipePrice = null, Ingredients = new() };
Check(!costs.IsConfigured, "无效配置不生成商店配方");
costs.RecipePrice = 25;
costs.Ingredients["388"] = 3;
Check(costs.IsConfigured && costs.Recipe("test.bench") == "388 3/Home/test.bench/true/none", "配方格式、产物和大型物件标记一致");
costs.Ingredients["388"] = -1;
Check(!costs.IsConfigured, "非法制作数量拒绝");
try { CatalogChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL CatalogChecks: "+ex); }
try { LayoutChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL LayoutChecks: "+ex); }
try { EditorChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL EditorChecks: "+ex); }
try { DraftChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL DraftChecks: "+ex); }
try { LibraryChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL LibraryChecks: "+ex); }
try { ImportChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL ImportChecks: "+ex); }
try { ManufacturingChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL ManufacturingChecks: "+ex); }
try { TemplateChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL TemplateChecks: "+ex); }
try { FurnitureChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL FurnitureChecks: "+ex); }
try { FirstProductsChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL FirstProductsChecks: "+ex); }
try { WeaponMaterialChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL WeaponMaterialChecks: "+ex); }
try { WeaponStatsChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL WeaponStatsChecks: "+ex); }
try { WeaponTemplateChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL WeaponTemplateChecks: "+ex); }
try { WeaponPreviewChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL WeaponPreviewChecks: "+ex); }
try { DefaultWeaponChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL DefaultWeaponChecks: "+ex); }
try { SpecialEffectChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL SpecialEffectChecks: "+ex); }
try { EffectIntegrationChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL EffectIntegrationChecks: "+ex); }
try { RefinementSoulCreativeChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL RefinementSoulCreativeChecks: "+ex); }
try { ArtChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL ArtChecks: "+ex); }
try { SimpleWorkbenchChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL SimpleWorkbenchChecks: "+ex); }
try { JourneyChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL JourneyChecks: "+ex); }
try { OptimizationChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL OptimizationChecks: "+ex); }
try { WorkbenchSupplyChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL WorkbenchSupplyChecks: "+ex); }
try { UiRefactorChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL UiRefactorChecks: "+ex); }
try { GameplayChecks.Run(Check); } catch(Exception ex) { failures++;Console.Error.WriteLine("FAIL GameplayChecks: "+ex); }
if(failures>0){Console.Error.WriteLine($"{failures} 项失败，{count} 项通过。");Environment.Exit(1);}
Console.WriteLine($"全部 {count} 项纯数据检查通过。未加载或运行游戏。");
