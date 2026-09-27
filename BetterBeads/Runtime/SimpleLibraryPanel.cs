#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;
internal sealed class LibraryPanel
{
    private static readonly string[] StarPixels={"....#....","...###...","#########",".#######.","..#####..",".#######.",".###.###.",".##...##."};
    private readonly BlueprintRepository repository;
    private readonly SaveProgress progress;
    private readonly IReadOnlyList<GameSpriteTemplates.Entry> templates;
    private readonly EditorDocument document;
    private readonly Action<Blueprint,DraftIssue?> open;
    private readonly Action backToEditor;
    private readonly Action<Blueprint> openDraft;
    private readonly ScenePreviewPanel scene=new();
    private LiteLibraryLayout geometry=null!;
    private ProductUse? filterUse;
    private bool favoritesOnly,recentFirst=true;
    private ListedBlueprint[]? filteredSource;
    private ListedBlueprint[] filtered=Array.Empty<ListedBlueprint>();
    private string? pendingRecentId;
    private readonly ImageImportPanel importer;
    private readonly string exportDirectory;
    private readonly FrameControls controls=new();
    private readonly TextBox search,name;
    private UiRect area,frame;
    private string? selected;
    private int page;
    private string mode="",feedback="";
    private ListedBlueprint? target;
    private sealed record ListedBlueprint(string Id,string? Raw,Blueprint Design,bool Builtin);
    private readonly SearchIndex<ListedBlueprint> index;
    private string cachedSearch="";
    public void Enter()=>index.Invalidate();
    public string FeedbackScope=>scene.Active?"scene":importer.Active?importer.FeedbackScope:"library/"+mode;
    public bool HasModal=>scene.Active||mode.Length>0||importer.Active;
    public UiRect? HoverControl(int x,int y)=>scene.Active?scene.Hover(x,y):importer.Active?importer.HoverControl(x,y):controls.HitTest(x,y);
    private static string T(string key,string value)=>ContentText.Get("simple."+key,value);
    public LibraryPanel(SaveProgress p,EditorDocument d,ProcessingCatalog c,ITranslationHelper t,Action<Blueprint,DraftIssue?> open,Action import,Action save,ManufacturingService? m,Action backToEditor,string modDirectory,Action<Blueprint> openImported)
    {
        progress=p;LibraryPreferences.Normalize(p);repository=new(p);templates=GameSpriteTemplates.Load(out var missing);document=d;this.open=open;this.backToEditor=backToEditor;openDraft=openImported;
        importer=new ImageImportPanel(modDirectory,openImported);exportDirectory=Path.Combine(modDirectory,"exports");
        index=new(()=>templates.Where(e=>!progress.HiddenLiteTemplates.Contains(e.Id))
            .Select(e=>new ListedBlueprint(e.Id,null,e.Design,true))
            .Concat(repository.List("").Where(e=>e.Design is {} d&&SimpleCrafting.Supported(d))
                .Select(e=>new ListedBlueprint(e.Id,e.Raw,e.Design!,false))).ToArray(),e=>e.Design.Name);
        if(missing is not null)feedback=T("template-missing","游戏贴图读取失败：")+missing;
        search=new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=""};
        name=new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=""};
    }
    public void Layout(Rectangle r,UiRect f){area=new(r.X,r.Y,r.Width,r.Height);frame=f;geometry=LiteLibraryLayout.Calculate(area);search.X=geometry.Search.X;search.Y=geometry.Search.Y;search.Width=geometry.Search.Width;ReleaseFocus();controls.Clear();importer.Layout(area,frame);}
    public void ReleaseFocus(){search.Selected=name.Selected=false;if(ReferenceEquals(Game1.keyboardDispatcher.Subscriber,search)||ReferenceEquals(Game1.keyboardDispatcher.Subscriber,name))Game1.keyboardDispatcher.Subscriber=null;}
    public void CloseImport(){importer.Close();scene.Close();}
    public void UpdateImport()=>importer.Update();
    public void SelectSaved(string id){LibraryPreferences.Touch(progress,id);OnlineSession.MarkPersonalChanged();selected=id;search.Text="";page=0;index.Invalidate();}
    public void RecordOpened(){if(pendingRecentId is {} id){LibraryPreferences.Touch(progress,id);OnlineSession.MarkPersonalChanged();filteredSource=null;}pendingRecentId=null;}
    public void CancelOpen()=>pendingRecentId=null;
    public bool Key(Keys key){if(scene.Active){if(key==Keys.Escape)scene.Close();return true;}if(importer.Active)return importer.Key(key);if(key==Keys.Escape){if(HasModal){mode="";ReleaseFocus();return true;}if(search.Selected){ReleaseFocus();return true;}backToEditor();return true;}return search.Selected||name.Selected||HasModal;}
    public void Click(int x,int y)
    {
        if(scene.Active){scene.Click(x,y);return;}
        if(importer.Active){importer.Click(x,y);return;}
        ReleaseFocus();if(!HasModal&&geometry.Search.Contains(x,y)){search.Selected=true;Game1.keyboardDispatcher.Subscriber=search;return;}
        if(mode=="rename"&&new UiRect(name.X,name.Y,name.Width,48).Contains(x,y)){name.Selected=true;Game1.keyboardDispatcher.Subscriber=name;return;}
        controls.TryInvoke(x,y);
    }
    public void Draw(SpriteBatch b)
    {
        if(scene.Active){scene.Draw(b,frame,()=>{});return;}
        if(importer.Active){importer.Draw(b);return;}
        ArtResources.Scope="library/";controls.Clear();
        ArtResources.Tab(b,new UiRect(area.X,area.Y-56,112,40),T("library-title","我的图纸"),true);
        Button(b,geometry.Import,T("import-reference","导入参考图"),()=>{pendingRecentId=null;ReleaseFocus();importer.Start();});
        Button(b,geometry.Filter,T("filter-sort","筛选与排序"),()=>{mode="filter";ReleaseFocus();});
        search.Draw(b);
        if(search.Text.Length==0&&!search.Selected)ArtResources.TextLine(b,T("search","搜索图纸名称"),new(area.X+12,area.Y+8,area.Width-24,32),ArtResources.MutedInk);
        var entries=Entries();
        if(entries.Length>0&&!entries.Any(e=>e.Id==selected))selected=entries[0].Id;
        int count=geometry.Count;
        page=Math.Clamp(page,0,Math.Max(0,(entries.Length-1)/count));
        for(int i=0;i<count&&page*count+i<entries.Length;i++)
        {
            var e=entries[page*count+i];var r=geometry.Row(i);var face=ArtResources.Button(b,r,true,selected==e.Id,identity:e.Id);
            int iconSize=Math.Min(44,face.Height-8);
            var slot=new UiRect(face.X+6,face.Y+4,iconSize,iconSize);
            ArtResources.InventoryPreviewSlot(b,slot);
            ArtResources.DesignPreview(b,e.Design,new(slot.X+4,slot.Y+4,slot.Width-8,slot.Height-8),trimTransparent:SimpleCrafting.IsWoodwork(e.Design.Use));
            ArtResources.TextLine(b,e.Design.Name+(e.Builtin?" · "+T("template-badge","模板"):""),new(face.X+64,face.Y+2,face.Width-120,24),ArtResources.ButtonInk(r));
            var grid=e.Design.Views["front"];
            ArtResources.TextLine(b,ContentText.Get("use."+e.Design.Use,e.Design.Use switch{ProductUse.Dagger=>"匕首",ProductUse.Hammer=>"锤",ProductUse.Sword=>"剑",ProductUse.WoodFurniture=>"摆件",_=>"拼豆画"})+$" · {grid.Width}*{grid.Height}px",
                new UiRect(face.X+64,face.Bottom-24,face.Width-120,22),ArtResources.MutedInk);
            controls.Add((r,()=>selected=e.Id));
            var star=geometry.Star(i);bool favorite=progress.FavoriteBlueprintIds.Contains(e.Id);
            var starFace=ArtResources.Button(b,star,selected:favorite);
            int sx=starFace.X+(starFace.Width-18)/2,sy=starFace.Y+(starFace.Height-16)/2;
            for(int yy=0;yy<StarPixels.Length;yy++)for(int xx=0;xx<9;xx++)if(StarPixels[yy][xx]=='#')
                b.Draw(Game1.staminaRect,new Rectangle(sx+xx*2,sy+yy*2,2,2),favorite?new Color(120,70,25):ArtResources.MutedInk);
            controls.Add((star,()=>{if(!progress.FavoriteBlueprintIds.Add(e.Id))progress.FavoriteBlueprintIds.Remove(e.Id);OnlineSession.MarkPersonalChanged();filteredSource=null;}));
            var preview=geometry.Preview(i);controls.Add((preview,()=>{ReleaseFocus();scene.Open(e.Design);}));
            if(!HasModal&&preview.Contains(WorkbenchUi.MouseX,WorkbenchUi.MouseY))ArtResources.Hint(T("scene-open","点击查看实际比例与占地"));
        }
        var current=entries.FirstOrDefault(e=>e.Id==selected);
        Button(b,geometry.Actions[0],T("open","打开"),()=>
        {
            if(current!.Builtin)OpenEntry(current,true);
            else{target=current;mode="open";ReleaseFocus();}
        },current is not null);
        Button(b,geometry.Actions[1],T("rename","改名"),()=>{target=current;mode="rename";name.Text=current!.Design.Name;},current is not null&&!current.Builtin);
        Button(b,geometry.Actions[2],T("delete","删除"),()=>{target=current;mode="delete";},current is not null);
        Button(b,geometry.Actions[3],T("share","分享"),()=>{target=current;mode="share";feedback="";ReleaseFocus();});
        Button(b,geometry.Previous,"←",()=>page--,page>0);Button(b,geometry.Next,"→",()=>page++,(page+1)*count<entries.Length);
        ArtResources.TextLine(b,feedback.Length>0?feedback:entries.Length==0?T("no-matches","没有符合条件的图纸"):$"{page+1} / {Math.Max(1,(entries.Length+count-1)/count)}",geometry.Status,ArtResources.Ink,true);
        if(!HasModal)return;
        ArtResources.Scope=FeedbackScope;controls.Clear();b.Draw(Game1.fadeToBlackRect,ArtResources.Rect(frame),Color.Black*0.58f);
        if(mode=="filter"){DrawFilters(b);return;}
        if(mode=="open"){DrawOpenChoices(b);return;}
        if(mode=="share"){DrawShare(b);return;}
        int dialogWidth=Math.Min(520,frame.Width-32);
        const int dialogHeight=192;
        var box=new UiRect(frame.X+(frame.Width-dialogWidth)/2,frame.Y+(frame.Height-dialogHeight)/2,dialogWidth,dialogHeight);ArtResources.Panel(b,box);
        ArtResources.TextLine(b,mode=="rename"?T("rename","改名"):target?.Builtin==true?T("template-delete-confirm","从当前存档隐藏此模板？"):T("delete-confirm","删除图纸？已有成品不受影响。"),new(box.X+16,box.Y+16,box.Width-32,32),ArtResources.Ink);
        if(mode=="delete"&&target?.Builtin==true)ArtResources.TextLine(b,T("template-delete-note","已复制的图纸不受影响。"),new(box.X+16,box.Y+56,box.Width-32,32),ArtResources.Ink);
        if(mode=="rename"){name.X=box.X+16;name.Y=box.Y+64;name.Width=box.Width-32;name.Draw(b);}
        Button(b,new(box.X+16,box.Bottom-64,(box.Width-40)/2,44),T("cancel","取消"),()=>{mode="";ReleaseFocus();});
        Button(b,new(box.X+box.Width/2+4,box.Bottom-64,(box.Width-40)/2,44),T("confirm-short","确认"),()=>
        {
            bool active=!target!.Builtin&&document.Snapshot().Id==target.Id;
            if(active&&document.IsDirty){feedback=T("save-first","请先保存当前修改");mode="";return;}
            bool ok=target.Builtin?progress.HiddenLiteTemplates.Add(target.Id)
                :mode=="rename"?repository.Rename(target.Id,target.Design.Revision,name.Text):repository.Delete(target.Id,target.Raw!);
            if(ok&&active){if(mode=="rename"&&repository.TryOpen(target.Id,out var d))document.TryOpen(d!);else if(mode=="delete")document.TryOpen(SimpleCrafting.Blank(SimpleCrafting.Picture16));}
            if(ok&&target.Builtin)selected=null;
            if(ok&&mode=="delete")LibraryPreferences.Remove(progress,target.Id);
            if(ok)OnlineSession.MarkPersonalChanged();
            index.Invalidate();
            feedback=ok?(target.Builtin?T("template-hidden","模板已从当前存档隐藏"):T("updated","图纸已更新")):T("conflict","记录已变化，请重新选择");mode="";ReleaseFocus();
        });
    }
    private ListedBlueprint[] Entries()
    {
        if(cachedSearch!=search.Text){cachedSearch=search.Text;page=0;}
        var source=index.Find(search.Text);
        if(!ReferenceEquals(source,filteredSource)){filtered=LibraryPreferences.Query(source,progress,e=>e.Id,e=>e.Design,filterUse,favoritesOnly,recentFirst).ToArray();filteredSource=source;}
        return filtered;
    }
    private Blueprint? ReadSelected(ListedBlueprint entry)
    {
        if(entry.Builtin)return progress.HiddenLiteTemplates.Contains(entry.Id)?null:entry.Design;
        if(progress.BlueprintRecords.TryGetValue(entry.Id,out var raw)&&raw==entry.Raw&&repository.TryOpen(entry.Id,out var current))return current;
        feedback=T("conflict","记录已变化，请重新选择");index.Invalidate();return null;
    }
    private void OpenEntry(ListedBlueprint entry,bool asNew)
    {
        var design=ReadSelected(entry);mode="";ReleaseFocus();
        if(design is null)return;
        pendingRecentId=entry.Builtin||!asNew?entry.Id:null;
        if(asNew)openDraft(ArtworkColors.CopyDraft(design));else open(design,null);
    }
    private void DrawOpenChoices(SpriteBatch b)
    {
        var p=LibraryOpenLayout.Calculate(frame);ArtResources.Panel(b,p.Dialog);
        ArtResources.TextLine(b,T("open-design-title","打开图纸"),new(p.Dialog.X+16,p.Dialog.Y+12,p.Dialog.Width-32,28),ArtResources.Ink);
        ArtResources.TextLine(b,T("open-design-hint","选择保存到原图，或另建一张图纸"),new(p.Dialog.X+16,p.Dialog.Y+44,p.Dialog.Width-32,28),ArtResources.MutedInk);
        Button(b,p.Edit,T("edit-original","编辑原图"),()=>OpenEntry(target!,false));
        Button(b,p.Copy,T("create-from-design","作为新图创作"),()=>OpenEntry(target!,true));
        Button(b,p.Cancel,T("cancel","取消"),()=>{mode="";ReleaseFocus();});
    }
    private void DrawFilters(SpriteBatch b)
    {
        var p=LibraryFilterLayout.Calculate(frame);ArtResources.Panel(b,p.Dialog);
        ArtResources.TextLine(b,T("filter-sort","筛选与排序"),new(p.Dialog.X+16,p.Dialog.Y+12,p.Dialog.Width-32,36),ArtResources.Ink);
        ProductUse?[] kinds={null,ProductUse.Picture,ProductUse.WoodFurniture,ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer};
        string[] names={T("all-kinds","全部"),T("picture-category","拼豆画"),T("ornament-short","摆件"),T("sword-short","剑"),T("dagger-short","匕首"),T("hammer-short","锤")};
        void Option(UiRect r,string label,bool active,Action action){ArtResources.ButtonText(b,r,label,selected:active);controls.Add((r,()=>{action();filteredSource=null;page=0;feedback="";}));}
        for(int i=0;i<kinds.Length;i++){var kind=kinds[i];Option(p.Categories[i],names[i],filterUse==kind,()=>filterUse=kind);}
        Option(p.Favorites,T("favorites-only","仅看收藏"),favoritesOnly,()=>favoritesOnly=!favoritesOnly);
        Option(p.Sort,recentFirst?T("sort-recent","排序：最近使用"):T("sort-name","排序：名称"),false,()=>recentFirst=!recentFirst);
        Button(b,p.Done,T("done","完成"),()=>mode="");
    }
    private void DrawShare(SpriteBatch b)
    {
        var p=LibraryShareLayout.Calculate(frame);ArtResources.Panel(b,p.Dialog);
        ArtResources.TextLine(b,T("share-title","导出与导入图纸"),new(p.Dialog.X+16,p.Dialog.Y+16,p.Dialog.Width-80,32),ArtResources.Ink);
        ArtResources.CloseButton(b,new ClickableTextureComponent(ArtResources.Rect(p.Close),Game1.mouseCursors,new Rectangle(337,494,12,12),3f));
        controls.Add((p.Close,()=>mode=""));
        Button(b,p.Copy,T("share-copy","复制选中图纸的分享码"),()=>
        {
            var design=target is null?null:ReadSelected(target);
            if(design is null)return;
            try
            {
                string code=BlueprintSharing.Encode(design);
                if(ShareClipboard.TryCopy(code))feedback=T("share-copied","分享码已复制");
                else
                {
                    Directory.CreateDirectory(exportDirectory);
                    string path=Path.Combine(exportDirectory,SafeStem(design)+".txt");File.WriteAllText(path,code);
                    feedback=T("share-code-file","剪贴板不可用，分享码已存入 exports 文件夹");
                }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException)
            {feedback=T("share-error","导出失败，请检查图纸或文件权限");}
        },target is not null);
        Button(b,p.Chart,T("share-chart","导出带色号图纸"),()=>
        {
            var design=target is null?null:ReadSelected(target);
            if(design is null)return;
            try
            {
                Directory.CreateDirectory(exportDirectory);
                string path=Path.Combine(exportDirectory,SafeStem(design)+".svg");
                File.WriteAllText(path,BlueprintColorChart.Create(design,MardPaletteReference.Current));
                feedback=T("share-chart-saved","色号图已保存到 exports 文件夹");
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException)
            {feedback=T("share-error","导出失败，请检查图纸或文件权限");}
        },target is not null);
        Button(b,p.Paste,T("share-paste","从剪贴板导入分享码"),()=>
        {
            if(!BlueprintSharing.TryDecode(ShareClipboard.TryPaste(),out var design)||design is null)
            {feedback=T("share-invalid","剪贴板中没有有效的拼豆图纸码");return;}
            mode="";ReleaseFocus();openDraft(design);
        });
        ArtResources.TextLine(b,feedback.Length>0?feedback:T("share-hint","导入后可编辑；首次保存时命名。色号为近似参考。"),p.Note,ArtResources.MutedInk,true);
    }
    private static string SafeStem(Blueprint design)
    {
        string name=new string(design.Name.Where(c=>!Path.GetInvalidFileNameChars().Contains(c)&&!char.IsControl(c)).Take(40).ToArray()).Trim();
        return (name.Length==0?"bead-design":name)+"-"+design.Id[..Math.Min(8,design.Id.Length)];
    }
    private void Button(SpriteBatch b,UiRect r,string label,Action action,bool enabled=true){ArtResources.ButtonText(b,r,label,enabled);if(enabled)controls.Add((r,action));}
}
#endif
