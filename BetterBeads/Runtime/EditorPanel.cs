using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

internal sealed partial class EditorPanel
{
    private readonly EditorDocument document;
    private readonly ProcessingCatalog catalog;
    private readonly SaveProgress progress;
    private readonly ITranslationHelper text;
    private readonly CanvasTransform transform = new();
    private EditorLayout layout = null!;
    private readonly FrameControls controls = new();
    private string color, material, tool = "paint";
    private string drawer = "";
    private int toolScroll;
    private int materialPage;
    private string? hoverText;
    private bool gridLines = false, materialsOverlay, referenceVisible;
    private bool panning, rightStroke;
    private Point previousMouse;
    private bool previousMiddle, middlePan;
    private string? replaceColorFrom, replaceMaterialFrom;
    private bool replacingMaterial;
    private string? candidateNote;
    private Blueprint? candidate;
    private bool naming;
    private readonly TextBox nameBox;
    private UiRect nameInput;
    private string? notice;
    private bool checking;
    private bool settings, templatePicker, help;
    private int templatePage,helpPage;
    private ManufacturingFailure availabilityFailure;
    private ImportPanel? importer;
    private WeaponPanel? weaponPanel;
    private readonly ReferenceReader referenceReader;
    private readonly ManufacturingService? manufacturing;
    private ManufacturingPlan? manufacturePlan;
    private int manufacturePage;
    private Blueprint? conversionSource;
    private TemplateConversionPreview? conversion;
    private IReadOnlyList<TemplateSpec> conversionChoices=Array.Empty<TemplateSpec>();
    private int conversionIndex,cropX,cropY;
    private ImportSizing conversionSizing;
    private int issuePage;
    private (string View,int Cell)? located;
    private DraftReport report=new(Array.Empty<DraftIssue>(),Array.Empty<MaterialAmount>());
    public bool HasModal => preparationPlan is not null || paletteDraft is not null || fitting is not null || templatePicker || help || candidate is not null || naming || checking || settings || importer is not null || weaponPanel is not null || manufacturePlan is not null || conversionSource is not null;
    public EditorDocument Document => document;
    public bool HasOverlay=>HasModal || drawer.Length>0;
    public event Action<string>? Saved;
    public void ResetForOpenedDesign()
    {
        CloseFitting();Suspend();candidate=null;naming=false;checking=false;settings=false;templatePicker=help=false;drawer="";
        importer=null;weaponPanel=null;manufacturePlan=null;preparationPlan=null;conversionSource=null;conversion=null;
        replaceColorFrom=null;replaceMaterialFrom=null;replacingMaterial=false;candidateNote=null;located=null;notice=null;
        material=DefaultMaterial(document.Snapshot().Use);ClosePalette();
        tool="paint";toolScroll=materialPage=0;Center();
    }

    public EditorPanel(EditorDocument document, ProcessingCatalog catalog, SaveProgress progress, ITranslationHelper text,ReferenceReader referenceReader,ManufacturingService? manufacturing)
    {
        this.document = document; this.catalog = catalog; this.progress = progress; this.text = text;
        this.referenceReader=referenceReader;
        this.manufacturing=manufacturing;
        progress.FavoriteColors=BeadPalette.Normalize(progress.FavoriteColors);
        color=BeadPalette.Id(progress.FavoriteColors[0]);
        material=DefaultMaterial(document.Snapshot().Use);
        nameBox=new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=document.Snapshot().Name,Width=260};
    }

    public void Layout(EditorLayout value)
    {
        bool changed=layout is null || layout.Canvas!=value.Canvas;
        Suspend(); layout = value; controls.Clear(); transform.Layout(value.Canvas);
        importer?.InvalidateLayout();
        weaponPanel?.InvalidateLayout();
        paletteLayout=null;paletteDrag="";
        drawer="";
        if (changed && !value.TooSmall) Center();
    }
    public void DrawHover(SpriteBatch b)
    {if(hoverText is not null && !HasModal)IClickableMenu.drawHoverText(b,hoverText,Game1.smallFont);}
    private void Center() { var grid = document.ViewSnapshot(); transform.Center(grid.Width, grid.Height); }
    private string DefaultMaterial(ProductUse use)=>MaterialRules.IsDecoration(use)?"decoration":ClothingTemplates.IsClothing(use)?"wool":catalog.Materials.FirstOrDefault(m=>MaterialRules.CanUse(m,use))?.Id??"";
    public void BeginImport()
    {
        Suspend();drawer="";settings=false;
        importer=new(document.Snapshot(),document.View,catalog,progress,referenceReader,text,design=>
        {if(design is not null && document.ApplyCandidate(design)){material=DefaultMaterial(design.Use);materialPage=0;tool="paint";Center();}importer=null;controls.Clear();});
    }
    public void Suspend()
    {
        document.EndStroke(); panning = false; previousMiddle = Mouse.GetState().MiddleButton == ButtonState.Pressed;
        nameBox.Selected=false;
        if(ReferenceEquals(Game1.keyboardDispatcher.Subscriber,nameBox)) Game1.keyboardDispatcher.Subscriber=null;
    }
    private void BeginWeaponPanel()
    {
        Suspend();drawer="";settings=false;
        weaponPanel=new(document.Snapshot(),catalog,progress,manufacturing,text,design=>
        {if(design is not null)document.ApplyCandidate(design);weaponPanel=null;controls.Clear();});
    }
    public bool Save()
    {
        Suspend(); var snapshot=document.Snapshot();
        if(!new BlueprintRepository(progress).Save(snapshot)){notice="editor.save-failed";return false;}
        document.MarkSaved(snapshot.Revision);notice="editor.saved";Saved?.Invoke(snapshot.Id);return true;
    }

    public void Click(int x, int y, bool right = false)
    {
        if (layout.TooSmall) return;
        if(importer is not null){if(!right)importer.Click(x,y);return;}
        if(weaponPanel is not null){if(!right)weaponPanel.Click(x,y);return;}
        if(!controls.IsReady)return;
        if(paletteDraft is not null && !right && PaletteClick(x,y))return;
        notice=null;
        if(naming && !right && nameInput.Contains(x,y)){nameBox.Selected=true;Game1.keyboardDispatcher.Subscriber=nameBox;return;}
        if (!right)
        {
            Suspend();
            if(controls.TryInvoke(x,y))return;
        }
        if (HasModal) return;
        if (drawer.Length > 0) { if (!layout.Drawer.Contains(x,y)) {drawer = "";controls.Clear();} return; }
        var grid = document.ViewSnapshot();
        if (!layout.AcceptsCanvasInput(HasOverlay,x,y)) return;
        if (!right && Keyboard.GetState().IsKeyDown(Keys.Space))
        { Suspend(); panning = true; middlePan = false; previousMouse = new(x,y); return; }
        if (!transform.TryCell(x,y,grid.Width,grid.Height,out int cx,out int cy)) return;
        if (!right && tool == "pick") { var picked = document.Pick(cx,cy); if (picked is not null) color = BeadPalette.Id(picked.Rgba); return; }
        if (!right && tool == "fill") { document.Fill(cx,cy,color,material); return; }
        rightStroke = right;
        document.BeginStroke(cx,cy,right || tool == "erase" ? BrushTool.Erase : tool == "material" ? BrushTool.Material : BrushTool.Paint,color,material);
    }

    public void Update()
    {
        if (layout.TooSmall) return;
        var mouse = Mouse.GetState();
        var point = new Point(WorkbenchUi.MouseX,WorkbenchUi.MouseY);
        if(paletteDraft is not null)
        {
            if(mouse.LeftButton!=ButtonState.Pressed)paletteDrag="";
            else if(paletteDrag.Length>0)UpdatePaletteColor(point.X,point.Y);
            return;
        }
        bool middle = mouse.MiddleButton == ButtonState.Pressed;
        if (middle && !previousMiddle && !HasModal && drawer.Length==0 && layout.Canvas.Contains(point.X,point.Y))
        { document.EndStroke(); panning=true; middlePan=true; previousMouse=point; }
        previousMiddle=middle;
        if (HasModal) return;
        if (panning)
        {
            if ((middlePan ? !middle : mouse.LeftButton != ButtonState.Pressed) || !layout.Canvas.Contains(point.X,point.Y)) { panning = false; return; }
            transform.Pan(point.X - previousMouse.X,point.Y - previousMouse.Y); previousMouse = point; return;
        }
        if (!document.IsStrokeActive) return;
        if ((rightStroke ? mouse.RightButton : mouse.LeftButton) != ButtonState.Pressed) { document.EndStroke(); return; }
        var grid = document.ViewSnapshot();
        if (drawer.Length > 0 || !transform.TryCell(point.X,point.Y,grid.Width,grid.Height,out int x,out int y)) document.EndStroke();
        else document.ContinueStroke(x,y);
    }

    public bool Key(Keys key)
    {
        // Esc/undo may arrive before the next draw; old confirmation callbacks must be invalidated now.
        controls.Clear();
        if(paletteDraft is not null){if(key==Keys.Escape)ClosePalette();return true;}
        if(importer is not null){if(key==Keys.Escape)importer.Back();return true;}
        if(weaponPanel is not null){if(key==Keys.Escape)weaponPanel.Cancel();return true;}
        if(naming)
        {
            if(key==Keys.Escape){Suspend();naming=false;}
            else if(key==Keys.Enter){document.Rename(nameBox.Text);Suspend();naming=false;}
            return true;
        }
        if (HasModal) { if(key==Keys.Escape){CloseFitting();templatePicker=help=false;candidate=null;checking=false;settings=false;manufacturePlan= null;preparationPlan=null;conversionSource=null;conversion=null;} return true; }
        if (key == Keys.Escape && (drawer.Length > 0 || replacingMaterial)) { drawer = ""; replacingMaterial=false; return true; }
        if(drawer.Length>0)return true;
        var state = Keyboard.GetState();
        if (state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl))
        {
            if (key == Keys.Z) { document.Undo();Center(); return true; }
            if (key == Keys.Y) { document.Redo();Center(); return true; }
        }
        return false;
    }
    public void Scroll(int direction)
    {
        if(HasModal) return;
        controls.Clear();
        int x = WorkbenchUi.MouseX, y = WorkbenchUi.MouseY;
        if(drawer=="tools")toolScroll=Math.Max(0,toolScroll-Math.Sign(direction)*52);
        else if(drawer=="materials")materialPage=Math.Max(0,materialPage-Math.Sign(direction));
        else if(!layout.UsesDrawers && layout.Tools.Contains(x,y))return;
        else if(!layout.UsesDrawers && layout.Materials.Contains(x,y))materialPage=Math.Max(0,materialPage-Math.Sign(direction));
        else if (layout.AcceptsCanvasInput(HasOverlay,x,y)) { Suspend(); transform.ZoomAt(x,y,direction); }
    }

    public void Draw(SpriteBatch b)
    {
        controls.Clear();
        hoverText=null;
        if (layout.TooSmall) { Label(b,"editor.small",layout.Frame); return; }
        var design=document.Snapshot();
        var grid=design.Views[document.View];
        if(!catalog.Materials.Any(m=>m.Id==material && MaterialRules.CanUse(m,design.Use)))
        {material=DefaultMaterial(design.Use);materialPage=0;}
        if(!WeaponMaterials.IsWeapon(design.Use) && tool=="material")tool="paint";
        var activeRecipe=manufacturing?.FindRecipe(design.TemplateId);
        var availability=activeRecipe is null?null:manufacturing!.CheckAvailability(design,activeRecipe,progress);
        availabilityFailure=availability?.Failure??ManufacturingFailure.InvalidRecipe;
        report=availability?.Report??DraftStatus.Evaluate(design,catalog,progress,manufacturing);
        TextButton(b,layout.ProductButton,TemplateName(design.TemplateId)+(layout.UsesDrawers?"":" ▾"),()=>{templatePicker=true;templatePage=0;});
        DrawToolButton(b,layout.PaintButton,"paint");DrawToolButton(b,layout.EraseButton,"erase");
        Button(b,layout.MoreButton,"editor.more",()=>OpenDrawer("tools"));
        if(layout.UsesDrawers)Button(b,layout.SidebarButton,"editor.sidebar",()=>OpenDrawer("colors"));
        ArtResources.Panel(b,layout.Board,"board");
        Fill(b,layout.Canvas,new Color(232,219,196));
        var reference = design.Reference;
        if (referenceVisible && reference is not null)
            for (int y=0;y<reference.Height;y++) for(int x=0;x<reference.Width;x++)
                FillClipped(b,transform.CellRect(x,y),ColorOf(reference.Pixels[y*reference.Width+x])*0.4f,layout.Canvas);
        for (int y=0;y<grid.Height;y++) for(int x=0;x<grid.Width;x++)
        {
            var cell = grid.Cells[y*grid.Width+x]; var rect = transform.CellRect(x,y);
            if (cell is not null) {if(!ArtResources.BeadCell(b,rect,layout.Canvas,ColorOf(cell.Rgba)))FillClipped(b,rect,ColorOf(cell.Rgba),layout.Canvas);}
            else if(transform.Zoom>=4)
            {
                int peg=Math.Max(1,transform.Zoom/6),cx=rect.X+rect.Width/2,cy=rect.Y+rect.Height/2;
                FillClipped(b,new(cx-peg/2,cy-peg/2,peg+1,peg+1),new Color(181,159,128),layout.Canvas);
                FillClipped(b,new(cx-peg/2,cy-peg/2,peg,Math.Max(1,peg/2)),new Color(255,245,222),layout.Canvas);
            }
            if (gridLines && transform.Zoom>=4)
            {
                FillClipped(b,new(rect.X,rect.Y,rect.Width,1),Color.Black*0.25f,layout.Canvas);
                FillClipped(b,new(rect.X,rect.Y,1,rect.Height),Color.Black*0.25f,layout.Canvas);
            }
            if(materialsOverlay && cell is not null && transform.Zoom>=6)
            {
                int index=catalog.Materials.FindIndex(m=>m.Id==cell.MaterialId);
                FillClipped(b,new(rect.X+2,rect.Y+2,Math.Max(2,transform.Zoom/4),Math.Max(2,transform.Zoom/4)),
                    index<0 ? Color.Magenta : new Color(70+index*53%180,70+index*89%180,70+index*113%180),layout.Canvas);
            }
            if(located is {} target && target.View==document.View && target.Cell==y*grid.Width+x)
            {
                FillClipped(b,new(rect.X,rect.Y,rect.Width,2),Color.Red,layout.Canvas);
                FillClipped(b,new(rect.X,rect.Bottom-2,rect.Width,2),Color.Red,layout.Canvas);
                FillClipped(b,new(rect.X,rect.Y,2,rect.Height),Color.Red,layout.Canvas);
                FillClipped(b,new(rect.Right-2,rect.Y,2,rect.Height),Color.Red,layout.Canvas);
            }
        }
        if(!HasModal && drawer.Length==0 && transform.TryCell(WorkbenchUi.MouseX,WorkbenchUi.MouseY,grid.Width,grid.Height,out int ghostX,out int ghostY))
        {
            var ghost=transform.CellRect(ghostX,ghostY);
            if(tool=="paint" && grid.Cells[ghostY*grid.Width+ghostX] is null && BeadPalette.Resolve(catalog,color) is {} selectedColor)
                ArtResources.BeadCell(b,ghost,layout.Canvas,ColorOf(selectedColor.Rgba)*0.45f);
        }
        if(!layout.UsesDrawers)
        {
            ArtResources.Panel(b,layout.Sidebar,"inset");
            DrawPalette(b,layout.Tools);DrawSidebar(b,layout.Materials,design);
        }
        string label = text.Get(document.IsDirty ? "editor.dirty" : "editor.clean") + " · " + transform.Zoom + "×";
        if(replaceColorFrom is not null || replacingMaterial) label=text.Get("editor.choose-target").ToString();
        if(transform.TryCell(WorkbenchUi.MouseX,WorkbenchUi.MouseY,grid.Width,grid.Height,out int hx,out int hy)) label += $" · ({hx+1}, {hy+1})";
        var problem=report.Issues.FirstOrDefault(i=>i.Code!="unavailable") ?? report.Issues.FirstOrDefault();
        if(problem is not null) label+=" · "+text.Get("issue."+problem.Code);
        if(availability is {Failure: not ManufacturingFailure.None and not ManufacturingFailure.InvalidDesign})
            label=text.Get("manufacturing.error."+availability.Failure).ToString();
        if(notice is not null)label=text.Get(notice).ToString();
        string status=problem is not null?problem.Code switch{"locked-color"=>"lock","unknown-color"=>"missing-color","unassigned-material"=>"unassigned","insufficient-material" or "minimum-material"=>"missing-material",_=>"invalid-material"}:document.IsDirty?"unsaved":"success";
        int statusInset=ArtResources.Status(b,status,new Rectangle(layout.Footer.X,layout.Footer.Y+4,12,12))?18:0;
        ArtResources.TextLine(b,label,new(layout.Footer.X+statusInset,layout.Footer.Y,layout.Footer.Width-statusInset,24),ArtResources.Ink,scale:0.84f);
        if(layout.Status.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=label;
        controls.Add((new(layout.Footer.X,layout.Footer.Y,layout.Footer.Width,24),()=>{checking=true;issuePage=0;}));
        Button(b,layout.SaveButton,"editor.save",()=>Save());
        TextButton(b,layout.MakeButton,text.Get(availability?.CanMake==true?"editor.make":"editor.check-cost"),()=>{if(availability?.CanMake==true)BeginManufacture();else{checking=true;issuePage=0;}},iconKey:availability?.CanMake==true?"primary":"");
        Button(b,layout.HelpButton,layout.UsesDrawers?"editor.help-short":"editor.help",()=>help=true);
        if(drawer.Length>0)
        {
            Fill(b,layout.Frame,Color.Black*0.45f);controls.Clear();hoverText=null;
            if(drawer=="tools")DrawMoreTools(b,layout.Drawer);
            else
            {
                ArtResources.Panel(b,layout.Drawer);
                DrawDrawerTab(b,layout.DrawerColors,"editor.drawer-colors","colors");
                DrawDrawerTab(b,layout.DrawerMaterials,"editor.drawer-materials","materials");
                Button(b,layout.DrawerClose,"editor.drawer-close",()=>{drawer="";controls.Clear();});
                if(drawer=="colors")DrawPalette(b,layout.DrawerContent);else DrawSidebar(b,layout.DrawerContent,design);
            }
        }
        if(settings)DrawSettings(b,design);
        if(templatePicker)DrawTemplates(b);
        if(help)DrawHelp(b);
        if(fitting is not null)DrawFitting(b);
        if(checking)DrawIssues(b,design);
        if(candidate is not null)
        {
            Fill(b,layout.Frame,Color.Black*0.65f); controls.Clear();
            var dialog=new UiRect(layout.Frame.X+24,layout.Frame.Y+64,layout.Frame.Width-48,layout.Frame.Height-96);
            ArtResources.Panel(b,dialog); Line(b,text.Get("editor.replacement").ToString(),new(dialog.X+12,dialog.Y+12,dialog.Width-24,28),ArtResources.Ink);
            if(candidateNote is not null)Line(b,candidateNote,new(dialog.X+12,dialog.Y+44,dialog.Width-24,28),ArtResources.Ink);
            DrawPreview(b,candidate.Views[document.View],new(dialog.X+24,dialog.Y+80,Math.Min(160,dialog.Width-48),Math.Min(160,dialog.Height-144)));
            Button(b,new(dialog.Right-264,dialog.Bottom-52,124,40),"editor.cancel",()=>candidate=null);
            Button(b,new(dialog.Right-132,dialog.Bottom-52,124,40),"editor.apply",()=>{document.ApplyCandidate(candidate!);candidate=null;});
        }
        if(naming)
        {
            Fill(b,layout.Frame,Color.Black*0.65f);controls.Clear();
            var dialog=new UiRect(layout.Frame.X+24,layout.Frame.Y+64,layout.Frame.Width-48,220);
            ArtResources.Panel(b,dialog);Label(b,"editor.rename",new(dialog.X+12,dialog.Y+8,dialog.Width-24,40));
            nameBox.X=dialog.X+16;nameBox.Y=dialog.Y+64;nameBox.Width=Math.Min(360,dialog.Width-32);
            nameInput=new(nameBox.X,nameBox.Y,nameBox.Width,48);nameBox.Draw(b);
            Button(b,new(dialog.Right-264,dialog.Bottom-52,124,40),"editor.cancel",()=>{Suspend();naming=false;});
            Button(b,new(dialog.Right-132,dialog.Bottom-52,124,40),"editor.name-apply",()=>{document.Rename(nameBox.Text);Suspend();naming=false;});
        }
        if(importer is not null){controls.Clear();importer.Draw(b,layout.Frame);}
        if(manufacturePlan is not null)DrawManufacture(b);
        if(conversionSource is not null)DrawConversion(b);
        if(weaponPanel is not null){controls.Clear();weaponPanel.Draw(b,layout.Frame);}
        if(paletteDraft is not null)DrawPaletteEditor(b);
        if(preparationPlan is not null)DrawPreparation(b);

    }

    private void BeginManufacture()
    {
        Suspend();var design=document.Snapshot();
        var recipe=manufacturing?.FindRecipe(design.TemplateId);
        if(recipe is null){notice="issue.unavailable";return;}
        var preview=manufacturing!.Preview(design,recipe,progress,Guid.NewGuid().ToString("N"));
        if(preview.Plan is null)
        {notice="manufacturing.error."+preview.Failure;return;}
        manufacturePlan=preview.Plan;manufacturePage=0;drawer="";
    }
    private void DrawManufacture(SpriteBatch b)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var box=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,box);var plan=manufacturePlan!;var product=plan.Product;
        Line(b,text.Get("manufacturing.confirm-title").ToString(),new(box.X+12,box.Y+12,box.Width-24,28),ArtResources.Ink);
        Line(b,product.Design.Name,new(box.X+12,box.Y+44,box.Width-24,28),ArtResources.Ink);
        Line(b,text.Get(plan.IsCreative?"creative.confirm":"manufacturing.cost",new{cost=plan.MoneyBefore-plan.MoneyAfter}).ToString(),new(box.X+12,box.Y+76,box.Width-24,28),ArtResources.Ink);
        var materials=product.ActualMaterials.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>text.Get("material."+p.Key)+" × "+p.Value).ToList();
        var range=manufacturing?.SupplyRange;
        materials.Add(text.Get("supply.range",new{radius=range?.Radius??5,count=range?.Chests??0}));
        foreach(var change in plan.Changes)
        {
            int retained=change.Slot==plan.OutputSlot?0:change.Before?.ItemId==change.After?.ItemId?change.After?.Count??0:0;
            if(change.Before is {} input && input.Count>retained)
                materials.Add(text.Get("supply.consume",new{name=ItemRegistry.GetDataOrErrorItem(input.ItemId).DisplayName,count=input.Count-retained,quality=QualityName(input.Quality)}));
            if(change.Slot!=plan.OutputSlot && change.After is {CanReceiveBeads:true} output)
            {
                int old=change.Before?.ItemId==output.ItemId?change.Before.Count:0;
                if(output.Count>old)materials.Add(text.Get("supply.return",new{name=ItemRegistry.GetDataOrErrorItem(output.ItemId).DisplayName,count=output.Count-old}));
            }
        }
        if(WeaponStatValues.TryRead(product.FinalStats,out var stats))
        {
            materials.Add(text.Get("weapon.damage",new{min=stats!.MinDamage,max=stats.MaxDamage}).ToString());
            materials.Add(text.Get("weapon.speed",new{speed=stats.Speed,knockback=stats.Knockback.ToString("0.##")}).ToString());
            materials.Add(text.Get("weapon.critical",new{chance=(stats.CritChance*100).ToString("0.##"),multiplier=stats.CritMultiplier.ToString("0.##")}).ToString());
        }
        foreach(string effect in product.Effects)materials.Add(text.Get("effect.description."+effect).ToString());
        if(product.EffectParameters.GetValueOrDefault("damagePenaltyPercent")>0)
            materials.Add(text.Get("effect.penalty",new{penalty=product.EffectParameters["damagePenaltyPercent"]}).ToString());
        int count=Math.Max(1,(box.Height-224)/32);
        manufacturePage=Math.Clamp(manufacturePage,0,Math.Max(0,(materials.Count-1)/count));
        for(int i=0;i<count && manufacturePage*count+i<materials.Count;i++)
        {
            var material=materials[manufacturePage*count+i];
            Line(b,material,new(box.X+12,box.Y+116+i*32,box.Width-24,28),ArtResources.Ink);
        }
        int w=(box.Width-36)/2;
        Button(b,new(box.X+12,box.Bottom-96,w,40),"processing.previous",()=>manufacturePage--,manufacturePage>0);
        Button(b,new(box.X+24+w,box.Bottom-96,w,40),"processing.next",()=>manufacturePage++,(manufacturePage+1)*count<materials.Count);
        Button(b,new(box.X+12,box.Bottom-48,w,40),"editor.cancel",()=>manufacturePlan=null);
        Button(b,new(box.X+24+w,box.Bottom-48,w,40),"manufacturing.confirm",()=>
        {
            // Clear confirmation immediately; queued clicks can't resubmit this plan through old controls.
            manufacturePlan=null;controls.Clear();var recipe=manufacturing?.FindRecipe(product.Design.TemplateId);
            notice=recipe is null?"manufacturing.changed":manufacturing!.Submit(plan,recipe,progress);
        });
    }

    private void DrawSettings(SpriteBatch b,Blueprint design)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var dialog=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,dialog);
        int x=dialog.X+12,w=dialog.Width-24,y=dialog.Y+12;
        var grid=document.ViewSnapshot();
        string[] lines={
            text.Get("editor.settings").ToString(),design.Name,
            text.Get("use."+design.Use)+$" · {grid.Width}×{grid.Height}",
            text.Get("editor.template",new{template=text.Get(FurnitureTemplates.TryGet(design.TemplateId,out var definition)?definition.NameKey:
                WeaponTemplates.TryGet(design.TemplateId,out var weapon)?weapon.NameKey:
                design.TemplateId==ProductTemplates.Hat?"template.hat":"template.unknown")}).ToString(),
            text.Get("editor.direction",new{direction=ViewName(document.View)}).ToString(),
            text.Get(document.IsClothing?"editor.wool-pending":manufacturing?.SingleViewChoices.Count>0?"editor.template-help":"editor.template-pending").ToString()
        };
        foreach(string line in lines){Line(b,line,new(x,y,w,28),ArtResources.Ink);y+=32;}
        int half=(w-12)/2;
        Button(b,new(x,dialog.Bottom-100,half,40),"editor.rename",()=>{settings=false;nameBox.Text=design.Name;naming=true;});
        if(design.Views.Count>1)Button(b,new(x+half+12,dialog.Bottom-100,half,40),"editor.next-direction",()=>
        {
            var views=design.Views.Keys.ToArray();int index=Array.IndexOf(views,document.View);
            document.SetView(views[(index+1)%views.Length]);Center();located=null;
        },design.Views.Count>1);
        else Button(b,new(x+half+12,dialog.Bottom-100,half,40),"editor.change-template",BeginConversion,
            manufacturing?.SingleViewChoices.Count>0);
        Button(b,new(x,dialog.Bottom-52,w,40),"editor.cancel",()=>settings=false);
    }
    private string ViewName(string view) => view is "front" or "right" or "left" or "back"
        ?text.Get("view."+view).ToString():view;

    private void BeginConversion()
    {
        Suspend();conversionChoices=manufacturing?.SingleViewChoices??Array.Empty<TemplateSpec>();
        if(conversionChoices.Count==0)return;
        conversionSource=document.Snapshot();conversionIndex=0;conversionSizing=ImportSizing.Original;cropX=cropY=0;
        settings=false;drawer="";RefreshConversion();
    }
    private void RefreshConversion()
    {
        conversion=null;
        try{conversion=TemplateConversion.Preview(conversionSource!,conversionChoices[conversionIndex],catalog,conversionSizing,cropX,cropY,document.View);}
        catch(ArgumentException){/* Show explicit resize-required state without changing the source. */}
    }
    private void DrawConversion(SpriteBatch b)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var box=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,box);int x=box.X+12,w=box.Width-24,half=(w-8)/2;
        var target=conversionChoices[conversionIndex];var old=conversionSource!.Views[document.View];
        TextButton(b,new(x,box.Y+8,w,36),TemplateName(target.Id)+$" · {target.Width}×{target.Height} →",()=>
        {conversionIndex=(conversionIndex+1)%conversionChoices.Count;conversionSizing=ImportSizing.Original;cropX=cropY=0;RefreshConversion();});
        if(conversion is not null)
        {
            DrawPreview(b,conversion.Candidate.Views.GetValueOrDefault(document.View)??conversion.Candidate.Views["front"],new(x,box.Y+52,w,Math.Max(48,box.Height-260)));
            Line(b,text.Get("editor.conversion-summary",new{unassigned=conversion.UnassignedCells,incompatible=conversion.IncompatibleCells,supplements=conversion.IncompatibleSupplements}).ToString(),
                new(x,box.Bottom-208,w,28),ArtResources.Ink);
        }
        else Line(b,text.Get("import.adjust-size").ToString(),new(x,box.Bottom-208,w,28),Color.DarkRed);
        Button(b,new(x,box.Bottom-168,w,36),"import.size."+conversionSizing,()=>
        {conversionSizing=(ImportSizing)(((int)conversionSizing+1)%3);RefreshConversion();});
        TextButton(b,new(x,box.Bottom-124,half,36),$"X {cropX} + ↻",()=>{cropX=(cropX+1)%old.Width;RefreshConversion();},conversionSizing==ImportSizing.Crop);
        TextButton(b,new(x+half+8,box.Bottom-124,half,36),$"Y {cropY} + ↻",()=>{cropY=(cropY+1)%old.Height;RefreshConversion();},conversionSizing==ImportSizing.Crop);
        Line(b,text.Get(ClothingTemplates.IsClothing(target.Use)||document.IsClothing?"editor.clothing-conversion":"editor.conversion-note").ToString(),new(x,box.Bottom-80,w,28),ArtResources.Ink);
        Button(b,new(x,box.Bottom-44,half,36),"editor.cancel",()=>{conversionSource=null;conversion=null;});
        Button(b,new(x+half+8,box.Bottom-44,half,36),"editor.apply",()=>
        {
            if(conversion is not null && document.ApplyCandidate(conversion.Candidate))
            {
                material=DefaultMaterial(document.Snapshot().Use);
                replaceColorFrom=replaceMaterialFrom=null;replacingMaterial=false;materialPage=0;tool="paint";located=null;Center();
            }
            conversionSource=null;conversion=null;controls.Clear();
        },conversion is not null);
    }

    private void DrawIssues(SpriteBatch b,Blueprint design)
    {
        Fill(b,layout.Frame,Color.Black*0.7f);controls.Clear();
        var dialog=new UiRect(layout.Frame.X+16,layout.Frame.Y+20,layout.Frame.Width-32,layout.Frame.Height-40);
        ArtResources.Panel(b,dialog);
        string heading=design.Name+" · "+text.Get("use."+design.Use)+" · "+ViewName(document.View);
        Line(b,heading,new(dialog.X+12,dialog.Y+12,dialog.Width-24,28),ArtResources.Ink);
        var items=report.Issues.GroupBy(i=>(i.Code,i.View,i.Id)).Select(g=>(Issue:g.First(),Count:g.Count())).ToArray();
        var bill=report.Materials.Select(m=>text.Get(m.IsFree?"creative.amount":"editor.amount",new{material=MaterialName(m.Id),needed=m.Needed,owned=m.Owned,missing=m.Missing}).ToString()).ToArray();
        if(availabilityFailure is not ManufacturingFailure.None and not ManufacturingFailure.InvalidDesign)bill=new[]{text.Get("manufacturing.error."+availabilityFailure).ToString()}.Concat(bill).ToArray();
        int total=items.Length+bill.Length;
        int perPage=Math.Max(1,(dialog.Height-164)/48);
        issuePage=Math.Clamp(issuePage,0,Math.Max(0,(total-1)/perPage));
        if(total==0)Line(b,text.Get("editor.no-issues").ToString(),new(dialog.X+12,dialog.Y+52,dialog.Width-24,40),ArtResources.Ink);
        for(int row=0;row<perPage && issuePage*perPage+row<total;row++)
        {
            int itemIndex=issuePage*perPage+row;
            var rect=new UiRect(dialog.X+12,dialog.Y+48+row*48,dialog.Width-24,40);
            if(itemIndex<bill.Length){Line(b,bill[itemIndex],new(rect.X+6,rect.Y+6,rect.Width-12,28),ArtResources.Ink);continue;}
            var entry=items[itemIndex-bill.Length];var issue=entry.Issue;
            Fill(b,rect,issue.Cell.HasValue?Color.Wheat:Color.White);
            string description=text.Get("issue."+issue.Code).ToString();
            if(issue.Id is not null)description+=" · "+(catalog.Materials.Any(m=>m.Id==issue.Id)?MaterialName(issue.Id):catalog.Colors.FirstOrDefault(c=>c.Id==issue.Id) is {} col?text.Get(col.NameKey).ToString():issue.Id);
            if(issue.View is not null)description+=" · "+ViewName(issue.View);
            if(entry.Count>1)description+=" ×"+entry.Count;
            Line(b,description,new(rect.X+6,rect.Y+6,rect.Width-12,28),ArtResources.Ink);
            if(issue.Cell is int cell && issue.View is string view)
                controls.Add((rect,()=>
                {
                    document.SetView(view);Center();var grid=document.ViewSnapshot();
                    var target=transform.CellRect(cell%grid.Width,cell/grid.Width);
                    transform.Pan(layout.Canvas.X+layout.Canvas.Width/2-target.X-target.Width/2,
                        layout.Canvas.Y+layout.Canvas.Height/2-target.Y-target.Height/2);
                    located=(view,cell);checking=false;drawer="";
                }));
        }
        int half=(dialog.Width-36)/2;
        Button(b,new(dialog.X+12,dialog.Bottom-100,half,36),"palette.adjust",()=>{checking=false;BeginPalette();});
        Button(b,new(dialog.X+24+half,dialog.Bottom-100,half,36),"editor.assign-all",PreviewMaterialAssignment,WeaponMaterials.IsWeapon(design.Use));
        int w=(dialog.Width-48)/3, bottom=dialog.Bottom-52;
        Button(b,new(dialog.X+12,bottom,w,40),"processing.previous",()=>issuePage--,issuePage>0);
        Button(b,new(dialog.X+24+w,bottom,w,40),"processing.next",()=>issuePage++,(issuePage+1)*perPage<total);
        Button(b,new(dialog.X+36+w*2,bottom,w,40),"editor.cancel",()=>checking=false);
    }
    public void Locate(DraftIssue? issue)
    {
        if(issue?.View is not string view || issue.Cell is not int cell)return;
        var design=document.Snapshot();
        if(!DraftStatus.Evaluate(design,catalog,progress,manufacturing).Issues.Contains(issue))return;
        if(!design.Views.TryGetValue(view,out var grid) || cell<0 || cell>=grid.Cells.Count)return;
        document.SetView(view);Center();var target=transform.CellRect(cell%grid.Width,cell/grid.Width);
        transform.Pan(layout.Canvas.X+layout.Canvas.Width/2-target.X-target.Width/2,
            layout.Canvas.Y+layout.Canvas.Height/2-target.Y-target.Height/2);
        located=(view,cell);drawer="";
    }
    private static void Line(SpriteBatch b,string value,UiRect rect,Color color)
        =>ArtResources.TextLine(b,value,rect,color);

    private void DrawPalette(SpriteBatch b,UiRect area)
    {
        Line(b,text.Get("editor.palette"),new(area.X+12,area.Y,112,24),ArtResources.Ink);
        var cells=ColorPickerLayout.FavoriteCells(area);
        for(int i=0;i<16;i++)
        {
            int index=i;var r=cells[i];uint rgba=progress.FavoriteColors[i];string id=BeadPalette.Id(rgba);
            var face=ArtResources.BeadSlot(b,r,color==id,r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY));
            var tray=new UiRect(face.X+3,face.Y+3,face.Width-6,face.Height-6);
            ArtResources.BeadCell(b,tray,tray,ColorOf(rgba),respectDisplaySetting:false);
            if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=$"{i+1}/16 · #{rgba>>8:X6}";
            controls.Add((r,()=>
            {
                if(replaceColorFrom is not null)
                {
                    var temp=new EditorDocument(document.Snapshot(),catalog,document.View);
                    if(temp.ReplaceColor(replaceColorFrom,id)){candidate=temp.Snapshot();candidateNote=null;}
                    replaceColorFrom=null;
                }
                color=id;favoriteIndex=index;
            }));
        }
        uint selected=BeadPalette.Resolve(catalog,color)?.Rgba??0;
        Line(b,$"#{selected>>8:X6}",ColorPickerLayout.FavoriteCaption(area),ArtResources.Ink);
        Button(b,ColorPickerLayout.FavoriteAction(area),"palette.adjust",BeginPalette);
    }
    private void OpenDrawer(string page){Suspend();drawer=page;controls.Clear();}
    private void DrawDrawerTab(SpriteBatch b,UiRect r,string key,string page)
    {
        ArtResources.Tab(b,r,text.Get(key),drawer==page);
        controls.Add((r,()=>OpenDrawer(page)));
    }
    private void DrawToolButton(SpriteBatch b,UiRect r,string name)
    {
        if(layout.UsesDrawers)
        {
            var face=ArtResources.Button(b,r,true,tool==name);
            if(!ArtResources.Icon(b,name,new(face.X+(face.Width-16)/2,face.Y+(face.Height-16)/2,16,16)))
                ArtResources.TextLine(b,name=="paint"?"+":"−",face,ArtResources.Ink,true);
            controls.Add((r,()=>tool=name));
        }
        else Button(b,r,"editor."+name,()=>tool=name,true,tool==name);
        if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=text.Get("editor."+name);
    }
    private void DrawSidebar(SpriteBatch b,UiRect area,Blueprint design)
    {
        var stock=DraftStatus.ReadStock(catalog,manufacturing);
        if(!WeaponMaterials.IsWeapon(design.Use))
        {
            string id=document.IsClothing?"wool":"decoration";
            var amount=report.Materials.FirstOrDefault(m=>m.Id==id);
            var p=MaterialPanelLayout.Calculate(area,design.Views.Count>1);
            Line(b,MaterialName(id),p.Title,ArtResources.Ink);
            Line(b,text.Get("supply.short",new{needed=amount?.Needed??0,owned=stock.GetValueOrDefault(id)}),p.Amount,ArtResources.Ink);
            var range=manufacturing?.SupplyRange;
            Line(b,text.Get("supply.range",new{radius=range?.Radius??5,count=range?.Chests??0}),p.Range,ArtResources.MutedInk);
            Button(b,p.Inspect,"editor.check-cost",()=>{drawer="";checking=true;issuePage=0;});
            if(design.Views.Count>1)TextButton(b,p.View,ViewName(document.View)+" →",NextView);
            return;
        }
        var allowed=catalog.Materials.Where(m=>MaterialRules.CanUse(m,design.Use)).ToArray();
        bool replacing=replacingMaterial && area==layout.DrawerContent;
        var picker=PickerLayout.Materials(area,allowed.Length,materialPage,replacing);
        if(drawer.Length==0 || area==layout.DrawerContent)materialPage=picker.Page;
        Line(b,text.Get("editor.materials"),picker.Caption,ArtResources.Ink);
        if(replacing)
        {
            var sources=document.ViewSnapshot().Cells.Where(c=>c is not null).Select(c=>c!.MaterialId).Distinct().ToArray();
            if(!sources.Contains(replaceMaterialFrom))replaceMaterialFrom=sources.FirstOrDefault();
            TextButton(b,picker.Source,text.Get("editor.material-source",new{material=MaterialName(replaceMaterialFrom)}),()=>
                {replaceMaterialFrom=sources[(Array.IndexOf(sources,replaceMaterialFrom)+1)%sources.Length];},sources.Length>0);
        }
        foreach(var (index,r) in picker.Cells)
        {
            var entry=allowed[index];var face=ArtResources.Button(b,r,true,material==entry.Id);
            string count=stock.GetValueOrDefault(entry.Id).ToString();
            Line(b,MaterialName(entry.Id),new(face.X+12,face.Y+8,face.Width-76,24),ArtResources.Ink);
            ArtResources.TextLine(b,count,new(face.Right-56,face.Y+8,44,24),ArtResources.Ink,true);
            controls.Add((r,()=>
            {
                if(replacing)
                {
                    var temp=new EditorDocument(document.Snapshot(),catalog,document.View);
                    int count=temp.CountMaterial(replaceMaterialFrom);
                    if(temp.ReplaceMaterial(replaceMaterialFrom,entry.Id))
                    {
                        candidate=temp.Snapshot();candidateNote=text.Get("editor.material-replacement",new{
                            source=MaterialName(replaceMaterialFrom),target=MaterialName(entry.Id),count});
                    }
                    replaceMaterialFrom=null;replacingMaterial=false;drawer="";
                }
                material=entry.Id;
            }));
            if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=text.Get("editor.material-stock",new{material=MaterialName(entry.Id),count=stock.GetValueOrDefault(entry.Id)});
        }
        TextButton(b,picker.Previous,"<",()=>materialPage--,picker.Page>0);
        TextButton(b,picker.Next,">",()=>materialPage++,picker.Page+1<picker.Pages);
    }
    private void DrawMoreTools(SpriteBatch b,UiRect area)
    {
        ArtResources.Panel(b,area);
        var current=document.Snapshot();
        var names=new[]{"patterns","valuable-sources","undo","redo","import","weapon-details","prepare","fitting","material","assign-all","copy-directions","pick","fill","center","settings","save-as","mirror-h","mirror-v","replace-color","replace-material","palette","overlay","reference","grid","help"}
#if BEADS_LITE
            .Where(n=>n!="patterns")
#endif
            .Where(n=>n!="prepare" || WeaponMaterials.IsWeapon(current.Use))
            .Where(n=>!(!WeaponMaterials.IsWeapon(current.Use) && n is "material" or "replace-material" or "assign-all" or "overlay"))
            .Where(n=>n!="fitting" || document.IsClothing)
            .Where(n=>n!="copy-directions" || current.Views.Count>1)
            .Where(n=>n!="weapon-details" || WeaponMaterials.IsWeapon(current.Use)).ToArray();
        int rows=Math.Max(1,(area.Height-120)/52),start=Math.Clamp(toolScroll/52,0,Math.Max(0,names.Length-rows));toolScroll=start*52;
        for(int i=0;i<rows && start+i<names.Length;i++)
        {
            string name=names[start+i];var r=new UiRect(area.X+12,area.Y+60+i*52,area.Width-24,44);
            bool enabled=name=="undo"?document.CanUndo:name=="redo"?document.CanRedo:true;
            if(name=="valuable-sources")TextButton(b,r,ContentText.Get("copy.EditorPanel.5e8ad223a1","花卉／硬木取料：")+(PlayMode.AllowValuableSources?ContentText.Get("copy.EditorPanel.ce7ef28b67","允许"):ContentText.Get("copy.EditorPanel.0f810a7901","保护")),()=>Action(name));
            else Button(b,r,"editor."+name,()=>Action(name),enabled,tool==name);
        }
        int half=(area.Width-24)/2;
        TextButton(b,new(area.X+8,area.Bottom-52,half,40),"<",()=>toolScroll=Math.Max(0,start-rows)*52,start>0);
        TextButton(b,new(area.X+16+half,area.Bottom-52,half,40),">",()=>toolScroll=(start+rows)*52,start+rows<names.Length);
        Line(b,text.Get("editor.more"),new(area.X+16,area.Y+12,120,40),ArtResources.Ink);
        Button(b,layout.DrawerClose,"editor.drawer-close",()=>drawer="");
    }

    private void Action(string name)
    {
        switch(name)
        {
#if !BEADS_LITE
            case "patterns":drawer="";WorkshopService.Current?.ShowPatterns(Game1.activeClickableMenu);break;
#endif
            case "valuable-sources":PlayMode.ToggleValuableSources();break;
            case "wool-budget":candidate=document.Snapshot();candidate.WoolMaterials=new(){{"wool",manufacturing?.FindRecipe(candidate.TemplateId)?.Template.WoolBudget??0}};candidateNote=text.Get("editor.wool-pending");drawer="";break;
            case "fitting":BeginFitting();break;
            case "save-as":SaveAs();break;
            case "settings":drawer="";settings=true;break;
            case "center":Center();drawer="";break;
            case "help":drawer="";help=true;break;
            case "assign-all":PreviewMaterialAssignment();break;
            case "copy-directions":candidate=document.Snapshot();foreach(var v in candidate.Views.Keys.ToArray())candidate.Views[v]=document.ViewSnapshot();candidateNote=text.Get("editor.copy-directions-note");drawer="";break;
            case "weapon-details":BeginWeaponPanel();break;
            case "grid":gridLines=!gridLines;break;
            case "import":BeginImport();break;
            case "undo":document.Undo();Center();break; case "redo":document.Redo();Center();break;
            case "mirror-h":document.Mirror(true);break; case "mirror-v":document.Mirror(false);break;
            case "palette":BeginPalette();break; case "overlay":materialsOverlay=!materialsOverlay;break;
            case "prepare":BeginPreparation();break;
            case "reference":referenceVisible=!referenceVisible;break; default:tool=name;drawer="";break;
            case "replace-color":replaceColorFrom=color;replaceMaterialFrom=null;replacingMaterial=false;drawer="";break;
            case "replace-material":
                var sources=document.ViewSnapshot().Cells.Where(c=>c is not null).Select(c=>c!.MaterialId).Distinct().ToArray();
                replacingMaterial=sources.Length>0;replaceMaterialFrom=sources.FirstOrDefault();replaceColorFrom=null;
                drawer="materials";materialPage=0;
                if(!replacingMaterial)notice="issue.empty";
                break;
        }
    }
    private string MaterialName(string? id)=>id is null?text.Get("editor.material-unassigned").ToString():
        catalog.Materials.Any(m=>m.Id==id)?text.Get("material."+id).ToString():id;
    private string QualityName(int quality)=>text.Get(quality switch{1=>"supply.silver",2=>"supply.gold",4=>"supply.iridium",_=>"supply.regular"});
    private void Button(SpriteBatch b,UiRect rect,string key,Action action,bool enabled=true,bool active=false)
        =>TextButton(b,rect,text.Get(key).ToString(),action,enabled,active,key);
    private void TextButton(SpriteBatch b,UiRect rect,string label,Action action,bool enabled=true,bool active=false,string iconKey="")
    {
        ArtResources.ButtonText(b,rect,label,enabled,active,iconKey);
        if(rect.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY) && Game1.smallFont.MeasureString(label).X*ArtResources.TextHeight(true)/Game1.smallFont.MeasureString(ContentText.Get("copy.EditorPanel.16e7a38c73","国Ag")).Y>rect.Width-24)hoverText=label;
        if(enabled) controls.Add((rect,action));
    }
    private static void DrawPreview(SpriteBatch b,BeadGrid grid,UiRect area)
    {
        float scale=Math.Min(area.Width/(float)grid.Width,area.Height/(float)grid.Height);
        for(int y=0;y<grid.Height;y++) for(int x=0;x<grid.Width;x++) if(grid.Cells[y*grid.Width+x] is {} cell)
            FillClipped(b,new(area.X+(int)(x*scale),area.Y+(int)(y*scale),Math.Max(1,(int)Math.Ceiling(scale)),Math.Max(1,(int)Math.Ceiling(scale))),ColorOf(cell.Rgba),area);
    }
    private void Label(SpriteBatch b,string key,UiRect r)=>b.DrawString(Game1.smallFont,Game1.parseText(text.Get(key).ToString(),Game1.smallFont,Math.Max(1,r.Width-20)),new(r.X+10,r.Y+10),ArtResources.Ink);
    private static Color ColorOf(uint rgba)=>new((byte)(rgba>>24),(byte)(rgba>>16),(byte)(rgba>>8),(byte)rgba);
    private static void Fill(SpriteBatch b,UiRect r,Color color){if(r.Width>0 && r.Height>0)b.Draw(Game1.staminaRect,new Rectangle(r.X,r.Y,r.Width,r.Height),color);}
    private static void FillClipped(SpriteBatch b,UiRect r,Color color,UiRect clip)
    {
        int x=Math.Max(r.X,clip.X),y=Math.Max(r.Y,clip.Y);
        Fill(b,new(x,y,Math.Max(0,Math.Min(r.Right,clip.Right)-x),Math.Max(0,Math.Min(r.Bottom,clip.Bottom)-y)),color);
    }
}




