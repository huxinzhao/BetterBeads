using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

internal sealed class WorkbenchMenu : IClickableMenu
{
    private readonly SaveProgress progress;
    private readonly ITranslationHelper text;
    private readonly Rectangle[] tabs = new Rectangle[2];
    private static readonly string[] TabKeys = { "bench.editor", "bench.library" };
    private int selected=1;
    private readonly EditorPanel? editor;
    private readonly LibraryPanel? library;
    private Blueprint? pendingDesign;
    private DraftIssue? pendingIssue;
    private bool pendingImported;
    private bool closePrompt, closing, saveFailed;
    private readonly Rectangle[] closeChoices=new Rectangle[3];
    private EditorLayout geometry=null!;
    private Point layoutViewport;
    private WorkbenchScale uiScale;

    public WorkbenchMenu(SaveProgress progress, ITranslationHelper text, InventoryService? service, EditorDocument? draft,ReferenceReader referenceReader,ManufacturingService? manufacturing,string modDirectory="") : base(0, 0, 800, 520, true)
    {
        this.progress = progress;
        this.text = text;
        if (service is not null && draft is not null) editor = new(draft,service.Catalog,progress,text,referenceReader,manufacturing);
        if(draft is not null && service is not null)library=new(progress,draft,service.Catalog,text,OpenDesign,()=>
        {ReleaseInputs();selected=1;Layout();editor?.BeginImport();},()=>
        {if(editor?.SaveAs()==true){ReleaseInputs();selected=1;Layout();}},manufacturing,ReturnToEditor
#if BEADS_LITE
        ,modDirectory,OpenImported
#endif
        );
        if(editor is not null && library is not null)editor.Saved+=library.SelectSaved;
#if BEADS_LITE
        if(editor is not null)editor.Completed+=result=>
        {
            closing=true;ReleaseInputs();ReleasePreview();exitThisMenu();
            UiSounds.Success("getNewSpecialItem");
            try{Game1.activeClickableMenu=new ProductReceivedMenu(result.Product!,editor.Document.IsDirty);}
            catch{Game1.activeClickableMenu=null;Game1.addHUDMessage(new HUDMessage(ContentText.Get("simple.made","制作成功")));}
        };
#endif
        Layout();
    }

    private void Layout()
    {
        editor?.Suspend();
        library?.ReleaseFocus();
        uiScale=WorkbenchScale.Calculate(Game1.uiViewport.Width,Game1.uiViewport.Height,Game1.smallFont.MeasureString(ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y);
        #if BEADS_LITE
        var editorLayout = SimpleEditorLayout.Calculate(uiScale.Width,uiScale.Height);
#else
        var editorLayout = EditorLayout.Calculate(uiScale.Width,uiScale.Height);
#endif
        geometry=editorLayout;
        layoutViewport=new Point(Game1.uiViewport.Width,Game1.uiViewport.Height);
        width=editorLayout.Frame.Width; height=editorLayout.Frame.Height;
        xPositionOnScreen = editorLayout.Frame.X;
        yPositionOnScreen = editorLayout.Frame.Y;
        initializeUpperRightCloseButton();
        upperRightCloseButton.bounds=ArtResources.Rect(editorLayout.CloseButton);
        tabs[0]=ArtResources.Rect(editorLayout.EditorTab);tabs[1]=ArtResources.Rect(editorLayout.LibraryTab);
        editor?.Layout(editorLayout);
        if(!editorLayout.TooSmall)
        {
#if BEADS_LITE
            library?.Layout(ArtResources.Rect(LibraryLayout.LiteArea(editorLayout)),editorLayout.Frame);
#else
            library?.Layout(ArtResources.Rect(editorLayout.LibraryArea),editorLayout.Frame);
#endif
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => Layout();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
#if BEADS_LITE
        if(OnlineSession.Busy)return;
#endif
        EnsureLayout();
        using var ui=WorkbenchUi.Enter(uiScale);
        x=uiScale.Logical(x);y=uiScale.Logical(y);
#if BEADS_LITE
        ArtResources.Press(FeedbackTarget(x,y));
#else
        ArtResources.PressAt(x,y);
#endif
        if(closePrompt)
        {
            if(closeChoices[0].Contains(x,y))
            {
#if BEADS_LITE
                if(editor is null)saveFailed=true;
                else
                {
                    closePrompt=false;selected=1;Layout();
                    editor.Save(CompletePending,CancelPending,CancelPending);
                }
#else
                if(editor?.Save()==true)CompletePending();else saveFailed=true;
#endif
            }
            else if(closeChoices[1].Contains(x,y)){editor?.Document.DiscardChanges();CompletePending();}
            else if(closeChoices[2].Contains(x,y)){CancelPending();}
            return;
        }
        if(selected==1 && editor?.PaletteOpen==true)
        {
            upperRightCloseButton.bounds=ArtResources.Rect(editor.PaletteCloseButton);
            if(upperRightCloseButton.containsPoint(x,y)){editor.ClosePalette();editor.Suspend();return;}
        }
        if(selected==1 && editor?.HasOverlay==true) {editor.Click(x,y);return;}
#if BEADS_LITE
        if(selected==2)
        {
            if(library?.HasModal!=true)
            {
                upperRightCloseButton.bounds=ArtResources.Rect(geometry.CloseButton);
                if(upperRightCloseButton.containsPoint(x,y)){ReturnToEditor();return;}
            }
            library?.Click(x,y);return;
        }
#else
        if(selected==2 && library?.HasModal==true){library.Click(x,y);return;}
#endif
        upperRightCloseButton.bounds=ArtResources.Rect(geometry.CloseButton);
        if(upperRightCloseButton.containsPoint(x,y)){TryClose();return;}
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].Contains(x, y)) { editor?.Suspend(); selected = i+1;
#if BEADS_LITE
                if(selected==2)library?.Enter();
#endif
                Layout(); return; }
        if (selected == 1) editor?.Click(x,y);
        if(selected==2 && !geometry.TooSmall)library?.Click(x,y);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
#if BEADS_LITE
        if(OnlineSession.Busy)return;
#endif
        EnsureLayout();using var ui=WorkbenchUi.Enter(uiScale);
        if(!closePrompt && selected==1)editor?.Click(uiScale.Logical(x),uiScale.Logical(y),true);
    }
    private void EnsureLayout()
    {
        var desired=WorkbenchScale.Calculate(Game1.uiViewport.Width,Game1.uiViewport.Height,Game1.smallFont.MeasureString(ContentText.Get("copy.ArtResources.16e7a38c73","国Ag")).Y);
        if(layoutViewport.X!=Game1.uiViewport.Width || layoutViewport.Y!=Game1.uiViewport.Height || desired!=uiScale)Layout();
    }
    public override void update(GameTime time) { EnsureLayout();using var ui=WorkbenchUi.Enter(uiScale); base.update(time); if(!closePrompt && selected==1
#if BEADS_LITE
        && !OnlineSession.Busy
#endif
        ) editor?.Update();
#if BEADS_LITE
        if(!closePrompt && selected==2)library?.UpdateImport();
#endif
    }
    public override void receiveScrollWheelAction(int direction)
    {
#if BEADS_LITE
        if(OnlineSession.Busy)return;
#endif
        EnsureLayout();using var ui=WorkbenchUi.Enter(uiScale);
        if(closePrompt)return;
        if(selected==1)editor?.Scroll(direction);
    }
    public override void receiveKeyPress(Keys key)
    {
#if BEADS_LITE
        if(OnlineSession.Busy)return;
#endif
        if(closePrompt){if(key==Keys.Escape)CancelPending();return;}
        if(selected==2 && library?.Key(key)==true)return;
#if BEADS_LITE
        if(selected==2 && (key==Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton,key))){ReturnToEditor();return;}
#endif
        if (selected==1 && editor?.Key(key)==true) return;
        if(key==Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton,key)){TryClose();return;}
        base.receiveKeyPress(key);
    }
    private void TryClose()
    {
        pendingDesign=null;pendingImported=false;
        ReleaseInputs();
        saveFailed=false;
        if(editor?.Document.IsDirty==true) closePrompt=true;
        else {closing=true;exitThisMenu();}
    }
    private void ReturnToEditor()
    {
        library?.ReleaseFocus();selected=1;Layout();
    }
    private void OpenDesign(Blueprint design,DraftIssue? issue)
    {
        ReleaseInputs();pendingDesign=design.Copy();pendingIssue=issue;pendingImported=false;saveFailed=false;
        if(editor?.Document.IsDirty==true)closePrompt=true;
        else CompletePending();
    }
    private void OpenImported(Blueprint design)
    {
        ReleaseInputs();pendingDesign=design.Copy();pendingIssue=null;pendingImported=true;saveFailed=false;
        if(editor?.Document.IsDirty==true)closePrompt=true;
        else CompletePending();
    }
    private void CancelPending()
    {
#if BEADS_LITE
        library?.CancelOpen();
#endif
        pendingDesign=null;pendingIssue=null;pendingImported=false;closePrompt=false;saveFailed=false;
    }
    private void CompletePending()
    {
        if(pendingDesign is null){closePrompt=false;closing=true;exitThisMenu();return;}
        if(pendingDesign.Revision>0&&!pendingImported)
        {
            if(!new BlueprintRepository(progress).TryOpen(pendingDesign.Id,out var latest) || latest is null){saveFailed=true;return;}
            pendingDesign=latest;
        }
        if((pendingImported?editor?.Document.TryOpenImported(pendingDesign):editor?.Document.TryOpen(pendingDesign))!=true)
        {saveFailed=true;CancelPending();
#if BEADS_LITE
            editor?.ShowNotice(text.Get("editor.save-failed").ToString());
#endif
            return;}
#if BEADS_LITE
        library?.RecordOpened();
#endif
        pendingDesign=null;pendingImported=false;closePrompt=false;selected=1;Layout();editor!.ResetForOpenedDesign();
        editor.Locate(pendingIssue);pendingIssue=null;
    }
    public override bool readyToClose()=>
#if BEADS_LITE
        !OnlineSession.Busy&&
#endif
        (closing || (!closePrompt && editor?.HasOverlay!=true && library?.HasModal!=true && editor?.Document.IsDirty!=true));
    public override void receiveGamePadButton(Buttons button)
    {
#if BEADS_LITE
        if(OnlineSession.Busy)return;
#endif
        if(button==Buttons.B){if(closePrompt)CancelPending();else if(library?.HasModal==true)library.Key(Keys.Escape);
#if BEADS_LITE
            else if(selected==2)ReturnToEditor();
#endif
            else if(editor?.HasOverlay==true)editor.Key(Keys.Escape);else TryClose();return;}
        if(!closePrompt)base.receiveGamePadButton(button);
    }
    protected override void cleanupBeforeExit() { ReleasePreview(); ReleaseInputs();
#if BEADS_LITE
        library?.CloseImport();
#endif
        ArtResources.ResetMotion(); base.cleanupBeforeExit(); }
    public void ReleasePreview(){editor?.CloseFitting();editor?.ClosePalette();}
    public void ReleaseInputs() { editor?.Suspend(); library?.ReleaseFocus(); }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        using var ui=WorkbenchUi.Enter(uiScale);
        using(new ScaledUiBatch(b,uiScale.Factor))DrawContent(b);
        if(selected==1 && !closePrompt)editor?.DrawHover(b);
#if BEADS_LITE
        ArtResources.DrawHint(b);
#endif
        ArtResources.ReleasePreviewRetired();
#if BEADS_LITE
        if(OnlineSession.Busy)
        {
            b.Draw(Game1.fadeToBlackRect,new Rectangle(0,0,Game1.uiViewport.Width,Game1.uiViewport.Height),Color.Black*.45f);
            var label=OnlineSession.Text("waiting");var size=Game1.smallFont.MeasureString(label);
            b.DrawString(Game1.smallFont,label,new Vector2((Game1.uiViewport.Width-size.X)/2,(Game1.uiViewport.Height-size.Y)/2),Color.White);
        }
#endif
        drawMouse(b);
    }

    private void DrawContent(SpriteBatch b)
    {
        ArtResources.BeginFrame();
#if BEADS_LITE
        ArtResources.PrepareFeedback(closePrompt?"close-prompt":selected==2?library?.FeedbackScope??"library/":editor?.FeedbackScope??"editor",FeedbackTarget(WorkbenchUi.MouseX,WorkbenchUi.MouseY));
        ArtResources.Scope="editor";
#endif
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, uiScale.Width+1, uiScale.Height+1), Color.Black * 0.65f);
        ArtResources.Panel(b,new Rectangle(xPositionOnScreen,yPositionOnScreen,width,height));
#if !BEADS_LITE
        string title=text.Get(PlayMode.Creative?"creative.bench-name":"bench.name").ToString();
        ArtResources.TextLine(b,title,geometry.Title,ArtResources.Ink);
#endif
#if BEADS_LITE
        if(selected!=2)
#endif
        for (int i = 0; i < tabs.Length; i++)
        {
            var r = tabs[i];
            string tabLabel=text.Get(TabKeys[i]).ToString();
            ArtResources.Tab(b,new(r.X,r.Y,r.Width,r.Height),tabLabel,i+1==selected);
        }
        if(selected==1 && editor?.PaletteOpen!=true
#if BEADS_LITE
            || selected!=1 && selected!=2
#else
            || selected!=1
#endif
          )
        {upperRightCloseButton.bounds=ArtResources.Rect(geometry.CloseButton);ArtResources.CloseButton(b,upperRightCloseButton);}
        if(selected==1 && editor is not null) editor.Draw(b);
        else if(selected==2 && library is not null && !geometry.TooSmall)library.Draw(b);
        else {
        var message = text.Get(selected switch { 1 => "bench.editor-pending", _ => "bench.library-status" },
            new { count = progress.BlueprintRecords.Count, colors = progress.UnlockedColors.Count }).ToString();
        b.DrawString(Game1.smallFont, Game1.parseText(message, Game1.smallFont, width - 72),
            new Vector2(xPositionOnScreen + 36, yPositionOnScreen + 166), ArtResources.Ink);
        }
        if(selected==1 && editor?.PaletteOpen==true)
        {ArtResources.Scope=
#if BEADS_LITE
            editor.FeedbackScope;
#else
            "dye";
#endif
            upperRightCloseButton.bounds=ArtResources.Rect(editor.PaletteCloseButton);ArtResources.CloseButton(b,upperRightCloseButton);}
#if BEADS_LITE
        else if(selected==2 && library?.HasModal!=true && !geometry.TooSmall)
        {ArtResources.Scope="library/";upperRightCloseButton.bounds=ArtResources.Rect(geometry.CloseButton);ArtResources.CloseButton(b,upperRightCloseButton);}
#endif
        if(closePrompt)
        {
            ArtResources.Scope="close-prompt";b.Draw(Game1.fadeToBlackRect,new Rectangle(xPositionOnScreen,yPositionOnScreen,width,height),Color.Black*0.75f);
            int dw=Math.Min(540,width-24), dh=260, dx=xPositionOnScreen+(width-dw)/2, dy=yPositionOnScreen+(height-dh)/2;
            ArtResources.Panel(b,new Rectangle(dx,dy,dw,dh));
            b.DrawString(Game1.smallFont,Game1.parseText(text.Get(saveFailed?"editor.save-failed":"editor.unsaved").ToString(),Game1.smallFont,dw-40),new Vector2(dx+20,dy+20),ArtResources.Ink);
            string[] keys=pendingDesign is null?new[]{"editor.save-close","editor.discard","editor.keep-editing"}
                :new[]{"library.save-open","library.discard-open","editor.keep-editing"};
            for(int i=0;i<3;i++)
            {
                var r=new Rectangle(dx+20,dy+90+i*50,dw-40,42);closeChoices[i]=r;
                ArtResources.ButtonText(b,new(r.X,r.Y,r.Width,r.Height),text.Get(keys[i]).ToString());
            }
        }

    }
#if BEADS_LITE
    private UiRect? FeedbackTarget(int x,int y)
    {
        if(closePrompt)
        {
            foreach(var r in closeChoices)if(r.Contains(x,y))return new(r.X,r.Y,r.Width,r.Height);
            return null;
        }
        if(selected==2)
        {
            if(library?.HasModal!=true&&geometry.CloseButton.Contains(x,y))return geometry.CloseButton;
            return library?.HoverControl(x,y);
        }
        if(selected!=1)return null;
        if(editor?.PaletteOpen==true&&editor.PaletteCloseButton.Contains(x,y))return editor.PaletteCloseButton;
        var control=editor?.HoverControl(x,y);
        if(control is not null||editor?.HasOverlay==true)return control;
        if(geometry.CloseButton.Contains(x,y))return geometry.CloseButton;
        foreach(var r in tabs)if(r.Contains(x,y))return new(r.X,r.Y,r.Width,r.Height);
        return null;
    }
#endif
}
