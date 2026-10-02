using BetterBeads.Data;
using BetterBeads.Runtime;
using System.Text.Json;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
if(args.Contains("--player-ui-only"))
{
    try{PlayerUiChecks.Run(Check);Console.WriteLine($"PASS {passed} player UI checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--player-experience-only"))
{
    try{PlayerExperienceChecks.Run(Check);Console.WriteLine($"PASS {passed} player experience checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--reliability-only"))
{
    try{ReliabilityChecks.Run(Check);Console.WriteLine($"PASS {passed} reliability checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--geometry-only"))
{
    try{GeometryChecks.Run(Check);Console.WriteLine($"PASS {passed} geometry checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--geometry-reference"))
{
    try
    {
    var pixels=File.ReadAllBytes(".tools/nexus-art-reference/weapons.rgba");
    foreach(var (use,id) in new[]{(ProductUse.Sword,4),(ProductUse.Dagger,23),(ProductUse.Hammer,29)})
    {
        var d=SimpleCrafting.Blank(SimpleCrafting.WeaponId(use,16));var grid=d.Views["front"];
        for(int y=0;y<16;y++)for(int x=0;x<16;x++)if(pixels[((y+id/8*16)*128+id%8*16+x)*4+3]>=128)
            grid.Cells[y*16+x]=new(){MaterialId="iridium",Rgba=0xffffffff};
        var result=WeaponGeometry.Evaluate(d);var baseline=SimpleCrafting.Stats(use,"iridium");
        Console.WriteLine(use+" "+System.Text.Json.JsonSerializer.Serialize(result));
        Check(result.Stats.MinDamage==baseline.MinDamage&&result.Stats.MaxDamage==baseline.MaxDamage&&result.Stats.Speed==baseline.Speed
            &&Math.Abs(result.Reach-1)<1e-10,"original Galaxy silhouette exactly calibrates damage, speed and reach");
    }
    Console.WriteLine($"PASS {passed} original-game reference checks.");
    }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex.Message);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--decor-only"))
{
    try{DecorationChecks.Run(Check);Console.WriteLine($"PASS {passed} decoration checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--circuit-api-only"))
{
    var controller=CircuitCreativeArtwork.CreateController();
    Check(controller.CreatedInCreativeMode,"circuit controller is marked creative");
    Check(controller.Design.Views["front"].Cells.Count==256&&DesignStorage.IsStructurallyValid(controller.Design),"circuit controller has a valid 16x16 bead design");
    Check(SimpleCrafting.SaleValue(controller,_=>999)==0,"creative controller has zero artwork sale value");
    var export=CircuitArtworkExport.Serialize(controller.Design);
    Check(export is not null,"saved artwork can be exported through the read-only circuit format");
    uint before;
    using(var parsed=JsonDocument.Parse(export!))before=parsed.RootElement.GetProperty("Pixels")[7*16+7].GetUInt32();
    controller.Design.Views["front"].Cells[7*16+7]!.Rgba=0x11223344;
    using(var parsed=JsonDocument.Parse(export!))Check(parsed.RootElement.GetProperty("Pixels")[7*16+7].GetUInt32()==before,"artwork export is a detached pixel snapshot");
    Console.WriteLine($"PASS {passed} Lite circuit bridge checks.");return;
}
if(args.Contains("--gift-only"))
{
    try{GiftGalleryChecks.Run(Check);Console.WriteLine($"PASS {passed} gift gallery checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--market-only"))
{
    try{MarketChecks.Run(Check);Console.WriteLine($"PASS {passed} market checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--large-weapons-only"))
{
    try{LargeWeaponChecks.Run(Check);Console.WriteLine($"PASS {passed} large weapon checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--unlock-only"))
{
    try{UnlockChecks.Run(Check);Console.WriteLine($"PASS {passed} workbench unlock checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--share-only"))
{
    try{ShareChecks.Run(Check);Console.WriteLine($"PASS {passed} sharing checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--localization-only"))
{
    try{LocalizationChecks.Run(Check);Console.WriteLine($"PASS {passed} localization checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--creative-only"))
{
    try{CreativeChecks.Run(Check);DesignSwitchChecks.Run(Check);Console.WriteLine($"PASS {passed} creative experience checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--appearance-only"))
{
    try{WeaponAppearanceChecks.Run(Check);Console.WriteLine($"PASS {passed} weapon appearance data checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--received-only"))
{
    try{ReceivedChecks.Run(Check);Console.WriteLine($"PASS {passed} received presentation checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--optimization-only"))
{
    try{OptimizationChecks.Run(Check);ImageImportChecks.Run(Check);ButtonFeedbackChecks.Run(Check);await ExperienceChecks.Run(Check);Console.WriteLine($"PASS {passed} focused optimization checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--feedback-only"))
{
    try{ButtonFeedbackChecks.Run(Check);Console.WriteLine($"PASS {passed} button feedback checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--import-only"))
{
    try{ImageImportChecks.Run(Check);Console.WriteLine($"PASS {passed} image import checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
if(args.Contains("--switch-only"))
{
    try{DesignSwitchChecks.Run(Check);Console.WriteLine($"PASS {passed} design switch checks.");}
    catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    return;
}
void CheckUi()
{
 var dumps=new List<object>();
 foreach(var size in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
 {
  var scale=WorkbenchScale.Calculate(size.Item1,size.Item2,32);var l=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
  var tool=SimpleEditorLayout.ToolButtons(l);var cost=SimpleEditorLayout.CostBox(l);
  var keyboard=ControllerKeyboardLayout.Calculate(l.Frame);
  var keyboardTargets=keyboard.Targets(0);
  Check(keyboard.Keys.Length==24&&keyboard.Pages.Length==2&&keyboard.Actions.Length==4
      &&keyboardTargets.All(r=>keyboard.Box.Contains(r))&&keyboard.Keys.All(r=>r.Width>=40&&r.Height>=44)
      &&keyboard.Actions.All(r=>r.Height>=44)&&keyboard.Keys[^1].Bottom<=keyboard.Actions[0].Y
      &&keyboard.Targets(1).Length==2+ControllerKeyboardLayout.SecondPage.Length+4,
      "paged controller keyboard separates keys and actions "+size);
  var share=LibraryShareLayout.Calculate(l.Frame);
  Check(share.Dialog.Contains(share.ImportFile)&&!share.ImportFile.Overlaps(share.Note)
      &&!share.ImportFile.Overlaps(share.Paste),"share-file import fits dialog "+size);
  var mardLayout=ColorPickerLayout.Calculate(l.Frame);
  var searchLayout=PaletteSearchLayout.Calculate(l.Frame);
  Check(searchLayout.Dialog.Contains(searchLayout.Search)&&searchLayout.Dialog.Contains(searchLayout.Close)
      &&searchLayout.Dialog.Contains(searchLayout.Previous)&&searchLayout.Dialog.Contains(searchLayout.Next)
      &&searchLayout.Results.All(r=>searchLayout.Dialog.Contains(r)&&r.Height>=48&&!r.Overlaps(searchLayout.Status)
          &&!r.Overlaps(searchLayout.Search)&&!r.Overlaps(searchLayout.Previous))
      &&!searchLayout.Search.Overlaps(searchLayout.Close),"MARD search fits dialog "+size);
  Check(mardLayout.Dialog.Contains(mardLayout.ReferenceCaption)&&mardLayout.Dialog.Contains(mardLayout.BeforeCard)
      &&mardLayout.Dialog.Contains(mardLayout.AfterCard)&&!mardLayout.BeforeCard.Overlaps(mardLayout.AfterCard)
      &&!mardLayout.BeforeCard.Overlaps(mardLayout.Close)&&!mardLayout.AfterCard.Overlaps(mardLayout.Apply),
      "MARD before/after preview fits color dialog "+size);
  Check(mardLayout.Dialog.Contains(mardLayout.Close)&&mardLayout.Close.Width==l.CloseButton.Width
      &&mardLayout.Close.Height==l.CloseButton.Height
      &&(!mardLayout.Compact||!mardLayout.Close.Overlaps(mardLayout.DyeTab)&&!mardLayout.ColorTab.Overlaps(mardLayout.DyeTab)),
      "dye return cross matches main cross and clears compact tabs "+size);
  Check(l.Header.Y==l.Frame.Y+(l.UsesDrawers?12:6)
      &&(l.UsesDrawers||l.Frame.Bottom-cost.Bottom==34),"wide content lift keeps frame centered "+size);
  Check(l.Frame.Contains(l.Board)&&l.Canvas.Height>0&&l.Frame.Contains(cost)&&!l.Board.Overlaps(cost),"board/dialogue bounds "+size);
  var controllerHint=SimpleEditorLayout.ControllerHint(l);
  Check(l.Frame.Contains(controllerHint)&&controllerHint.Height>=18&&!controllerHint.Overlaps(l.Board)
      &&!controllerHint.Overlaps(cost)&&!controllerHint.Overlaps(l.SaveButton)&&!controllerHint.Overlaps(l.MakeButton),
      "controller hints use clear gutter "+size);
  Check(tool[0].X==l.Board.X&&tool[^1].Right==l.Board.Right&&tool.All(r=>r.Height>=40)&&tool.Zip(tool.Skip(1)).All(p=>!p.First.Overlaps(p.Second)),"tool edges and fixed hit targets "+size);
  Check(!SimpleEditorLayout.Category(l).Overlaps(SimpleEditorLayout.Size(l))&&SimpleEditorLayout.Category(l).Width>=184
      &&SimpleEditorLayout.Size(l).Width>=200&&SimpleEditorLayout.Size(l).Right==l.Board.Right,"selectors and combined size label fit "+size);
  int leftLift=l.UsesDrawers?0:16;
  Check(SimpleEditorLayout.Category(l).Y==l.Body.Y-leftLift&&tool[0].Y==l.Body.Y+(l.UsesDrawers?48:52)-leftLift
      &&l.CanvasToolbar.Y==l.Body.Y-leftLift&&l.Board.Y==l.Body.Y+(l.UsesDrawers?96:104)-leftLift,
      "left work area moves together "+size);
  Check(l.UsesDrawers?!SimpleEditorLayout.SupplyButton(l).Overlaps(l.EditorTab):
      !l.Board.Overlaps(l.Sidebar)&&l.EditorTab.Y==SimpleEditorLayout.Category(l).Y&&l.LibraryTab.Y==l.EditorTab.Y
      &&l.CloseButton.Y==l.EditorTab.Y&&!l.CloseButton.Overlaps(l.LibraryTab)
      &&l.Sidebar.Y==l.EditorTab.Bottom+8&&l.Sidebar.Bottom==l.Body.Bottom,
      "sidebar and tabs align with left selector "+size);
  Check(!cost.Overlaps(l.SaveButton)&&!cost.Overlaps(l.MakeButton)&&l.Frame.Contains(l.MakeButton)
      &&(l.UsesDrawers||l.SaveButton.Bottom==cost.Bottom&&l.MakeButton.Bottom==cost.Bottom),"dialogue and footer button bottoms align "+size);
  var libraryArea=LibraryLayout.LiteArea(l);var libraryBack=LibraryLayout.LiteBack(libraryArea);
  int libraryRows=Math.Max(1,(libraryArea.Height-160)/64);
  Check(l.Frame.Contains(libraryArea)&&l.Frame.Contains(libraryBack)
      &&libraryBack.Y==l.EditorTab.Y&&libraryBack.Contains(l.CloseButton)
      &&libraryArea.Y+56+(libraryRows-1)*64+56<=libraryArea.Bottom-96,
      "library replaces close target and keeps rows above actions "+size);
  var area=l.UsesDrawers?l.DrawerContent:l.Sidebar;
  foreach(bool sword in new[]{false,true})
  {
   int palette=SimpleEditorLayout.PaletteCell(area,sword);
   int paletteWidth=4*palette+24,paletteLeft=area.X+(area.Width-paletteWidth)/2;
   var touchHits=Enumerable.Range(0,20).Select(i=>SimpleEditorLayout.TouchPaletteHit(new UiRect(
       paletteLeft+i%4*(palette+8),area.Y+8+i/4*(palette+8),palette,palette))).ToArray();
   Check(touchHits.All(r=>area.Contains(r)&&r.Width>=32&&r.Height>=32)
       &&touchHits.SelectMany((r,i)=>touchHits.Skip(i+1).Select(other=>!r.Overlaps(other))).All(ok=>ok),
       "touch palette targets expand without overlap "+size+sword);
   var preview=SimpleEditorLayout.ProductPreview(area,sword);
   int dyeTop=area.Y+8+5*(palette+8);
   Check(dyeTop+52+(sword?48:0)<=area.Bottom,"20 colors and material controls fit "+size+sword);
   var actions=SimpleEditorLayout.ColorActions(area,sword);
   var touch=SimpleEditorLayout.TouchColorActions(area,sword);
   Check(area.Contains(touch.View)&&area.Contains(touch.Dye)&&touch.View.Height>=44&&touch.View.Width>=60
       &&touch.Dye.Width>=104&&!touch.View.Overlaps(touch.Dye)&&!touch.View.Overlaps(touch.Picker)
       &&(l.UsesDrawers||!touch.View.Overlaps(l.Canvas)),"touch view stays in supply actions "+size+sword);
   Check(area.Contains(actions.Picker)&&area.Contains(actions.Dye)&&!actions.Picker.Overlaps(actions.Dye)
       &&actions.Picker.Y==dyeTop&&actions.Dye.Y==dyeTop&&actions.Picker.Width>=40&&actions.Dye.Width>=100,
       "picker sits beside dye button with usable hit targets "+size+sword);
   if(!l.UsesDrawers)Check(preview.Width>=56&&area.Contains(preview),"native item preview fits sidebar "+size+sword);
   if(preview.Width>0)Check(preview.Y==dyeTop+52+(sword?48:0)+8
       &&preview.Width==preview.Height,"preview follows controls and remains square "+size+sword);
  }
  var picker=ColorPickerLayout.Calculate(l.Frame);var pickerCells=ColorPickerLayout.FavoriteCells(picker.Slots,false);
  Check(pickerCells.Count==20&&pickerCells.All(picker.Slots.Contains)&&pickerCells[16].Y>pickerCells[12].Y,"20 dye slots fit selector "+size);
  Check(new[]{picker.Previous,picker.Next,picker.Apply}.All(r=>picker.Dialog.Contains(r)&&r.Height>=40)
      &&picker.Previous.Right+8==picker.Next.X&&picker.Previous.Y==picker.Next.Y
      &&(picker.Compact?picker.Previous.Bottom+8==picker.Apply.Y&&picker.Previous.X==picker.Apply.X&&picker.Next.Right==picker.Apply.Right
          :picker.Previous.Y==picker.Apply.Y&&picker.Next.Right<=picker.Apply.X),
      "dye action buttons have aligned, separate targets "+size);
  Check(!l.AcceptsCanvasInput(true,l.Canvas.X,l.Canvas.Y),"selector/sidebar isolates canvas input "+size);
  var t=new CanvasTransform();t.Layout(l.Canvas);t.Center(16,16);var cell=t.CellRect(7,7);
  Check(cell.Width==cell.Height,"bead cells remain square "+size);
  Check(t.TryCell(scale.Logical((int)((cell.X+cell.Width/2f)*scale.Factor)),scale.Logical((int)((cell.Y+cell.Height/2f)*scale.Factor)),16,16,out int cx,out int cy)&&cx==7&&cy==7,"scaled pointer "+size);
  var touchPreview=SimpleEditorLayout.TouchColorActions(area,false);
  dumps.Add(new{Width=size.Item1,Height=size.Item2,Scale=scale,Layout=l,Tools=tool,CostBox=cost,Category=SimpleEditorLayout.Category(l),Size=SimpleEditorLayout.Size(l),LibraryArea=libraryArea,LibraryBack=libraryBack,LibraryRows=libraryRows,SupplyButton=SimpleEditorLayout.SupplyButton(l),Cell=SimpleEditorLayout.PaletteCell(area),SwordCell=SimpleEditorLayout.PaletteCell(area,true),PicturePreview=SimpleEditorLayout.ProductPreview(area,false),SwordPreview=SimpleEditorLayout.ProductPreview(area,true),ControllerHint=controllerHint,Keyboard=keyboard,TouchActions=new{touchPreview.Picker,touchPreview.Dye,touchPreview.View}});
 }
 Check(WorkbenchScale.Calculate(1920,1080,32).Factor*20>=32,"native full screen font height");
 var resizeCanvas=new CanvasTransform();resizeCanvas.Layout(new UiRect(0,0,160,160));resizeCanvas.Center(16,16);
 resizeCanvas.Relayout(new UiRect(0,0,320,160),16,16);
 Check(resizeCanvas.Zoom==10&&resizeCanvas.CellRect(0,0).Width==resizeCanvas.CellRect(0,0).Height,
     "wider viewport preserves square bead size");
 Check(DirectionalFocus.Move(new[]{new UiRect(0,0,40,40),new UiRect(60,0,40,40),new UiRect(0,60,40,40)},0,1,0)==1
     &&DirectionalFocus.Move(new[]{new UiRect(0,0,40,40),new UiRect(60,0,40,40),new UiRect(0,60,40,40)},0,0,1)==2
     &&DirectionalFocus.Move(new[]{new UiRect(0,0,40,40),new UiRect(60,0,40,40),new UiRect(0,60,40,40)},0,1,1)==1,
     "controller focus follows visible target direction");
 Check(BeadPalette.Defaults.Length==20&&BeadPalette.Normalize(null).Count==20,"five default color rows");
 for(int row=0;row<5;row++)Check(Enumerable.Range(0,3).All(column=>
 {
  uint dark=BeadPalette.Defaults[row*4+column],light=BeadPalette.Defaults[row*4+column+1];
  int Lum(uint c)=>((int)(c>>24)*3+(int)((c>>16)&255)*6+(int)((c>>8)&255))/10;
  return Lum(dark)<Lum(light);
 }),"default shade progression row "+row);
 var legacy=Enumerable.Range(0,16).Select(i=>0x112233FFu+(uint)(i<<8)).ToArray();var migrated=BeadPalette.Normalize(legacy);
 Check(migrated.Take(16).SequenceEqual(legacy)&&migrated.Skip(16).SequenceEqual(BeadPalette.Defaults.Skip(16)),"old sixteen colors preserved and new row appended");
 Directory.CreateDirectory("art/lite-0.16.5-library");File.WriteAllText("art/lite-0.16.5-library/layouts.json",JsonSerializer.Serialize(dumps));
}
if(args.Contains("--experience-only"))
{
 try{CheckUi();ImageImportChecks.Run(Check);DesignSwitchChecks.Run(Check);ButtonFeedbackChecks.Run(Check);await ExperienceChecks.Run(Check);Console.WriteLine($"PASS {passed} experience, import, layout and feedback checks.");}
 catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
 return;
}
if(args.Contains("--ui-only"))
{
 try{CheckUi();Console.WriteLine($"PASS {passed} UI layout checks.");}
 catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
 return;
}
try {
var mardDefinition=JsonSerializer.Deserialize<MardPaletteDefinition>(File.ReadAllText("BetterBeads/assets/Mard221.json"));
Check(MardPaletteReference.Configure(mardDefinition)&&MardPaletteReference.Current.ColorCount==221,"MARD 221 reference loads locally");
var lookup=MardPaletteReference.Current;
Check(lookup.Search("").Length==221&&lookup.Search("g").All(c=>c.Code.StartsWith("G")),"MARD search lists all colors and filters prefixes");
Check(lookup.FindExact(" g017 ") is {Code:"G17"}&&lookup.Search(" g017 ").FirstOrDefault().Code=="G17",
    "MARD search accepts case, whitespace and leading zero");
Check(lookup.FindExact("G") is null&&lookup.Search("Z").Length==0,"MARD search distinguishes prefix and missing code");
Check(mardDefinition!.Colors.Select(c=>c.Code).Distinct().Count()==221&&mardDefinition.DefaultCodes.Distinct().Count()==20,
    "MARD codes and twenty defaults are unique");
Check(Math.Abs(ColorDifference.Ciede2000(new(50,2.6772,-79.7751),new(50,0,-82.7485))-2.0425)<0.0001,
    "CIEDE2000 published test pair");
var mard=MardPaletteReference.Current;int calculated=mard.MatchCalculations;
Check(mard.Match(BeadPalette.Defaults[0]) is {Code:"G8",Exact:true}&&mard.Display(BeadPalette.Defaults[0])=="G8",
    "exact MARD default omits approximate mark");
for(int i=0;i<600;i++)mard.Match(BeadPalette.Defaults[0]);
Check(mard.MatchCalculations==calculated+1,"unchanged dye color reuses nearest-code match");
Check(mard.Match(0x713D30FF) is {Exact:false}&&mard.Display(0x713D30FF).StartsWith("≈ "),
    "custom color retains own RGB and shows approximate code");
var previousColors=BeadPalette.Normalize(new[]{0x123456FFu,0xABCDEFffu});
Check(previousColors[0]==0x123456FFu&&previousColors[1]==0xABCDEFFFu,
    "existing favorite colors survive new defaults");
Check(!MardPaletteReference.Configure(null)&&BeadPalette.Defaults.Length==20,"invalid reference uses bundled defaults");
Check(MardPaletteReference.Current.Search("").Length==20&&MardPaletteReference.Current.Search("G17").Length==1,
    "MARD search uses available fallback entries");
Check(MardPaletteReference.Configure(mardDefinition),"valid reference restores full palette");
if(args.Contains("--mard-search-only")){CheckUi();Console.WriteLine($"PASS {passed} MARD search and layout checks.");return;}
if(args.Contains("--ui-only")){CheckUi();Console.WriteLine($"PASS {passed} UI alignment checks; offline geometry exported.");return;}
if(args.Contains("--picker-only"))
{
 var sampledDesign=SimpleCrafting.Blank(SimpleCrafting.Picture16);
 sampledDesign.Views["front"].Cells[17]=new(){ColorId="custom:AABBCC",Rgba=0xAABBCCFF,MaterialId="decoration"};
 var editor=new EditorDocument(sampledDesign,SimpleCrafting.Catalog(),"front");
 Check(editor.TrySampleColor(1,1,out var sampled)&&sampled=="rgb.AABBCC","sample existing bead exact color");
 Check(!editor.TrySampleColor(2,1,out _)&&!editor.TrySampleColor(-1,1,out _),"empty and outside cells cannot sample");
 Check(!editor.IsDirty&&editor.ViewSnapshot().Cells[17]!.Rgba==0xAABBCCFF,"sampling leaves drawing and save state untouched");
 Console.WriteLine($"PASS {passed} eyedropper checks.");return;
}
if(args.Contains("--templates-only"))
{
 var pixels=new uint[256];pixels[0]=0xFFFFFFFF;pixels[17]=0xAABBCC80;pixels[18]=0x1234567F;
 var picture=LiteTemplatePixels.Create("测试画",SimpleCrafting.Picture16,pixels,16,16);
 Check(picture.Views["front"].Cells.Count(c=>c is not null)==2&&picture.Views["front"].Cells[18] is null,"transparent sprite pixels omitted");
 Check(picture.Views["front"].Cells[17] is {} bead&&bead.Rgba==0xAABBCCFF&&bead.ColorId=="custom:AABBCC"&&bead.MaterialId=="decoration","sprite color and picture material");
 var templateSword=LiteTemplatePixels.Create("测试剑",SimpleCrafting.Sword,pixels,16,16);
 Check(SimpleCrafting.Metal(templateSword)=="iridium"&&SimpleCrafting.Cost(templateSword)==("(O)337",4),"sword template uses current metal rules");
 var progress=new SaveProgress();var repository=new BlueprintRepository(progress);
 Check(repository.SaveAs(picture,picture.Name+" 副本",out var templateCopy)&&templateCopy is not null&&templateCopy.Id!=picture.Id&&progress.BlueprintRecords.Count==1,"copy creates independent saved record");
 Check(repository.TryOpen(templateCopy!.Id,out var reopened)&&reopened!.Views["front"].Cells[17]!.Rgba==0xAABBCCFF&&picture.Revision==0,"copy preserves pixels without altering template");
 progress.HiddenLiteTemplates.Add("builtin:white-chicken");
 var persisted=JsonSerializer.Deserialize<SaveProgress>(JsonSerializer.Serialize(progress))!;
 Check(persisted.HiddenLiteTemplates.Contains("builtin:white-chicken")&&persisted.BlueprintRecords.ContainsKey(templateCopy.Id),"hiding a template persists without deleting a copy");
 var oldSave=JsonSerializer.Deserialize<SaveProgress>("{\"SchemaVersion\":1,\"BlueprintRecords\":{}}")!;
 Check(oldSave.HiddenLiteTemplates.Count==0,"older Lite saves keep templates visible");
 Console.WriteLine($"PASS {passed} built-in template checks.");return;
}
FurnitureTemplates.Configure(DefaultManufacturing.Furniture().Concat(SimpleCrafting.Frames()).Concat(SimpleCrafting.Ornaments()).Concat(FurnitureFinish.Definitions()));
WeaponTemplates.Configure(DefaultWeapons.Templates());ClothingTemplates.Configure(new());
var catalog=SimpleCrafting.Catalog();var recipes=SimpleCrafting.Recipes().ToArray();
Check(ManufacturingCatalog.Validate(recipes,catalog).Count==0,"recipe registration");
Check(recipes.Length==17&&recipes.All(r=>r.DirectItems&&r.Template.ManufacturingAvailable),"seventeen current templates enabled");
Check(new[]{SimpleCrafting.Picture16,SimpleCrafting.Picture32,SimpleCrafting.Ornament,SimpleCrafting.LargeOrnament,
        SimpleCrafting.Sword,SimpleCrafting.Sword24,SimpleCrafting.Sword32,
        SimpleCrafting.Dagger,SimpleCrafting.Dagger24,SimpleCrafting.Dagger32,
        SimpleCrafting.Hammer,SimpleCrafting.Hammer24,SimpleCrafting.Hammer32,
        SimpleCrafting.Wallpaper16,SimpleCrafting.Wallpaper32,SimpleCrafting.Flooring16,SimpleCrafting.Flooring32}
    .All(id=>recipes.Any(r=>r.Template.Id==id)),"all selectable products have recipes");
Check(new[]{"effects","souls","refinement","dual-effects"}.All(k=>!FeatureAccess.Has(k)),"advanced features disabled");
Check(!new WorkbenchSettings().CreativeMode,"creative off by default");
Blueprint Picture(int count,int n=16){var d=SimpleCrafting.Blank(n==16?SimpleCrafting.Picture16:SimpleCrafting.Picture32);for(int i=0;i<count;i++)d.Views["front"].Cells[i]=new(){ColorId="custom:FFFFFF",Rgba=0xAADD77FF,MaterialId="decoration"};return d;}
InventorySlot Raw(string id,int count,string identity="raw")=>new(identity,id,count,999,RawMaterial:id=="(O)388"?"decoration":"copper",Yield:12);
ManufacturingPreview Preview(Blueprint d,InventorySlot?[] slots,int bag=-1,bool creative=false)=>ManufacturingTransaction.Preview(d,recipes.Single(r=>r.Template.Id==d.TemplateId),catalog,new HashSet<string>(),slots,200,"request",creative,bag);
int? BasePrice(string id)=>id switch{"(O)388"=>2,"(O)334"=>60,"(O)335"=>120,"(O)337"=>1000,_=>null};
foreach(var p in new[]{(0,0,16),(1,1,16),(4,1,16),(5,2,16),(256,64,16),(1024,256,32)})
{
 var d=Picture(p.Item1,p.Item3);var a=new InventorySlot?[]{Raw("(O)388",300),null};var result=Preview(d,a);
 Check(SimpleCrafting.Cost(d).Count==p.Item2,"wood rounding "+p.Item1);
 if(p.Item1==0){Check(result.Plan is null,"empty disabled");continue;}
 var plan=result.Plan!;Check(plan is not null&&plan.Changes.Single(c=>c.Slot==0).After!.Count==300-p.Item2,"direct cost "+p.Item1);
 Check(plan!.Changes.All(c=>c.After is null||!c.After.ItemId.Contains(".Bead."))&&plan.MoneyAfter==200,"no surplus or fee");
 Check(ProductTemplates.Matches(plan.Product)&&DesignStorage.TryReadSnapshot(DesignStorage.Serialize(plan.Product),out _),"readable product snapshot");
}
foreach(string id in new[]{"(O)390","(O)771","(O)709","(O)591",BeadItems.BeadId("decoration")})Check(Preview(Picture(1),new InventorySlot?[]{Raw(id,999),null}).Plan is null,"reject nonwood "+id);
var ornament=SimpleCrafting.Blank(SimpleCrafting.Ornament);ornament.Views["front"].Cells[0]=new(){ColorId="wood",Rgba=0xAADD77FF,MaterialId="decoration"};
ornament.Views["front"].Cells[4]=new(){ColorId="wood",Rgba=0xAADD77FF,MaterialId="decoration"};
Check(SimpleCrafting.Supported(ornament)&&SimpleCrafting.Cost(ornament)==("(O)388",1),"floor ornament is a one-tile woodwork");
var ornamentPlan=Preview(ornament,new InventorySlot?[]{Raw("(O)388",2),null}).Plan!;
Check(ornamentPlan.OutputItemId=="(F)"+SimpleCrafting.Ornament&&ProductTemplates.Matches(ornamentPlan.Product),"floor ornament manufactures as furniture");
Check(SimpleCrafting.SaleValue(ornamentPlan.Product,BasePrice)==4,"small ornament sells for twice one wood's base price");
Check(FurnitureTemplates.TryGet(SimpleCrafting.Ornament,out var floor)&&floor.NativeType=="other"&&floor.FootprintWidth==1&&floor.FootprintHeight==1
    &&floor.FurnitureData("摆件").Contains("/1 1/1 1/1/0/2/"),"ornament uses floor furniture footprint and indoor/outdoor placement");
Check(SimpleCrafting.LargeOrnament!=SimpleCrafting.Ornament&&FurnitureTemplates.TryGet(SimpleCrafting.LargeOrnament,out var largeFloor)
    &&largeFloor.NativeType=="other"&&largeFloor.PixelWidth==32&&largeFloor.PixelHeight==32
    &&largeFloor.FootprintWidth==2&&largeFloor.FootprintHeight==2
    &&largeFloor.FurnitureData("大型摆件").Contains("/2 2/2 2/1/0/2/"),"large ornament has distinct 2x2 floor definition");
foreach(int count in new[]{0,1,4,5,1024})
{
 var large=SimpleCrafting.Blank(SimpleCrafting.LargeOrnament);
 for(int i=0;i<count;i++)large.Views["front"].Cells[i]=new(){ColorId="wood",Rgba=0xAADD77FF,MaterialId="decoration"};
 int woodCost=(count+3)/4;
 Check(SimpleCrafting.Supported(large)&&large.Views["front"].Cells.Count==1024&&SimpleCrafting.Cost(large)==("(O)388",woodCost),"large ornament wood cost "+count);
 var made=Preview(large,new InventorySlot?[]{Raw("(O)388",300),null});
 if(count==0)Check(made.Plan is null,"empty large ornament cannot be made");
 else Check(made.Plan is {} plan&&plan.OutputItemId=="(F)"+SimpleCrafting.OrnamentVariant(large)
     &&ProductTemplates.Matches(plan.Product)&&DesignStorage.TryReadSnapshot(DesignStorage.Serialize(plan.Product),out _)
     &&plan.Changes.Single(c=>c.Slot==0).After!.Count==300-woodCost,"large ornament output and exact deduction "+count);
 if(made.Plan is {} largePlan)Check(SimpleCrafting.SaleValue(largePlan.Product,BasePrice)==woodCost*4,"large ornament sale value "+count);
}
foreach(var shape in new[]{(15,15,1,1,SimpleCrafting.OrnamentFromLarge11),(0,0,16,16,SimpleCrafting.OrnamentFromLarge11),
    (8,9,17,1,SimpleCrafting.OrnamentFromLarge21),(0,0,17,16,SimpleCrafting.OrnamentFromLarge21),
    (9,8,1,17,SimpleCrafting.OrnamentFromLarge12),(0,0,16,17,SimpleCrafting.OrnamentFromLarge12),
    (8,8,17,17,SimpleCrafting.LargeOrnament)})
{
 var ornamentDesign=SimpleCrafting.Blank(SimpleCrafting.LargeOrnament);
 for(int y=shape.Item2;y<shape.Item2+shape.Item4;y++)for(int x=shape.Item1;x<shape.Item1+shape.Item3;x++)
     ornamentDesign.Views["front"].Cells[y*32+x]=new(){ColorId="wood",Rgba=0xAADD77FF,MaterialId="decoration"};
 var made=Preview(ornamentDesign,new InventorySlot?[]{Raw("(O)388",300),null}).Plan!;
 int width=shape.Item3>16?32:16,height=shape.Item4>16?32:16;
 var atlas=ProductTemplates.CreateAtlas(made.Product);
 Check(made.OutputItemId=="(F)"+shape.Item5&&made.Product.FurnitureVariantId==shape.Item5
     &&FurnitureTemplates.TryGet(shape.Item5,out var furniture)&&furniture.FootprintWidth==width/16&&furniture.FootprintHeight==height/16
     &&atlas.Width==width&&atlas.Height==height&&atlas.Cells.Count(c=>c is not null)==shape.Item3*shape.Item4,
     "32 canvas footprint follows occupied bounds "+shape.Item5);
 var legacyOrnament=made.Product.Copy();legacyOrnament.FurnitureVariantId=null;
 Check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(made.Product),out var restored)&&restored!.FurnitureVariantId==shape.Item5
     &&ProductTemplates.Matches(restored)&&ProductTemplates.QualifiedId(restored)==made.OutputItemId
     &&ProductTemplates.QualifiedId(legacyOrnament)=="(F)"+SimpleCrafting.LargeOrnament
     &&ProductTemplates.CreateAtlas(legacyOrnament).Width==32&&DesignStorage.VisualKey(restored)!=DesignStorage.VisualKey(legacyOrnament),
     "large ornament variant survives save while old 2x2 products stay unchanged "+shape.Item5);
}
var wrongLarge=SimpleCrafting.Blank(SimpleCrafting.Ornament);wrongLarge.TemplateId=SimpleCrafting.LargeOrnament;
Check(!SimpleCrafting.Supported(wrongLarge),"large ornament rejects mismatched canvas");
var salePicture=Preview(Picture(5),new InventorySlot?[]{Raw("(O)388",3),null}).Plan!.Product;
var visual=new ProductRenderData(salePicture);string visualKey=visual.VisualKey;
salePicture.Design.Views["front"].Cells[0]!.Rgba=0x112233FF;
Check(visual.VisualKey==visualKey&&visual.Snapshot.Design.Views["front"].Cells[0]!.Rgba!=salePicture.Design.Views["front"].Cells[0]!.Rgba
    &&new ProductRenderData(salePicture).VisualKey!=visualKey,"render fingerprint stays paired with detached pixels");
Check(visual.CacheKey("atlas",1)!=visual.CacheKey("atlas",2)&&visual.CacheKey("atlas",1)!=visual.CacheKey("front",1),
    "painting frame revision and view distinguish cached textures");
Check(SimpleCrafting.SaleValue(salePicture,BasePrice)==8,"picture sale uses consumed wood cost");
Check(SimpleCrafting.SaleValue(salePicture,_=>null)==0,"unknown raw price cannot create money");
var forgedBill=salePicture.Copy();forgedBill.ActualMaterials["(O)388"]=999;
Check(SimpleCrafting.SaleValue(forgedBill,BasePrice)==0,"forged bill cannot inflate sale price");
var creativeProduct=salePicture.Copy();creativeProduct.CreatedInCreativeMode=true;
Check(SimpleCrafting.SaleValue(creativeProduct,BasePrice)==0,"creative product cannot generate sale income");
Check(SimpleCrafting.SaleValue(ornamentPlan.Product,_=>int.MaxValue)==int.MaxValue,"sale calculation clamps overflow");
Check(Preview(Picture(5),new InventorySlot?[]{Raw("(O)388",1),null}).Plan is null,"missing raw materials");
var ordered=Preview(Picture(13),new InventorySlot?[]{Raw("(O)388",1,"bag"),null,Raw("(O)388",2,"chestA"),Raw("(O)388",8,"chestB")},2).Plan!;
Check(ordered.OutputSlot==0&&ordered.Changes.Single(c=>c.Slot==2).After is null&&ordered.Changes.Single(c=>c.Slot==3).After!.Count==7,"bag then ordered chests; consumed bag slot reusable");
Check(Preview(Picture(1),new InventorySlot?[]{Raw("(O)388",2)},1).Failure==ManufacturingFailure.NoSpace,"full bag rejected");
Check(Preview(Picture(1),new InventorySlot?[]{new("occupied","(O)390",1,999),Raw("(O)388",2)},1).Failure==ManufacturingFailure.NoSpace,"chest slot is not delivery space");
Check(Preview(Picture(1),new InventorySlot?[]{null},1,true).Plan!.Changes.Count==1,"creative still delivers once without material");
Check(MaterialSupply.WithinRange(5,5,0,0,5)&&!MaterialSupply.WithinRange(6,0,0,0,5),"five tile square");
foreach(var metal in SimpleCrafting.Metals)foreach(int count in new[]{1,15,256})
{
 var d=SimpleCrafting.Blank(SimpleCrafting.Sword);d.SupplementaryMaterials=new(){{metal,0}};
 for(int i=0;i<count;i++)d.Views["front"].Cells[i]=new(){ColorId="white",Rgba=(i%2==0?0xABCDEFffu:0xFEDCBAffu),MaterialId=metal};
 var cost=SimpleCrafting.Cost(d);var p=Preview(d,new InventorySlot?[]{Raw(cost.Item,5),null}).Plan!;
 Check(cost.Count==4&&p.Changes.Single(c=>c.Slot==0).After!.Count==1,"fixed metal charge "+metal+count);
 var target=metal=="copper"?(20,28):metal=="iron"?(34,47):(61,85);
 Check(p.Product.FinalStats["minDamage"]==target.Item1&&p.Product.FinalStats["maxDamage"]==target.Item2&&ProductTemplates.Matches(p.Product),"sword stats and readable snapshot");
 Check(SimpleCrafting.SaleValue(p.Product,BasePrice)==2*cost.Count*BasePrice(cost.Item),"sword sale value "+metal);
 Check(Preview(d,new InventorySlot?[]{Raw(cost.Item,3),null}).Plan is null,"three ingots insufficient");
}
foreach(var kind in new[]{(SimpleCrafting.Dagger,ProductUse.Dagger,3),(SimpleCrafting.Hammer,ProductUse.Hammer,6)})
foreach(var metal in SimpleCrafting.Metals)
{
 var d=SimpleCrafting.Blank(kind.Item1);d.SupplementaryMaterials=new(){{metal,0}};
 d.Views["front"].Cells[17]=new(){ColorId="metal",Rgba=0xABCDEFffu,MaterialId=metal};
 var cost=SimpleCrafting.Cost(d);var p=Preview(d,new InventorySlot?[]{Raw(cost.Item,cost.Count+1),null}).Plan!;
 var stats=SimpleCrafting.Stats(d);
 Check(SimpleCrafting.Supported(d)&&cost.Count==kind.Item3&&p.Changes.Single(c=>c.Slot==0).After!.Count==1,"weapon cost and type "+kind.Item1+metal);
 Check(p.Product.FinalStats["minDamage"]==stats.MinDamage&&p.Product.FinalStats["maxDamage"]==stats.MaxDamage
     &&p.Product.WeaponRulesVersion==SimpleCrafting.Version&&ProductTemplates.Matches(p.Product),"weapon stats and snapshot "+kind.Item1+metal);
 Check(SimpleCrafting.SaleValue(p.Product,BasePrice)==2*cost.Count*BasePrice(cost.Item),"new weapon sale value "+kind.Item1+metal);
 Check(Preview(d,new InventorySlot?[]{Raw(cost.Item,cost.Count-1),null}).Plan is null,"weapon requires full ingots "+kind.Item1+metal);
 var weaponDoc=new EditorDocument(d,catalog,"front");uint tint=weaponDoc.ViewSnapshot().Cells[17]!.Rgba;
 Check(weaponDoc.SetWholeMetal(metal=="copper"?"iron":"copper")&&weaponDoc.ViewSnapshot().Cells[17]!.Rgba==tint,"new weapon material swap "+kind.Item1+metal);
 weaponDoc.Undo();Check(SimpleCrafting.Metal(weaponDoc.Snapshot())==metal,"new weapon material undo "+kind.Item1+metal);
}
var sword=SimpleCrafting.Blank(SimpleCrafting.Sword);var doc=new EditorDocument(sword,catalog,"front");
Check(doc.SetWholeMetal("iron")&&SimpleCrafting.Metal(doc.Snapshot())=="iron","empty metal select");doc.Undo();Check(SimpleCrafting.Metal(doc.Snapshot())=="copper","empty selection undo");doc.Redo();
doc.BeginStroke(1,1,BrushTool.Paint,BeadPalette.Id(0xEEEEEEFF),"iron");doc.EndStroke();var pixel=doc.ViewSnapshot().Cells[17]!.Rgba;
doc.SetWholeMetal("iridium");Check(doc.ViewSnapshot().Cells[17]!.MaterialId=="iridium"&&doc.ViewSnapshot().Cells[17]!.Rgba==pixel,"whole material replaces without recolor");
doc.Undo();Check(SimpleCrafting.Metal(doc.Snapshot())=="iron","metal undo");doc.Redo();Check(SimpleCrafting.Metal(doc.Snapshot())=="iridium","metal redo");
doc.ClearCanvas();Check(doc.ViewSnapshot().Cells.All(c=>c is null)&&SimpleCrafting.Metal(doc.Snapshot())=="iridium","clear retains selection");doc.Undo();Check(doc.ViewSnapshot().Cells[17] is not null,"clear one-step undo");
var full=SimpleCrafting.Blank(SimpleCrafting.Picture32);
for(int i=0;i<1024;i++)full.Views["front"].Cells[i]=new(){ColorId=BeadPalette.Id(BeadPalette.Defaults[0]),Rgba=BeadPalette.Defaults[0],MaterialId="decoration"};
var renderDoc=new EditorDocument(full,catalog,"front");var displayCache=new VersionedSnapshotCache<Blueprint>();int copies=0;
Blueprint Display()=>displayCache.Get(renderDoc.ChangeVersion,()=>{copies++;return renderDoc.Snapshot();});
var firstDisplay=Display();bool reused=true;for(int i=0;i<600;i++)reused&=ReferenceEquals(firstDisplay,Display());
Check(reused&&copies==1&&renderDoc.ViewSize==(32,32),"full 32 canvas copies once over 600 frames");
long version=renderDoc.ChangeVersion;
renderDoc.BeginStroke(0,0,BrushTool.Paint,BeadPalette.Id(BeadPalette.Defaults[1]),"decoration");
Check(renderDoc.ChangeVersion>version&&Display().Views["front"].Cells[0]!.Rgba==BeadPalette.Defaults[1]&&copies==2,"active stroke refreshes display immediately");
version=renderDoc.ChangeVersion;renderDoc.ContinueStroke(1,0);
Check(renderDoc.ChangeVersion>version&&Display().Views["front"].Cells[1]!.Rgba==BeadPalette.Defaults[1],"continuous stroke refreshes each actual edit");
renderDoc.EndStroke();version=renderDoc.ChangeVersion;renderDoc.ClearCanvas();
Check(renderDoc.ChangeVersion>version&&Display().Views["front"].Cells.All(c=>c is null),"clear invalidates display");
var padStrokeDoc=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),catalog,"front");
padStrokeDoc.BeginStroke(0,0,BrushTool.Paint,BeadPalette.Id(BeadPalette.Defaults[1]),"decoration");
padStrokeDoc.ContinueStroke(1,0);padStrokeDoc.EndStroke();
Check(padStrokeDoc.ViewSnapshot().Cells[0] is not null&&padStrokeDoc.ViewSnapshot().Cells[1] is not null,"held controller stroke paints consecutive cells");
padStrokeDoc.Undo();
Check(padStrokeDoc.ViewSnapshot().Cells[0] is null&&padStrokeDoc.ViewSnapshot().Cells[1] is null&&!padStrokeDoc.CanUndo,"held controller stroke is one undo step");
version=renderDoc.ChangeVersion;renderDoc.Undo();Check(renderDoc.ChangeVersion>version&&Display().Views["front"].Cells[0] is not null,"undo invalidates display");
version=renderDoc.ChangeVersion;renderDoc.Redo();Check(renderDoc.ChangeVersion>version&&Display().Views["front"].Cells[0] is null,"redo invalidates display");
version=renderDoc.ChangeVersion;renderDoc.DiscardChanges();Check(renderDoc.ChangeVersion>version&&Display().Views["front"].Cells.All(c=>c is not null),"discard invalidates display");
version=renderDoc.ChangeVersion;renderDoc.Rename("改名");Check(renderDoc.ChangeVersion>version&&Display().Name=="改名","rename invalidates display");
version=renderDoc.ChangeVersion;renderDoc.MarkSaved(2);Check(renderDoc.ChangeVersion>version&&Display().Revision==2,"save revision invalidates display");
version=renderDoc.ChangeVersion;renderDoc.TryOpen(SimpleCrafting.Blank(SimpleCrafting.Picture16));
Check(renderDoc.ChangeVersion>version&&renderDoc.ViewSize==(16,16)&&Display().Views["front"].Cells.Count==256,"opening design invalidates size and display");
var old=doc.Snapshot();old.SupplementaryMaterials["ruby"]=2;old.SelectedEffects.Add("ruby");old.Views["front"].Cells[18]=new(){ColorId="white",Rgba=0xFFFFFFFF,MaterialId="copper"};
var normalized=SimpleCrafting.Normalize(old);Check(SimpleCrafting.Metal(normalized)==""&&normalized.SelectedEffects.Count==0&&normalized.SupplementaryMaterials.Count==0&&old.SelectedEffects.Count==1,"mixed legacy requires choice without mutating original");
Check(Preview(normalized,new InventorySlot?[]{Raw("(O)334",4),null}).Plan is null,"mixed draft cannot bypass gate");
var migrationDoc=new EditorDocument(old,catalog,"front");migrationDoc.StageMigration(normalized);
Check(migrationDoc.IsDirty&&!migrationDoc.CanUndo,"legacy cleanup stays detached but cannot undo removed features");
migrationDoc.DiscardChanges();Check(migrationDoc.Snapshot().SelectedEffects.Count==1,"cancel leaves legacy source intact");
var pureWithGem=doc.Snapshot();pureWithGem.SupplementaryMaterials["ruby"]=2;
Check(SimpleCrafting.Metal(SimpleCrafting.Normalize(pureWithGem))=="","legacy positive supplements require reselection");
var save=new SaveProgress();var design=Picture(5);var saved=BlueprintAutoSave.Prepare(save,design)!;
Check(save.BlueprintRecords.Count==0&&saved.Saved.Revision==1,"save preparation detached");saved.Apply(save);
var unchanged=BlueprintAutoSave.Prepare(save,saved.Saved)!;unchanged.Apply(save);Check(unchanged.Saved.Revision==1&&save.BlueprintRecords.Count==1,"repeat make/save deduplicated");
var changed=saved.Saved.Copy();changed.Name="new name";var revision=BlueprintAutoSave.Prepare(save,changed)!;revision.Apply(save);Check(revision.Saved.Revision==2,"changed design increments once");
Check(BlueprintAutoSave.Prepare(save,saved.Saved) is null,"stale revision rejected");
revision.Restore(save);Check(save.BlueprintRecords[design.Id]==saved.After,"restore previous raw record");
saved.Restore(save);Check(save.BlueprintRecords.Count==0&&BlueprintAutoSave.Prepare(save,saved.Saved) is null,"rollback new and stale deleted rejected");
int wood=10,received=0;bool failed=false;
try{AtomicCraftCommit.Apply(()=>{wood-=2;received++;throw new Exception("injected delivery failure");},()=>{wood=10;received=0;},saved,save);}catch{failed=true;}
Check(failed&&wood==10&&received==0&&save.BlueprintRecords.Count==0,"partial delivery rollback leaves materials and blueprints intact");
AtomicCraftCommit.Apply(()=>{wood-=2;received++;},()=>{wood=10;received=0;},saved,save);Check(wood==8&&received==1&&save.BlueprintRecords.Count==1,"delivery and save commit together");
var fresh=BlueprintAutoSave.Prepare(save,saved.Saved)!;save.BlueprintRecords[design.Id]="external edit";failed=false;
try{AtomicCraftCommit.Apply(()=>wood--,()=>wood++,fresh,save);}catch{failed=true;}Check(failed&&wood==8,"precommit conflict has zero inventory mutation");
var requests=new TransactionRequests();Check(requests.TryBegin("one")&&!requests.TryBegin("one"),"duplicate active submit rejected");requests.Finish("one",true);Check(!requests.TryBegin("one"),"completed submit cannot repeat");
foreach(int n in new[]{16,32}){
 var d=Picture(1,n);var art=PaintingArt.Compose(d.Views["front"]);Check(art.Width==n+16&&art.Cells[8*art.Width+8]!.Rgba==d.Views["front"].Cells[0]!.Rgba,"frame preserves source pixels");
 Check(art.Cells[8*art.Width+9]!.Rgba==0xF1E5CCFF&&art.Cells[0] is null&&SimpleCrafting.Cost(d).Count==1,"empty canvas and margins do not count");
 var f=SimpleCrafting.Frames().Single(f=>f.PixelWidth==n);Check(f.NativeType=="painting"&&f.FootprintHeight==(n+16)/16&&f.FootprintWidth==f.FootprintHeight&&f.FurnitureData("Test").Contains("/1/0/2/"),"native wall-only nonrotating metadata");
}
Check(FurnitureTemplates.TryGet(ProductTemplates.Picture,out var legacy)&&legacy.NativeType=="other","old floor furniture unchanged");
CheckUi();
if(args.Length>0&&args[0].EndsWith(".zip")){
 using var zip=ZipFile.OpenRead(args[0]);Check(!zip.Entries.Any(e=>e.FullName.Contains("/patterns/")||e.FullName.EndsWith(".pdb")),"no presets or debug symbols packaged");
 using var dll=new MemoryStream();zip.GetEntry("BetterBeads/BetterBeads.dll")!.Open().CopyTo(dll);dll.Position=0;using var pe=new PEReader(dll);var r=pe.GetMetadataReader();var types=r.TypeDefinitions.Select(h=>r.GetString(r.GetTypeDefinition(h).Name)).ToHashSet();
 Check(!new[]{"WorkshopService","WorkshopMenu","ReferencePatterns","PatternDetailMenu","ImportPanel","RefinementContent"}.Any(types.Contains),"removed systems absent from assembly");Check(types.Contains("ProductReceivedMenu"),"completion display packaged");
}
Console.WriteLine($"PASS {passed} focused checks; layout data exported (not game screenshots).");
}catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
