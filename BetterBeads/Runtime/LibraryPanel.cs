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
    private readonly BlueprintRepository repository;
    private readonly EditorDocument document;
    private readonly ITranslationHelper text;
    private readonly Action<Blueprint,DraftIssue?> open;
    private readonly Action import;
    private readonly Action saveAs;
    private readonly ProcessingCatalog catalog;
    private readonly SaveProgress progress;
    private readonly ManufacturingService? manufacturing;
    private readonly TextBox search;
    private readonly List<(Rectangle Rect,Action Action)> controls=new();
    private Rectangle area;
    private LibraryLayout layout=null!;
    private string? selectedId;
    private ProductUse? filter;
    private int page;
    private BlueprintEntry? detail;
    private string mode="",feedback="";
    private readonly TextBox name;
    private bool reveal;
    private int detailPage;
    public bool HasModal=>detail is not null;
    public LibraryPanel(SaveProgress progress,EditorDocument document,ProcessingCatalog catalog,ITranslationHelper text,Action<Blueprint,DraftIssue?> open,Action import,Action saveAs,ManufacturingService? manufacturing,Action backToEditor)
    {
        repository=new(progress);this.document=document;this.text=text;this.open=open;
        this.import=import;
        this.saveAs=saveAs;
        this.catalog=catalog;this.progress=progress;
        this.manufacturing=manufacturing;
        search=new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=""};
        name=new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"),null,Game1.smallFont,ArtResources.Ink){Text=""};
    }
    public void Layout(Rectangle bounds,UiRect frame)
    {
        ReleaseFocus();area=bounds;layout=LibraryLayout.Calculate(new(area.X,area.Y,area.Width,area.Height),frame);controls.Clear();
        search.X=layout.Search.X;search.Y=layout.Search.Y;search.Width=layout.Search.Width;
    }
    public void ReleaseFocus(){search.Selected=name.Selected=false;if(ReferenceEquals(Game1.keyboardDispatcher.Subscriber,search)||ReferenceEquals(Game1.keyboardDispatcher.Subscriber,name))Game1.keyboardDispatcher.Subscriber=null;}
    public bool Key(Keys key)
    {
        controls.Clear();
        if(HasModal){if(key==Keys.Escape){ReleaseFocus();if(mode.Length>0)mode="";else detail=null;}return true;}
        if(!search.Selected)return false;if(key==Keys.Escape || key==Keys.Enter)ReleaseFocus();return true;
    }
    public void SelectSaved(string id){selectedId=id;search.Text="";filter=null;reveal=true;}
    public void Click(int x,int y)
    {
        ReleaseFocus();
        if(HasModal && mode=="rename" && new Rectangle(name.X,name.Y,name.Width,48).Contains(x,y))
        {name.Selected=true;Game1.keyboardDispatcher.Subscriber=name;return;}
        if(!HasModal && new Rectangle(search.X,search.Y,search.Width,44).Contains(x,y)){search.Selected=true;Game1.keyboardDispatcher.Subscriber=search;return;}
        foreach(var control in controls.AsEnumerable().Reverse())if(control.Rect.Contains(x,y)){controls.Clear();control.Action();return;}
    }
    public void Draw(SpriteBatch b)
    {
        controls.Clear();search.Draw(b);
        if(search.Text.Length==0 && !search.Selected)Label(b,text.Get("library.search").ToString(),new(search.X+12,search.Y+12),search.Width-24);
        Button(b,new(area.Right-144,area.Y,144,40),filter is null?text.Get("library.all").ToString():text.Get("use."+filter).ToString(),()=>
        {
            var parent=Game1.activeClickableMenu;ReleaseFocus();
            Game1.activeClickableMenu=new WorkshopMenu(ContentText.Get("copy.LibraryPanel.8d2f193af5","图纸筛选"),ContentText.Get("copy.LibraryPanel.680a7c62bf","参考图纸可预览并复制为独立作品。"),()=>
            {
                var rows=new List<WorkshopChoice>{
#if !BEADS_LITE
                    new(ContentText.Get("copy.LibraryPanel.ed3c1ec8a1","参考图纸"),()=>WorkshopService.Current?.ShowPatterns(parent,copy=>SelectSaved(copy.Id))),
#endif
                    new(ContentText.Get("copy.LibraryPanel.235192b989","我的图纸 · 全部类型"),()=>{filter=null;page=0;Game1.activeClickableMenu=parent;})};
                foreach(ProductUse kind in Enum.GetValues<ProductUse>())rows.Add(new(text.Get("use."+kind),()=>{filter=kind;page=0;Game1.activeClickableMenu=parent;}));
                return rows;
            },parent);
        });
        var entries=repository.List(search.Text,filter);
        int perPage=layout.RowsPerPage;
        if(reveal){int index=entries.ToList().FindIndex(e=>e.Id==selectedId);if(index>=0)page=index/perPage;reveal=false;}
        page=Math.Clamp(page,0,Math.Max(0,(entries.Count-1)/perPage));
        for(int row=0;row<perPage && page*perPage+row<entries.Count;row++)
        {
            var entry=entries[page*perPage+row];var rect=new Rectangle(area.X,area.Y+48+row*64,area.Width,58);
            var face=ArtResources.Button(b,rect,true,entry.Id==selectedId);
            var grid=entry.Design?.Views.Values.FirstOrDefault();
            if(grid is not null)
            {
                float scale=Math.Min(40f/grid.Width,40f/grid.Height);
                for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)if(grid.Cells[y*grid.Width+x] is {} cell)
                    b.Draw(Game1.staminaRect,new Rectangle(face.X+10+(int)(x*scale),face.Y+6+(int)(y*scale),Math.Max(1,(int)scale),Math.Max(1,(int)scale)),
                        new Color((byte)(cell.Rgba>>24),(byte)(cell.Rgba>>16),(byte)(cell.Rgba>>8),(byte)cell.Rgba));
            }
            ArtResources.TextLine(b,entry.Design?.Name??text.Get("library.unreadable").ToString(),new(face.X+64,face.Y+5,face.Width-80,24),ArtResources.Ink);
            ArtResources.TextLine(b,entry.Design is null?entry.Id:text.Get("use."+entry.Design.Use).ToString(),new(face.X+64,face.Y+30,face.Width-80,18),ArtResources.MutedInk,scale:0.82f);
            controls.Add((rect,()=>selectedId=entry.Id));
        }
        if(entries.Count==0)Label(b,text.Get("library.empty").ToString(),new(area.X,area.Y+52),area.Width);
        int w=(area.Width-18)/4,yBottom=area.Bottom-84;
        var selected=entries.FirstOrDefault(e=>e.Id==selectedId);
        Button(b,new(area.X,yBottom,w,38),text.Get("processing.previous").ToString(),()=>page--,page>0);
        Button(b,new(area.X+w+6,yBottom,w,38),text.Get("processing.next").ToString(),()=>page++,(page+1)*perPage<entries.Count);
        Button(b,new(area.X+2*(w+6),yBottom,w,38),text.Get("library.details").ToString(),()=>{detail=selected;mode="";feedback="";ReleaseFocus();},selected is not null);
        Button(b,new(area.X+3*(w+6),yBottom,w,38),text.Get("library.copy").ToString(),()=>
        {if(repository.Duplicate(selected!.Id,selected.Design!.Name,out var copy)){SelectSaved(copy!.Id);feedback="library.copied";}else feedback="library.conflict";},selected?.IsReadable==true);
        int half=(area.Width-12)/3;
        Button(b,new(area.X,area.Bottom-40,half,40),text.Get("library.new").ToString(),()=>open(new Blueprint{
            Name=text.Get("editor.untitled").ToString(),TemplateId=ProductTemplates.Picture,Use=ProductUse.Picture,
            Views=new(){["front"]=new(){Width=16,Height=16,Cells=Enumerable.Repeat<BeadCell?>(null,256).ToList()}}
        },null));
        Button(b,new(area.X+half+6,area.Bottom-40,half,40),text.Get("library.save-as").ToString(),saveAs);
        Button(b,new(area.X+2*(half+6),area.Bottom-40,half,40),text.Get("editor.import").ToString(),import);
        if(feedback.Length>0)Label(b,text.Get(feedback).ToString(),new(layout.Status.X,layout.Status.Y),layout.Status.Width);
        if(HasModal)DrawDetail(b);
    }
    private void DrawDetail(SpriteBatch b)
    {
        controls.Clear();
        var dialog=new Rectangle(layout.Dialog.X,layout.Dialog.Y,layout.Dialog.Width,layout.Dialog.Height);
        ArtResources.Panel(b,dialog);
        var entry=detail!;int x=dialog.X+12,width=dialog.Width-24;
        Label(b,entry.Design?.Name??text.Get("library.unreadable").ToString(),new(x,dialog.Y+12),width);
        if(mode is "issues" or "name")
        {
            var messages=new List<(string Label,DraftIssue? Issue)>();
            if(mode=="name")messages.AddRange(Game1.parseText(entry.Design?.Name??entry.Id,Game1.smallFont,width).Split('\n').Select(line=>(line,(DraftIssue?)null)));
            else
            {
                var report=DraftStatus.Evaluate(entry.Design!,catalog,progress,manufacturing);
                foreach(var group in report.Issues.GroupBy(i=>(i.Code,i.View,i.Id)))
                {
                    var issue=group.First();
                    string label=text.Get("issue."+issue.Code).ToString();
                    if(issue.Id is not null)label+=" · "+issue.Id;
                    if(group.Count()>1)label+=" ×"+group.Count();
                    messages.Add((label,issue));
                }
                foreach(var amount in report.Materials)messages.Add((text.Get(amount.IsFree?"creative.amount":"editor.amount",new{material=text.Get("material."+amount.Id),needed=amount.Needed,owned=amount.Owned,missing=amount.Missing}).ToString(),null));
            }
            int perPage=Math.Max(1,(dialog.Height-152)/36);
            detailPage=Math.Clamp(detailPage,0,Math.Max(0,(messages.Count-1)/perPage));
            for(int row=0;row<perPage && detailPage*perPage+row<messages.Count;row++)
            {
                var message=messages[detailPage*perPage+row];var rect=new Rectangle(x,dialog.Y+48+row*36,width,32);
                if(message.Issue?.Cell is not null)Button(b,rect,message.Label,()=>{detail=null;open(entry.Design!,message.Issue);});
                else Label(b,message.Label,new(rect.X+6,rect.Y+6),rect.Width-12);
            }
            int half=(width-8)/2;
            Button(b,new(x,dialog.Bottom-96,half,40),text.Get("processing.previous").ToString(),()=>detailPage--,detailPage>0);
            Button(b,new(x+half+8,dialog.Bottom-96,half,40),text.Get("processing.next").ToString(),()=>detailPage++,(detailPage+1)*perPage<messages.Count);
        }
        else if(mode=="rename")
        {
            name.X=x;name.Y=dialog.Y+64;name.Width=width;name.Draw(b);
            Button(b,new(x,dialog.Bottom-96,width,40),text.Get("editor.name-apply").ToString(),()=>
            {
                bool current=document.Snapshot().Id==entry.Id;
                if(current && document.IsDirty){feedback="library.draft-dirty";return;}
                if(!repository.Rename(entry.Id,entry.Design!.Revision,name.Text)){feedback="library.conflict";return;}
                if(current && repository.TryOpen(entry.Id,out var renamed))document.TryOpen(renamed!);
                SelectSaved(entry.Id);detail=null;mode="";feedback="library.renamed";ReleaseFocus();
            });
        }
        else if(mode=="delete")
        {
            Label(b,text.Get("library.delete-warning").ToString(),new(x,dialog.Y+56),width);
            Button(b,new(x,dialog.Bottom-96,width,40),text.Get("library.delete-confirm").ToString(),()=>
            {
                if(!repository.Delete(entry.Id,entry.Raw)){feedback="library.conflict";return;}
                selectedId=null;detail=null;mode="";feedback="library.deleted";
            });
        }
        else
        {
            var grid=entry.Design?.Views.Values.FirstOrDefault();
            if(grid is not null)
            {
                int size=Math.Min(128,Math.Max(48,dialog.Height-250));float scale=Math.Min(size/(float)grid.Width,size/(float)grid.Height);
                for(int y=0;y<grid.Height;y++)for(int xx=0;xx<grid.Width;xx++)if(grid.Cells[y*grid.Width+xx] is {} cell)
                    b.Draw(Game1.staminaRect,new Rectangle(x+(int)(xx*scale),dialog.Y+48+(int)(y*scale),Math.Max(1,(int)scale),Math.Max(1,(int)scale)),
                        new Color((byte)(cell.Rgba>>24),(byte)(cell.Rgba>>16),(byte)(cell.Rgba>>8),(byte)cell.Rgba));
                Label(b,text.Get("use."+entry.Design!.Use)+$" · {grid.Width}×{grid.Height}",new(x+size+12,dialog.Y+48),width-size-12);
            }
            int half=(width-8)/2;
            Button(b,new(x,dialog.Bottom-144,half,40),text.Get("library.open").ToString(),()=>{detail=null;open(entry.Design!,null);},entry.Design?.Views.Count>0);
            Button(b,new(x+half+8,dialog.Bottom-144,half,40),text.Get("library.issues").ToString(),()=>{mode="issues";detailPage=0;feedback="";},entry.IsReadable);
            Button(b,new(x,dialog.Bottom-96,half,40),text.Get("editor.rename").ToString(),()=>{mode="rename";name.Text=entry.Design!.Name;},entry.IsReadable);
            Button(b,new(x+half+8,dialog.Bottom-96,half,40),text.Get("library.delete").ToString(),()=>mode="delete");
        }
        if(feedback.Length>0 && mode is not "issues" and not "name")Label(b,text.Get(feedback).ToString(),new(x,dialog.Bottom-182),width);
        if(mode.Length==0)controls.Add((new(x,dialog.Y+8,width,32),()=>{mode="name";detailPage=0;}));
        Button(b,new(x,dialog.Bottom-48,width,40),text.Get("editor.cancel").ToString(),()=>{ReleaseFocus();if(mode.Length>0)mode="";else detail=null;});
    }
    private void Button(SpriteBatch b,Rectangle rect,string label,Action action,bool enabled=true)
    {ArtResources.ButtonText(b,new(rect.X,rect.Y,rect.Width,rect.Height),label,enabled);if(enabled)controls.Add((rect,action));}
    private static void Label(SpriteBatch b,string label,Vector2 position,int width)
    {while(label.Length>0 && Game1.smallFont.MeasureString(label).X>width)label=label[..^1];b.DrawString(Game1.smallFont,label,position,ArtResources.Ink);}
}

