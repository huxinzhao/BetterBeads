#if BEADS_LITE
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
    private readonly ManufacturingService? manufacturing;
    private readonly CanvasTransform transform=new();
    private readonly FrameControls controls=new();
    private readonly ScenePreviewPanel scene=new();
    private readonly VersionedSnapshotCache<Blueprint> display=new();
    private readonly TextBox nameBox;
    private readonly TextBox paletteSearchBox;
    private EditorLayout layout=null!;
    private UiRect[] toolButtons=Array.Empty<UiRect>();
    private string color,drawer="",tool="paint",selector="";
    private string? hoverText;
    private string noticeText="";
    private double noticeUntil;
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private string notice {get=>Now<noticeUntil?noticeText:"";set{noticeText=value;noticeUntil=double.PositiveInfinity;}}
    private bool panning;
    private bool touchMoveMode;
    private int panX,panY;
    private ManufacturingRecipe? cachedRecipe;
    private string recipeTemplate="";
    private Blueprint? confirmationDesign;
    public string FeedbackScope=>recoveryPrompt is not null?(recoveryDeleteConfirm?"recovery/delete":"recovery"):patternMoveOpen?"pattern/move":scene.Active?scene.FeedbackScope:paletteSearchOpen?"dye/search":paletteDraft is not null?(backgroundPage?"dye/background":workColorsPage?"dye/work":"dye/common"):naming?"naming":selector.Length>0?"selector/"+selector:clearConfirm?"clear":pendingConversion is not null?"conversion":plan is not null?"confirmation":drawer.Length>0?"drawer":"editor";
    private bool waitRelease,clearConfirm;
    private Blueprint? pendingConversion;
    private int pendingClippedBeads;
    private bool naming;
    private string namingError="";
    private string? approvedNewId;
    private Action? afterNamed;
    private Action? namingCanceled;
    private (long Version,bool Creative)? availabilityKey;
    private ManufacturingAvailability? cachedAvailability;
    private int cachedOwned;
    private long costVersion=-1;
    private readonly WeaponGeometryCache weaponAnalysis=new();
    private (WeaponStatValues Stats,double Reach,int SpeedShift) WeaponMetrics(Blueprint d)
    {
        var shaped=weaponAnalysis.Get(document.ChangeVersion,d);
        return(shaped.Stats,shaped.Reach,shaped.Stats.Speed);
    }
    private (string Item,int Count) cachedCost;
    private (string Item,int Count) DisplayCost(Blueprint d)
    {
        if(costVersion!=document.ChangeVersion){cachedCost=SimpleCrafting.Cost(d);costVersion=document.ChangeVersion;}
        return cachedCost;
    }
    private long nextAvailabilityRefresh;
    private ManufacturingPlan? plan;
    private bool rightStroke;
    public event Action<string>? Saved;
    public event Action<ManufacturingResult>? Completed;
    public EditorDocument Document=>document;
    public UiRect? HoverControl(int x,int y)=>scene.Active?scene.Hover(x,y):controls.HitTest(x,y);
    public IReadOnlyList<UiRect> ControllerTargets=>scene.Active?scene.ControllerTargets:
        paletteSearchOpen?new[]{PaletteSearchLayout.Calculate(layout.Frame).Search}.Concat(controls.Targets).ToArray():
        naming?new[]{new UiRect(nameBox.X,nameBox.Y,nameBox.Width,44)}.Concat(controls.Targets).ToArray():
        paletteDraft is not null&&paletteLayout is not null?controls.Targets.Concat(new[]{paletteLayout.SaturationValue,paletteLayout.Hue}).ToArray():controls.Targets;
    public bool HasModal=>recoveryPrompt is not null||patternMoveOpen||scene.Active||selector.Length>0||paletteDraft is not null||clearConfirm||pendingConversion is not null||plan is not null||naming;
    public bool IsNaming=>naming;
    public void ShowNotice(string message)=>notice=message;
    public bool HasOverlay=>HasModal||drawer.Length>0;
    private string T(string key,string fallback)=>ContentText.Get("simple."+key,fallback);
    public EditorPanel(EditorDocument document,ProcessingCatalog catalog,SaveProgress progress,ITranslationHelper text,ReferenceReader reader,ManufacturingService? manufacturing)
    {
        this.document=document;this.catalog=catalog;this.progress=progress;this.text=text;this.manufacturing=manufacturing;
        nameBox=new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=""};
        paletteSearchBox=new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=""};
        progress.FavoriteColors=BeadPalette.Normalize(progress.FavoriteColors);color=BeadPalette.Id(progress.FavoriteColors[0]);Normalize();
    }
    private void Normalize(){if(SimpleCrafting.Supported(document.Snapshot()))document.StageMigration(SimpleCrafting.Normalize(document.Snapshot()));}
    public void Layout(EditorLayout value)
    {
        Suspend();layout=value;toolButtons=SimpleEditorLayout.ToolButtons(layout);
        var size=document.ViewSize;transform.Relayout(layout.Canvas,size.Width,size.Height);controls.Clear();paletteLayout=null;paletteDrag="";drawer="";availabilityKey=null;
    }
    private void Center(){var (width,height)=document.ViewSize;transform.Center(width,height);}
    public void Suspend(){document.EndStroke();padStroke=false;panning=false;waitRelease=true;}
    public void CloseFitting()=>scene.Close();
    public void Locate(DraftIssue? issue){}
    public void BeginImport(){} // Shared full-version host contract; no Lite entry point.
    public void ResetForOpenedDesign(){ClosePalette();EndNaming();CancelPattern();Suspend();plan=null;clearConfirm=false;pendingConversion=null;drawer="";selector="";approvedNewId=null;padCellInitialized=false;if(tool=="picker")tool="paint";Normalize();Center();}
    private bool NeedsName()
    {
        var d=document.Snapshot();return d.Revision==0&&(approvedNewId!=d.Id||string.IsNullOrWhiteSpace(d.Name));
    }
    private void BeginNaming(Action continuation,Action? canceled=null)
    {
        Suspend();naming=true;afterNamed=continuation;namingCanceled=canceled;namingError="";
        nameBox.Text=document.Snapshot().Name;nameBox.Selected=true;
        Game1.keyboardDispatcher.Subscriber=nameBox;
    }
    private void EndNaming()
    {
        if(ReferenceEquals(Game1.keyboardDispatcher.Subscriber,nameBox))Game1.keyboardDispatcher.Subscriber=null;
        nameBox.Selected=false;naming=false;namingError="";afterNamed=null;namingCanceled=null;Suspend();
    }
    private void CancelNaming(){var canceled=namingCanceled;EndNaming();canceled?.Invoke();}
    private void ConfirmNaming()
    {
        string name=nameBox.Text.Trim();
        if(name.Length is <1 or >24){namingError=T("name-length","请输入1至24个字符的图纸名称");return;}
        document.Rename(name);approvedNewId=document.Snapshot().Id;
        var continuation=afterNamed;EndNaming();continuation?.Invoke();
    }
    public bool Save(Action? afterSaved=null,Action? onCanceled=null,Action? onFailed=null)
    {
        Suspend();
        var source=document.Snapshot();
        var decision=BlueprintManualSave.Prepare(progress,source);
        if(decision.Kind is BlueprintSaveKind.Reused or BlueprintSaveKind.Unchanged)
        {
            var existing=decision.Existing!;
            if(decision.Kind==BlueprintSaveKind.Reused)document.AdoptSaved(existing);
            else document.MarkSaved(existing.Revision);
            Saved?.Invoke(existing.Id);
            notice=decision.Kind==BlueprintSaveKind.Unchanged?T("unchanged","没有需要保存的修改"):T("already-exists","已有相同图纸：")+existing.Name;
            noticeUntil=Now+2;afterSaved?.Invoke();return true;
        }
        if(decision.Kind==BlueprintSaveKind.Conflict)
        {notice=text.Get("library.conflict");onFailed?.Invoke();return false;}
        if(NeedsName()){BeginNaming(()=>Save(afterSaved,onCanceled,onFailed),onCanceled);return false;}
        var save=decision.Change;
        if(save is null){notice=text.Get("library.conflict");onFailed?.Invoke();return false;}
        if(Context.IsMultiplayer&&!Context.IsMainPlayer&&OnlineSession.Current is {} online)
        {
            var previous=new Dictionary<string,string>(progress.BlueprintRecords);
            try{save.Apply(progress);}catch(InvalidOperationException){onFailed?.Invoke();return false;}
            online.Sync(ok=>
            {
                if(!ok){progress.BlueprintRecords=previous;notice=text.Get("editor.save-failed");onFailed?.Invoke();return;}
                document.MarkSaved(save.Saved.Revision);Saved?.Invoke(save.Id);notice=T("saved","图纸已保存");noticeUntil=Now+2;afterSaved?.Invoke();
            });return false;
        }
        try{save.Apply(progress);}
        catch(InvalidOperationException){notice=text.Get("library.conflict");onFailed?.Invoke();return false;}
        document.MarkSaved(save.Saved.Revision);Saved?.Invoke(save.Id);notice=T("saved","图纸已保存");noticeUntil=Now+2;afterSaved?.Invoke();return true;
    }
    public bool SaveAs()=>Save();
    private void RequestDesign(string id)
    {
        var current=document.Snapshot();
        if(current.TemplateId==id)return;
        Suspend();
        var candidate=SimpleDesignConversion.Convert(current,id,out int clipped);
        if(clipped>0){pendingConversion=candidate;pendingClippedBeads=clipped;return;}
        ApplyConversion(candidate);
    }
    private void ApplyConversion(Blueprint candidate)
    {
        if(document.ApplyCandidate(candidate)){notice="";availabilityKey=null;Center();}
        pendingConversion=null;Suspend();
    }
    public void Click(int x,int y,bool right=false)
    {
        if(scene.Active){if(!right)scene.Click(x,y);Suspend();return;}
        if(layout.TooSmall||!controls.IsReady)return;
        if(paletteSearchOpen)
        {
            if(!right && PaletteSearchLayout.Calculate(layout.Frame).Search.Contains(x,y))
            {paletteSearchBox.Selected=true;Game1.keyboardDispatcher.Subscriber=paletteSearchBox;return;}
            if(!right)controls.TryInvoke(x,y);
            return;
        }
        if(naming&&new UiRect(nameBox.X,nameBox.Y,nameBox.Width,48).Contains(x,y))
        {nameBox.Selected=true;Game1.keyboardDispatcher.Subscriber=nameBox;return;}
        if(paletteDraft is not null && !right && PaletteClick(x,y))return;
        if(!right && controls.TryInvoke(x,y)){Suspend();return;}
        if(HasOverlay||waitRelease||!layout.Canvas.Contains(x,y))return;
        if(!right&&(Keyboard.GetState().IsKeyDown(Keys.Space)||WorkbenchInput.Touch&&touchMoveMode)){panning=true;panX=x;panY=y;return;}
        var (width,height)=document.ViewSize;
        if(transform.TryCell(x,y,width,height,out int cx,out int cy))
        {
            rightStroke=right;
            if(!UseToolAt(cx,cy,right&&tool!="picker"))Suspend();
        }
    }
    // Mouse, touch and controller use the same per-cell action, so the picker can never paint.
    private bool UseToolAt(int x,int y,bool erase)
    {
        if(patternSelecting){if(erase)CancelPattern();else SelectPatternCell(x,y);return false;}
        if(tool=="picker"&&!erase)
        {
            if(document.TrySampleColor(x,y,out var sampled))
            {color=sampled;tool="paint";notice=T("color-picked","已吸取画板颜色");noticeUntil=Now+2;}
            else notice=T("pick-filled","请选择画板上已有的豆子");
            return false;
        }
        var d=document.Snapshot();string metal=SimpleCrafting.IsWoodwork(d.Use)?"decoration":SimpleCrafting.Metal(d);
        if(metal.Length==0){notice=T("choose-metal","请先选择铜、铁或铱");return false;}
        long before=document.ChangeVersion;
        notice="";
        bool started=document.BeginStroke(x,y,erase||tool=="erase"?BrushTool.Erase:BrushTool.Paint,color,metal);
        if(started&&!erase&&tool!="erase"&&document.ChangeVersion!=before)UiSounds.BeadPlaced();
        return started;
    }
    public void Update()
    {
        var mouse=Mouse.GetState();int x=WorkbenchUi.MouseX,y=WorkbenchUi.MouseY;
        if(mouse.LeftButton==ButtonState.Released&&mouse.RightButton==ButtonState.Released&&mouse.MiddleButton==ButtonState.Released)waitRelease=false;
        if(patternSelecting&&patternAnchor is not null&&!WorkbenchInput.Controller)
        {
            if(transform.TryCell(x,y,document.ViewSize.Width,document.ViewSize.Height,out int sx,out int sy))patternEnd=new(sx,sy);
            if(mouse.LeftButton==ButtonState.Released)FinishPatternSelection();
            return;
        }
        if(paletteDraft is not null){if(mouse.LeftButton==ButtonState.Released)paletteDrag="";else if(paletteDrag.Length>0)UpdatePaletteColor(x,y);return;}
        if(padStroke)return;
        bool panHeld=mouse.MiddleButton==ButtonState.Pressed||(mouse.LeftButton==ButtonState.Pressed&&(Keyboard.GetState().IsKeyDown(Keys.Space)||WorkbenchInput.Touch&&touchMoveMode));
        if(!HasOverlay&&!waitRelease&&panHeld&&(panning||layout.Canvas.Contains(x,y)))
        {
            document.EndStroke();if(panning)transform.Pan(x-panX,y-panY);
            panning=true;panX=x;panY=y;return;
        }
        if(panning){panning=false;Suspend();return;}
        if(!document.IsStrokeActive)return;
        if(HasOverlay||(rightStroke?mouse.RightButton:mouse.LeftButton)==ButtonState.Released){document.EndStroke();return;}
        long before=document.ChangeVersion;
        var (width,height)=document.ViewSize;if(transform.TryCell(x,y,width,height,out int cx,out int cy))document.ContinueStroke(cx,cy);else document.EndStroke();
        if(!rightStroke&&tool=="paint"&&document.ChangeVersion!=before)UiSounds.BeadPlaced();
    }
    public void Scroll(int direction){if(!HasOverlay&&!panning)transform.ZoomAt(WorkbenchUi.MouseX,WorkbenchUi.MouseY,direction);}
    public bool Key(Keys key)
    {
        if(recoveryPrompt is not null){if(key==Keys.Escape){if(recoveryDeleteConfirm)recoveryDeleteConfirm=false;else DeferRecovery();}return true;}
        if(patternMoveOpen){if(key==Keys.Escape)CancelPattern();return true;}
        if(patternSelecting&&key==Keys.Escape){CancelPattern();return true;}
        if(scene.Active){if(key==Keys.Escape){scene.Close();Suspend();}return true;}
        if(paletteSearchOpen)
        {
            if(key==Keys.Escape)ClosePaletteSearch();
            else if(key==Keys.Enter)SelectExactPaletteSearch();
            return true;
        }
        if(naming)
        {
            if(key==Keys.Escape)CancelNaming();
            else if(key==Keys.Enter)ConfirmNaming();
            return true;
        }
        if(key==Keys.Escape && HasOverlay){ClosePalette();drawer="";selector="";clearConfirm=false;pendingConversion=null;plan=null;Suspend();return true;}
        if(HasOverlay)return true;
        if(key==Keys.Home){Center();Suspend();return true;}
        if(key==Keys.Escape && tool=="picker"){tool="paint";notice="";Suspend();return true;}
        if(Keyboard.GetState().IsKeyDown(Keys.LeftControl)||Keyboard.GetState().IsKeyDown(Keys.RightControl))
        {if(key==Keys.S){Save();return true;}if(key==Keys.Z){UndoDesign();return true;}if(key==Keys.Y){RedoDesign();return true;}}
        return false;
    }
    public void DrawHover(SpriteBatch b){if(hoverText is not null&&(!HasModal||paletteDraft is not null))ArtResources.Hint(hoverText);}
    public void Draw(SpriteBatch b)
    {
        ArtResources.Scope="editor";controls.Clear();hoverText=null;if(layout.TooSmall)return;
        var d=display.Get(document.ChangeVersion,document.Snapshot);var g=d.Views["front"];
        TextButton(b,SimpleEditorLayout.Category(layout),T("category-button","品类")+"："+CategoryPath(d.Use),()=>selector="category");
        TextButton(b,SimpleEditorLayout.Size(layout),ContentText.Format("simple.size-label",$"选择尺寸：{g.Width}*{g.Height}px"),()=>selector="size");
        if(layout.UsesDrawers)TextButton(b,SimpleEditorLayout.SupplyButton(layout),T("supply-tab","豆色与用料"),()=>{drawer="supply";Suspend();});
        var tools=toolButtons;
        TextButton(b,tools[0],T("needle","拼豆针"),()=>{CancelPattern();tool="paint";notice="";},active:tool=="paint");
        TextButton(b,tools[1],T("tweezers","取豆镊"),()=>{CancelPattern();tool="erase";},active:tool=="erase");
        ArrowButton(b,tools[2],false,UndoDesign,document.CanUndo);
        ArrowButton(b,tools[3],true,RedoDesign,document.CanRedo);
        TextButton(b,tools[4],T("clear-canvas","清空画布"),()=>clearConfirm=true,g.Cells.Any(c=>c is not null));
        ArtResources.Panel(b,layout.Board,"board");Fill(b,layout.Canvas,SimpleCrafting.IsDecoration(d.Use)?ColorOf(d.BackgroundRgba):new Color(232,219,196));
        for(int gy=0;gy<g.Height;gy++)for(int gx=0;gx<g.Width;gx++)
        {
            var r=transform.CellRect(gx,gy);var cell=g.Cells[gy*g.Width+gx];
            if(cell is not null){if(!ArtResources.BeadCell(b,r,layout.Canvas,ColorOf(cell.Rgba))){int l=Math.Max(r.X,layout.Canvas.X),t=Math.Max(r.Y,layout.Canvas.Y);Fill(b,new(l,t,Math.Min(r.Right,layout.Canvas.Right)-l,Math.Min(r.Bottom,layout.Canvas.Bottom)-t),ColorOf(cell.Rgba));}}
            else {int peg=Math.Max(1,transform.Zoom/7);var dot=new UiRect(r.X+r.Width/2,r.Y+r.Height/2,peg,peg);if(layout.Canvas.Contains(dot))Fill(b,dot,new Color(173,148,116));}
        }
        DrawCellHint(b,g.Width,g.Height);
        DrawControllerCursor(b);
        DrawPatternSelection(b);
        if(recipeTemplate!=d.TemplateId){cachedRecipe=manufacturing?.FindRecipe(d.TemplateId);recipeTemplate=d.TemplateId;}
        var available=Availability(d,cachedRecipe);
        string status=notice.Length>0?notice:CostText(d,cachedOwned);
        var costBox=SimpleEditorLayout.CostBox(layout);ArtResources.DialogueBox(b,costBox);
        if(SimpleCrafting.IsWeapon(d.Use) && SimpleCrafting.Metal(d) is {} metal && metal.Length>0)
        {
            var metrics=WeaponMetrics(d);var stats=metrics.Stats;
            int lineHeight=layout.UsesDrawers?20:26;
            Line(b,status,new(costBox.X+12,costBox.Y+4,costBox.Width-24,lineHeight),ArtResources.Ink);
            Line(b,T("damage","伤害")+$" {stats.MinDamage}～{stats.MaxDamage} · "+T("speed-short","攻速")+$" {metrics.SpeedShift:+0;-0;0} · "+T("reach-short","范围")+$" ×{metrics.Reach:0.#}",new(costBox.X+12,costBox.Y+4+lineHeight,costBox.Width-24,lineHeight),ArtResources.Ink);
        }
        else Line(b,status,new(costBox.X+12,costBox.Y+8,costBox.Width-24,costBox.Height-16),ArtResources.Ink);
        TextButton(b,layout.SaveButton,T("save","保存图纸"),()=>Save(),hint:"Ctrl+S");
        TextButton(b,layout.MakeButton,T("make","熨烫"),()=>BeginMake(),available?.CanMake==true,iconKey:"primary",hint:available?.CanMake==true?null:MakeReason(d,available));
        if(!layout.UsesDrawers)DrawSupply(b,layout.Sidebar,d,available);
        if(drawer.Length>0 && !HasModal)
        {ArtResources.Scope=FeedbackScope;hoverText=null;Fill(b,layout.Frame,Color.Black*0.5f);controls.Clear();ArtResources.Panel(b,layout.Drawer);TextButton(b,layout.DrawerClose,T("close","关闭"),()=>drawer="");DrawSupply(b,layout.DrawerContent,d,available);}
        if(recoveryPrompt is not null){DrawRecovery(b);return;}
        if(patternMoveOpen){DrawPatternMove(b);return;}
        if(scene.Active){hoverText=null;scene.Draw(b,layout.Frame,Suspend);return;}
        if(paletteDraft is not null){DrawPaletteEditor(b);return;}
        if(naming){DrawNaming(b);return;}
        if(selector.Length>0)DrawSelector(b,d);
        else if(clearConfirm)Dialog(b,T("clear-confirm","清空当前画布？清空后可以撤销。"),new[]{(T("cancel","取消"),(Action)(()=>clearConfirm=false)),(T("clear","清空"),(Action)(()=>{document.ClearCanvas();clearConfirm=false;Suspend();}))});
        else if(pendingConversion is not null)Dialog(b,ContentText.Format("simple.switch-trim",$"边缘 {pendingClippedBeads} 颗豆子将被裁掉，可撤销。"),new[]{(T("cancel","取消"),(Action)(()=>pendingConversion=null)),(T("switch-keep-center","保留中心并切换"),(Action)(()=>ApplyConversion(pendingConversion!)))});
        else if(plan is not null)DrawConfirmation(b);
    }
    private ManufacturingAvailability? Availability(Blueprint d,ManufacturingRecipe? recipe)
    {
        var key=(document.ChangeVersion,PlayMode.Creative);
        long now=System.Diagnostics.Stopwatch.GetTimestamp();
        if(availabilityKey!=key||now>=nextAvailabilityRefresh)
        {
            cachedAvailability=recipe is null?null:manufacturing!.CheckAvailability(d,recipe,progress);
            cachedOwned=cachedAvailability?.AvailableRaw??0;
            availabilityKey=key;
            nextAvailabilityRefresh=now+System.Diagnostics.Stopwatch.Frequency/4;
        }
        return cachedAvailability;
    }
    private string MakeReason(Blueprint d,ManufacturingAvailability? available)
    {
        if(!d.Views["front"].Cells.Any(c=>c is not null))return T("place-first","请先摆豆");
        var cost=DisplayCost(d);
        if(cost.Item.Length==0)return T("choose-metal","请先选择铜、铁或铱");
        if(available?.Failure is ManufacturingFailure.InvalidInventory or ManufacturingFailure.FeatureLocked)return text.Get("manufacturing.error."+available.Failure).ToString();
        if(!PlayMode.Creative&&cachedOwned<cost.Count)return ContentText.Format("simple.missing-raw",$"还缺 {cost.Count-cachedOwned} 个{ItemName(cost.Item)}");
        if(available?.Failure==ManufacturingFailure.NoSpace)return T("no-output-space","背包无法接收成品，请腾出空位");
        return text.Get("manufacturing.error."+(available?.Failure??ManufacturingFailure.InvalidRecipe)).ToString();
    }
    private void DrawCellHint(SpriteBatch b,int width,int height)
    {
        if(WorkbenchInput.Touch||HasOverlay||waitRelease||!ArtResources.InteractiveScope)return;
        int x=WorkbenchUi.MouseX,y=WorkbenchUi.MouseY;
        bool pan=panning||Keyboard.GetState().IsKeyDown(Keys.Space);
        if(!transform.TryCell(x,y,width,height,out int cx,out int cy))return;
        var r=transform.CellRect(cx,cy);
        var ink=pan?new Color(76,123,153):tool=="erase"||rightStroke&&document.IsStrokeActive?new Color(178,77,59):new Color(255,244,188);
        int l=Math.Max(r.X,layout.Canvas.X),t=Math.Max(r.Y,layout.Canvas.Y),right=Math.Min(r.Right,layout.Canvas.Right),bottom=Math.Min(r.Bottom,layout.Canvas.Bottom);
        Fill(b,new(l,t,right-l,1),ink);Fill(b,new(l,bottom-1,right-l,1),ink);
        Fill(b,new(l,t,1,bottom-t),ink);Fill(b,new(right-1,t,1,bottom-t),ink);
        if(pan)ArtResources.Hint(T("pan-hint","平移画布 · Home 适应画布"));
    }
    private string CostText(Blueprint d,int owned)
    {
        var cost=DisplayCost(d);if(cost.Item.Length==0)return T("choose-metal","请先选择铜、铁或铱");
        if(PlayMode.Creative)return T("creative","创造模式 · 免费制作");
        return ContentText.Format("simple.cost-summary",$"*预计消耗{ItemName(cost.Item)}{cost.Count}个，当前可用{owned}个。");
    }
    internal static string ItemName(string id)=>ContentText.Get("simple.item."+id,id switch{"(O)388"=>"木材","(O)334"=>"铜锭","(O)335"=>"铁锭","(O)337"=>"铱锭",_=>id});
    private void DrawSupply(SpriteBatch b,UiRect area,Blueprint d,ManufacturingAvailability? availability)
    {
        bool weapon=SimpleCrafting.IsWeapon(d.Use),decoration=SimpleCrafting.IsDecoration(d.Use);
        ArtResources.Panel(b,area,"inset");int cell=SimpleEditorLayout.PaletteCell(area,weapon,decoration),gridWidth=4*cell+24,left=area.X+(area.Width-gridWidth)/2;
        for(int i=0;i<BeadPalette.Defaults.Length;i++)
        {int index=i;var r=new UiRect(left+i%4*(cell+8),area.Y+8+i/4*(cell+8),cell,cell);var rgba=progress.FavoriteColors[i];
            var face=ArtResources.BeadSlot(b,r,color==BeadPalette.Id(rgba),r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY));
            ArtResources.PaletteBead(b,face,ColorOf(rgba));
            controls.Add((WorkbenchInput.Touch?SimpleEditorLayout.TouchPaletteHit(r):r,()=>{favoriteIndex=index;color=BeadPalette.Id(rgba);if(layout.UsesDrawers){drawer="";Suspend();}}));}
        var actions=SimpleEditorLayout.ColorActions(area,weapon,decoration);
        if(WorkbenchInput.Touch)
        {
            var touch=SimpleEditorLayout.TouchColorActions(area,weapon,decoration);
            PickerButton(b,touch.Picker);
            TextButton(b,touch.Dye,T("dye","拼豆染色"),BeginPalette);
            TextButton(b,touch.View,T("touch-view","视图"),()=>{drawer="";selector="view";Suspend();},active:touchMoveMode);
        }
        else
        {
            PickerButton(b,actions.Picker);
            TextButton(b,actions.Dye,T("dye","拼豆染色"),BeginPalette);
        }
        int y=actions.Dye.Y+52;string selectedMetal=SimpleCrafting.IsWeapon(d.Use)?SimpleCrafting.Metal(d):"";
        if(SimpleCrafting.IsDecoration(d.Use))
        {
            var background=new UiRect(area.X+16,y,area.Width-32,40);
            TextButton(b,background,T("background","底色")+$" #{d.BackgroundRgba>>8:X6}",BeginBackgroundPalette);
            y+=48;
        }
        if(SimpleCrafting.IsWeapon(d.Use))
        {
            int metalWidth=Math.Max(148,gridWidth),metalLeft=area.X+(area.Width-metalWidth)/2,width=(metalWidth-16)/3;
            for(int i=0;i<3;i++)
            {
                string metal=SimpleCrafting.Metals[i],label=T("metal."+metal,metal switch{"copper"=>"铜","iron"=>"铁",_=>"铱"});
                var button=new UiRect(metalLeft+i*(width+8),y,i==2?metalWidth-2*(width+8):width,40);
                bool abbreviated=ArtResources.Truncated(label,button.Width-24);
                TextButton(b,button,abbreviated?T("metal-short."+metal,label):label,()=>document.SetWholeMetal(metal),active:selectedMetal==metal,hint:abbreviated?label:null);
            }
            y+=48;
        }
        var preview=SimpleEditorLayout.ProductPreview(area,weapon,decoration);
        if(preview.Width>0)
        {
            ArtResources.InventoryPreviewSlot(b,preview);
            if(SimpleCrafting.IsDecoration(d.Use))
            {
                var tile=ArtResources.SceneTexture(d);int side=(preview.Width-12)/2;
                for(int ty=0;ty<2;ty++)for(int tx=0;tx<2;tx++)
                    b.Draw(tile.Texture,new Rectangle(preview.X+6+tx*side,preview.Y+6+ty*side,side,side),tile.Source,Color.White);
            }
            else ArtResources.DesignPreview(b,d,new(preview.X+6,preview.Y+6,preview.Width-12,preview.Height-12),trimTransparent:SimpleCrafting.IsWoodwork(d.Use));
            Line(b,"⊕",new(preview.Right-24,preview.Y+4,20,20),ArtResources.Ink);
            if(preview.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=T("scene-open","点击查看实际比例与占地");
            controls.Add((preview,()=>{scene.Open(d);Suspend();}));
        }
    }
    private void DrawSelector(SpriteBatch b,Blueprint d)
    {
        if(selector=="pattern")
        {
            Dialog(b,T("pattern-edit","移动与复制"),new[]{
                (T("pattern-all","整幅图案"),(Action)(()=>StartPattern(false))),
                (T("pattern-select","框选区域"),(Action)(()=>StartPattern(true))),
                (T("cancel","取消"),(Action)(()=>{selector="";Suspend();}))});return;
        }
        void Choose(string id){selector="";RequestDesign(id);Suspend();}
        if(selector=="view")
        {
            Dialog(b,T("touch-view","视图"),new[]{
                (T("touch-zoom-in","放大"),(Action)(()=>{transform.ZoomStepsAt(layout.Canvas.X+layout.Canvas.Width/2,layout.Canvas.Y+layout.Canvas.Height/2,Math.Max(4,transform.Zoom/3));selector="";Suspend();})),
                (T("touch-zoom-out","缩小"),(Action)(()=>{transform.ZoomStepsAt(layout.Canvas.X+layout.Canvas.Width/2,layout.Canvas.Y+layout.Canvas.Height/2,-Math.Max(4,transform.Zoom/3));selector="";Suspend();})),
                (T("fit","适应画布"),(Action)(()=>{Center();selector="";Suspend();})),
                (touchMoveMode?T("touch-draw","返回绘制"):T("touch-move","移动画布"),(Action)(()=>{touchMoveMode=!touchMoveMode;selector="";Suspend();})),
                (T("cancel","取消"),(Action)(()=>{selector="";Suspend();}))});
            return;
        }
        if(selector is "category" or "category/furniture" or "category/weapon"){DrawCategorySelector(b,d);return;}
        else if(selector=="weapon")Dialog(b,T("weapons","拼豆武器"),new[]{
            (T("sword","拼豆剑"),(Action)(()=>Choose(SimpleCrafting.WeaponId(ProductUse.Sword,d.Use is ProductUse.Sword or ProductUse.Dagger or ProductUse.Hammer?d.Views["front"].Width:16)))),
            (T("dagger","拼豆匕首"),(Action)(()=>Choose(SimpleCrafting.WeaponId(ProductUse.Dagger,d.Use is ProductUse.Sword or ProductUse.Dagger or ProductUse.Hammer?d.Views["front"].Width:16)))),
            (T("hammer","拼豆锤"),(Action)(()=>Choose(SimpleCrafting.WeaponId(ProductUse.Hammer,d.Use is ProductUse.Sword or ProductUse.Dagger or ProductUse.Hammer?d.Views["front"].Width:16)))),
            (T("cancel","取消"),(Action)(()=>selector="category"))},selected:d.Use==ProductUse.Sword?0:d.Use==ProductUse.Dagger?1:d.Use==ProductUse.Hammer?2:-1);
        else if(selector=="orientation")Dialog(b,T("sword-orientation","挥舞方向"),new[]{
            (T("sword-diagonal","原版"),(Action)(()=>{document.SetSwordOrientation(SwordOrientation.Diagonal);selector="";Suspend();})),
            (T("sword-vertical","竖直模式"),(Action)(()=>{document.SetSwordOrientation(SwordOrientation.Vertical);selector="";Suspend();})),
            (T("fit","适应画布")+" · Home",(Action)(()=>{selector="";Center();Suspend();})),
            (T("cancel","取消"),(Action)(()=>selector="size"))},selected:d.SwordOrientation==SwordOrientation.Vertical?1:0);
        else if(d.Use==ProductUse.WoodFurniture)Dialog(b,T("size-select","选择尺寸"),new[]{
            (T("ornament-small","小摆件 · 16×16px / 1×1格"),(Action)(()=>Choose(SimpleCrafting.Ornament))),
            (T("ornament-large","大摆件 · 32×32px / 按图案占地"),(Action)(()=>Choose(SimpleCrafting.LargeOrnament))),
            (T("fit","适应画布")+" · Home",(Action)(()=>{selector="";Center();Suspend();})),
            (T("cancel","取消"),(Action)(()=>selector=""))},selected:d.TemplateId==SimpleCrafting.LargeOrnament?1:0);
        else if(d.Use==ProductUse.Sword)Dialog(b,T("size-select","选择尺寸"),new[]{
            ("16×16px",(Action)(()=>Choose(SimpleCrafting.Sword))),
            ("24×24px",(Action)(()=>Choose(SimpleCrafting.Sword24))),
            ("32×32px",(Action)(()=>Choose(SimpleCrafting.Sword32))),
            (T("sword-orientation","挥舞方向")+"："+(d.SwordOrientation==SwordOrientation.Vertical?T("sword-vertical","竖直模式"):T("sword-diagonal","原版")),(Action)(()=>selector="orientation")),
            (T("cancel","取消"),(Action)(()=>selector=""))},selected:d.Views["front"].Width==24?1:d.Views["front"].Width==32?2:0);
        else if(d.Use is ProductUse.Wallpaper or ProductUse.Flooring)Dialog(b,T("size-select","选择尺寸"),new[]{
            ("16×16px",(Action)(()=>Choose(ProductCategories.For(d.Use)!.TemplateFor(16)))),
            ("32×32px",(Action)(()=>Choose(ProductCategories.For(d.Use)!.TemplateFor(32)))),
            (T("fit","适应画布")+" · Home",(Action)(()=>{selector="";Center();Suspend();})),
            (T("cancel","取消"),(Action)(()=>selector=""))},selected:d.Views["front"].Width==32?1:0);
        else if(d.Use!=ProductUse.Picture)Dialog(b,T("size-select","选择尺寸"),new[]{
            ("16×16px",(Action)(()=>Choose(SimpleCrafting.WeaponId(d.Use,16)))),
            ("24×24px",(Action)(()=>Choose(SimpleCrafting.WeaponId(d.Use,24)))),
            ("32×32px",(Action)(()=>Choose(SimpleCrafting.WeaponId(d.Use,32)))),
            (T("fit","适应画布")+" · Home",(Action)(()=>{selector="";Center();Suspend();})),
            (T("cancel","取消"),(Action)(()=>selector=""))},selected:d.Views["front"].Width==24?1:d.Views["front"].Width==32?2:0);
        else Dialog(b,T("size-select","选择尺寸"),new[]{
            ("16×16px",(Action)(()=>Choose(SimpleCrafting.Picture16))),
            ("32×32px",(Action)(()=>Choose(SimpleCrafting.Picture32))),
            (T("fit","适应画布")+" · Home",(Action)(()=>{selector="";Center();Suspend();})),
            (T("cancel","取消"),(Action)(()=>selector=""))},selected:d.Views["front"].Width==32?1:0);
    }
    private string KindName(ProductUse use)=>use switch
    {
        ProductUse.Sword=>T("sword","拼豆剑"),ProductUse.Dagger=>T("dagger-short","匕首"),
        ProductUse.Hammer=>T("hammer-short","锤"),ProductUse.WoodFurniture=>T("ornament-short","摆件"),
        ProductUse.Wallpaper=>T("wallpaper","壁纸"),ProductUse.Flooring=>T("flooring","地板"),
        _=>T("picture-category","拼豆画")
    };
    private string CategoryPath(ProductUse use)=>
        (ProductCategories.For(use)?.Group==ProductGroup.Weapon?T("weapon-group","武器"):T("furniture-group","家具"))+"／"+KindName(use);
    private void DrawCategorySelector(SpriteBatch b,Blueprint d)
    {
        ArtResources.Scope=FeedbackScope;hoverText=null;Fill(b,layout.Frame,Color.Black*0.65f);controls.Clear();
        bool compact=layout.Frame.Width<640;
        int width=Math.Min(compact?440:680,layout.Frame.Width-20),height=Math.Min(compact?360:348,layout.Frame.Height-20);
        var box=new UiRect(layout.Frame.X+(layout.Frame.Width-width)/2,layout.Frame.Y+(layout.Frame.Height-height)/2,width,height);
        ArtResources.Panel(b,box);
        Line(b,T("category-select","选择品类"),new(box.X+16,box.Y+12,width-80,28),ArtResources.Ink);
        var close=new UiRect(box.Right-52,box.Y+8,40,40);
        ArtResources.CloseButton(b,new ClickableTextureComponent(ArtResources.Rect(close),Game1.mouseCursors,new Rectangle(337,494,12,12),3f));
        controls.Add((close,()=>{selector="";Suspend();}));
        ProductGroup active=selector=="category/weapon"?ProductGroup.Weapon:ProductGroup.Furniture;
        if(compact)
        {
            int bw=(width-40)/2;
            TextButton(b,new(box.X+16,box.Y+52,bw,40),T("furniture-group","家具"),()=>selector="category/furniture",active:active==ProductGroup.Furniture);
            TextButton(b,new(box.X+24+bw,box.Y+52,bw,40),T("weapon-group","武器"),()=>selector="category/weapon",active:active==ProductGroup.Weapon);
        }
        var groups=compact?new[]{active}:new[]{ProductGroup.Furniture,ProductGroup.Weapon};
        int columnWidth=compact?width-32:(width-48)/2;
        for(int column=0;column<groups.Length;column++)
        {
            var group=groups[column];int left=box.X+16+column*(columnWidth+16);
            int top=box.Y+(compact?100:56);
            if(!compact){Line(b,group==ProductGroup.Furniture?T("furniture-group","家具"):T("weapon-group","武器"),new(left,top,columnWidth,28),ArtResources.Ink);top+=32;}
            int row=0;
            foreach(var entry in ProductCategories.All.Where(c=>c.Group==group))
            {
                int rtop=top+row*56;
                string label=T(entry.NameKey,KindName(entry.Use));
                string purpose=T(entry.PurposeKey,entry.Use.ToString());
                int currentSize=d.Views["front"].Width;
                string id=entry.TemplateFor(entry.Group==ProductGroup.Furniture&&currentSize>16?32:currentSize);
                TextButton(b,new(left,rtop,columnWidth,40),label,()=>{selector="";RequestDesign(id);Suspend();},active:d.Use==entry.Use);
                Line(b,purpose,new(left+8,rtop+39,columnWidth-16,16),ArtResources.MutedInk);
                row++;
            }
        }
    }
    private void PickerButton(SpriteBatch b,UiRect r)
    {
        var face=ArtResources.Button(b,r,selected:tool=="picker");int x=face.X,y=face.Y;
        var ink=ArtResources.ButtonInk(r);
        if(!ArtResources.PipetteIcon(b,face))
        {
            Fill(b,new(x+25,y+7,10,8),ink);
            Fill(b,new(x+27,y+9,6,4),new Color(255,229,173));
            for(int i=0;i<12;i++)Fill(b,new(x+24-i,y+13+i,4,4),ink);
            Fill(b,new(x+9,y+26,5,4),ink);
        }
        Fill(b,new(x+28,y+26,9,9),ink);
        Fill(b,new(x+30,y+28,5,5),ColorOf(BeadPalette.Resolve(catalog,color)?.Rgba??BeadPalette.Defaults[0]));
        controls.Add((r,()=>{tool="picker";drawer="";notice=T("pick-prompt","点击画板上的豆子吸取颜色");}));
        if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=T("picker","吸管取色");
    }
    private void ArrowButton(SpriteBatch b,UiRect r,bool right,Action action,bool enabled)
    {
        var face=ArtResources.Button(b,r,enabled);int cx=face.X+face.Width/2,cy=face.Y+face.Height/2;
        for(int row=0;row<=12;row++){int half=Math.Min(row,12-row);Fill(b,new(right?cx-6:cx+6-half*2,cy-6+row,half*2+1,1),ArtResources.ButtonInk(r,enabled));}
        if(enabled)controls.Add((r,action));
        if(r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))hoverText=right?T("redo","重做")+" · Ctrl+Y":T("undo","撤销")+" · Ctrl+Z";
    }
    private void BeginMake()
    {
        Suspend();
        var d=document.Snapshot();var recipe=manufacturing?.FindRecipe(d.TemplateId);
        if(recipe is null)return;var preview=manufacturing!.Preview(d,recipe,progress,Guid.NewGuid().ToString("N"));plan=preview.Plan;confirmationDesign=plan?.Product.Design;
        if(plan is null)notice=text.Get("manufacturing.error."+preview.Failure);
    }
    private void DrawConfirmation(SpriteBatch b)
    {
        var p=plan!;var d=confirmationDesign!;var cost=SimpleCrafting.Cost(d);
        string subtitle=SimpleCrafting.IsDecoration(d.Use)
            ?T("decor-fixed-price","固定售价：材料基础价值的两倍")
            :ContentText.Get("market.unknown","售价完成后揭晓")+"\n"+ContentText.Get("market.random-note","市场估价随机，不代表图案好坏。");
        if(SimpleCrafting.IsWeapon(d.Use))
        {
            var metrics=WeaponMetrics(d);
            subtitle+="\n"+T("damage","伤害")+$" {metrics.Stats.MinDamage}～{metrics.Stats.MaxDamage}"
                +"\n"+T("speed-short","攻速")+$" {metrics.SpeedShift:+0;-0;0} · "+T("reach-short","范围")+$" ×{metrics.Reach:0.#}"
                +"\n"+ContentText.Get("weapon.shape.swing","挥舞时长 −5%");
        }
        Dialog(b,T("confirm","确认熨烫")+" · "+(p.IsCreative?T("free","免费"):ItemName(cost.Item)+" × "+cost.Count),new[]{(T("cancel","取消"),(Action)(()=>plan=null)),(T("confirm","确认熨烫"),(Action)(()=>
        {
            plan=null;Suspend();
            void Done(ManufacturingResult result)
            {
                if(!result.Success){notice=text.Get(result.Message);noticeUntil=Now+2;return;}
                Completed?.Invoke(result);
            }
            if(Context.IsMultiplayer&&OnlineSession.Current is {} online)online.Craft(d,Done);
            else Done(manufacturing!.SubmitResult(p,manufacturing.FindRecipe(d.TemplateId)!,progress));
        }))},d,subtitle:subtitle);
    }
    private void UndoDesign(){CancelPattern();var size=document.ViewSize;if(document.Undo()&&document.ViewSize!=size){Center();availabilityKey=null;}}
    private void RedoDesign(){CancelPattern();var size=document.ViewSize;if(document.Redo()&&document.ViewSize!=size){Center();availabilityKey=null;}}
    private void DrawNaming(SpriteBatch b)
    {
        ArtResources.Scope=FeedbackScope;hoverText=null;Fill(b,layout.Frame,Color.Black*0.65f);controls.Clear();
        int width=Math.Min(480,layout.Frame.Width-32),height=210;
        var box=new UiRect(layout.Frame.X+(layout.Frame.Width-width)/2,layout.Frame.Y+(layout.Frame.Height-height)/2,width,height);
        ArtResources.Panel(b,box);
        Line(b,T("name-new","给新图纸命名"),new(box.X+16,box.Y+16,box.Width-32,32),ArtResources.Ink);
        nameBox.X=box.X+16;nameBox.Y=box.Y+60;nameBox.Width=box.Width-32;nameBox.Draw(b);
        if(namingError.Length>0)Line(b,namingError,new(box.X+16,box.Y+110,box.Width-32,28),ArtResources.Ink);
        int buttonWidth=(box.Width-40)/2;
        TextButton(b,new(box.X+16,box.Bottom-60,buttonWidth,44),T("cancel","取消"),CancelNaming);
        TextButton(b,new(box.X+24+buttonWidth,box.Bottom-60,buttonWidth,44),T("name-confirm","确定并继续"),ConfirmNaming);
    }
    private void Dialog(SpriteBatch b,string title,(string Text,Action Run)[] choices,Blueprint? preview=null,int selected=-1,string? subtitle=null)
    {
        if(selector=="size"&&preview is null)
            choices=choices.Take(choices.Length-1).Append((T("pattern-edit","移动与复制"),(Action)(()=>{selector="pattern";Suspend();}))).Append(choices[^1]).ToArray();
        ArtResources.Scope=FeedbackScope;hoverText=null;Fill(b,layout.Frame,Color.Black*0.65f);controls.Clear();int width=Math.Min(480,layout.Frame.Width-32),height=preview is null?Math.Min(layout.Frame.Height-32,Math.Max(240,72+choices.Length*52+(subtitle?.Split('\n').Length??0)*24)):Math.Min(400,layout.Frame.Height-32);
        var r=new UiRect(layout.Frame.X+(layout.Frame.Width-width)/2,layout.Frame.Y+(layout.Frame.Height-height)/2,width,height);ArtResources.Panel(b,r);
        Line(b,title,new(r.X+16,r.Y+16,r.Width-32,32),ArtResources.Ink);
        var subtitleLines=subtitle?.Split('\n')??Array.Empty<string>();
        for(int i=0;i<subtitleLines.Length;i++)Line(b,subtitleLines[i],new(r.X+16,r.Y+46+i*24,r.Width-32,24),ArtResources.MutedInk);
        int previewTop=subtitleLines.Length==0?64:78+(subtitleLines.Length-1)*24;
        if(preview is not null)
        {
            var image=new UiRect(r.X+24,r.Y+previewTop,r.Width-48,r.Height-previewTop-72);
            if(SimpleCrafting.IsDecoration(preview.Use))
            {
                var tile=ArtResources.SceneTexture(preview);int side=Math.Min(image.Width/2,image.Height/2);
                int left=image.X+(image.Width-2*side)/2,top=image.Y+(image.Height-2*side)/2;
                for(int ty=0;ty<2;ty++)for(int tx=0;tx<2;tx++)
                    b.Draw(tile.Texture,new Rectangle(left+tx*side,top+ty*side,side,side),tile.Source,Color.White);
            }
            else ArtResources.DesignPreview(b,preview,image);
        }
        int row=preview is null?r.Y+64+subtitleLines.Length*24:r.Bottom-60;
        if(preview is not null){int bw=(r.Width-40)/2;for(int i=0;i<choices.Length;i++){var c=choices[i];TextButton(b,new(r.X+16+i*(bw+8),row,bw,44),c.Text,c.Run);}}
        else
        {
            string identity=FeedbackScope+title+string.Join("|",choices.Select(c=>c.Text));
            if(dialogIdentity!=identity){dialogIdentity=identity;dialogPage=0;}
            int capacity=Math.Max(1,(r.Bottom-row-8)/52);bool pages=choices.Length>capacity;
            if(pages)capacity=Math.Max(1,(r.Bottom-row-68)/52);
            int total=(choices.Length+capacity-1)/capacity;dialogPage=Math.Clamp(dialogPage,0,total-1);
            for(int i=dialogPage*capacity;i<Math.Min(choices.Length,(dialogPage+1)*capacity);i++)
            {var c=choices[i];TextButton(b,new(r.X+16,row,r.Width-32,44),c.Text,c.Run,active:i==selected);row+=52;}
            if(pages)
            {
                int bw=(r.Width-40)/2;
                TextButton(b,new(r.X+16,r.Bottom-56,bw,44),"◀",()=>dialogPage--,dialogPage>0);
                TextButton(b,new(r.X+24+bw,r.Bottom-56,bw,44),"▶",()=>dialogPage++,dialogPage+1<total);
            }
        }
    }
    private void TextButton(SpriteBatch b,UiRect r,string label,Action action,bool enabled=true,bool active=false,string iconKey="",string? hint=null)
    {ArtResources.ButtonText(b,r,label,enabled,active,iconKey);if(enabled)controls.Add((r,action));if(hint is not null&&r.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))ArtResources.Hint(hint);}
    private void Button(SpriteBatch b,UiRect r,string key,Action action)=>TextButton(b,r,text.Get(key),action);
    private static Color ColorOf(uint rgba)=>new((byte)(rgba>>24),(byte)(rgba>>16),(byte)(rgba>>8),(byte)rgba);
    private static void Fill(SpriteBatch b,UiRect r,Color c){if(r.Width>0&&r.Height>0)b.Draw(Game1.staminaRect,ArtResources.Rect(r),c);}
    private static void Line(SpriteBatch b,string label,UiRect r,Color c)=>ArtResources.TextLine(b,label,r,c);
}
#endif
