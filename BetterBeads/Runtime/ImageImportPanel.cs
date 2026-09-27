#if BEADS_LITE
using System.Diagnostics;
using System.Text;
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace BetterBeads.Runtime;

/// <summary>Transient preview: neither the bitmap nor its path is written to a save.</summary>
internal sealed class ImageImportPanel : IDisposable
{
    private readonly string folder;
    private readonly Action<Blueprint> accept;
    private readonly FrameControls controls=new();
    private readonly object chooserLock=new();
    private UiRect area,frame;
    private Texture2D? texture;
    private uint[]? pixels;
    private ImageImportResult? preview;
    private ImageImportPrepared? prepared;
    private string[] cachedFiles=Array.Empty<string>();
    private Task<string?>? chooserTask;
    private CancellationTokenSource? chooserCancel;
    private Process? chooserProcess;
    private int chooserGeneration;
    private string filename="",message="";
    private int page,tolerance=24;
    private string kind="picture";
    private bool large,restore=true,removeBackground,outline=true,showOriginal,settingsPage;
    private int weaponSize=16;
    private bool dirty=true;
    private bool autoSize;
    private bool conversionFailed;
    private sealed record Conversion(ImageImportPrepared Prepared,ImageImportResult Result,bool Large);
    private readonly LatestWork<Conversion> conversion=new();
    private bool Generating=>dirty||conversion.Pending;
    public string FeedbackScope=>"import/"+(pixels is null?"files":settingsPage?"settings":"preview");
    private bool preparedDirty=true,opaqueImage;
    public bool Active {get;private set;}
    public UiRect? HoverControl(int x,int y)=>controls.HitTest(x,y);
    private static string T(string key,string fallback)=>ContentText.Get("simple.import-"+key,fallback);
    public ImageImportPanel(string modDirectory,Action<Blueprint> accept)
    {folder=Path.Combine(modDirectory,"imports");this.accept=accept;}
    public void Layout(UiRect area,UiRect frame){this.area=area;this.frame=frame;controls.Clear();}
    public void Start()
    {
        Active=true;message="";settingsPage=false;
        try{Directory.CreateDirectory(folder);}catch(Exception){message=T("folder-error","无法创建 imports 文件夹，请检查安装位置权限");}
        RefreshFiles();
    }
    public void Close()
    {
        Active=false;conversion.Cancel();chooserCancel?.Cancel();
        lock(chooserLock){chooserGeneration++;try{if(chooserProcess is {HasExited:false} process)process.Kill(true);}catch(Exception){}}
        chooserTask=null;chooserCancel?.Dispose();chooserCancel=null;
        texture?.Dispose();texture=null;pixels=null;prepared=null;preview=null;filename="";controls.Clear();
    }
    public void Dispose()=>Close();
    public bool Key(Keys key)
    {
        if(!Active)return false;
        if(key==Keys.Escape){if(settingsPage)settingsPage=false;else Close();}
        return true;
    }
    public void Click(int x,int y){if(Active)controls.TryInvoke(x,y);}
    public void Update()
    {
        if(!Active)return;
        if(chooserTask is {IsCompleted:true} done)
        {
            chooserTask=null;chooserCancel?.Dispose();chooserCancel=null;
            try{string? selected=done.GetAwaiter().GetResult();if(selected=="")message=T("picker-fallback","文件选择器不可用，请使用 imports 文件夹");else if(selected is not null)Load(selected);else message="";}
            catch(OperationCanceledException){}
            catch(Exception){message=T("picker-fallback","文件选择器不可用，请使用 imports 文件夹");}
        }
        Refresh();
        if(conversion.TryTake(out var result,out var error))
        {
            conversionFailed=error is not null;
            if(result is not null){prepared=result.Prepared;preparedDirty=false;opaqueImage=!prepared.HasTransparency;preview=result.Result;large=result.Large;autoSize=false;message="";}
            else if(error is not null)message=T("conversion-error","无法生成图纸，请调整导入设置");
        }
    }
    private void RefreshFiles()
    {
        try{cachedFiles=Directory.Exists(folder)?Directory.EnumerateFiles(folder)
            .Where(f=>new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f=>Path.GetFileName(f),StringComparer.OrdinalIgnoreCase).Take(200).ToArray():Array.Empty<string>();}
        catch(Exception){cachedFiles=Array.Empty<string>();message=T("folder-error","无法读取 imports 文件夹，请检查安装位置权限");}
    }
    private void PickWindowsFile()
    {
        if(!OperatingSystem.IsWindows()){message=T("folder-hint","请将 PNG 或 JPG 图片放入 imports 文件夹");return;}
        if(chooserTask is not null)return;
        chooserCancel=new CancellationTokenSource();
        int generation;lock(chooserLock)generation=chooserGeneration;
        var token=chooserCancel.Token;chooserTask=Task.Run(()=>PickWindowsFileAsync(token,generation));
        message=T("waiting","等待选择图片…");
    }
    private async Task<string?> PickWindowsFileAsync(CancellationToken token,int generation)
    {
        try
        {
            const string script="[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; Add-Type -AssemblyName System.Windows.Forms; $owner=New-Object System.Windows.Forms.Form; $owner.TopMost=$true; $owner.ShowInTaskbar=$false; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='Images|*.png;*.jpg;*.jpeg'; $d.CheckFileExists=$true; try{if($d.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Out.Write($d.FileName)}}finally{$d.Dispose();$owner.Dispose()}";
            var start=new ProcessStartInfo("powershell.exe") {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,CreateNoWindow=true};
            start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-STA");start.ArgumentList.Add("-Command");start.ArgumentList.Add(script);
            token.ThrowIfCancellationRequested();
            using var process=Process.Start(start)??throw new IOException("File chooser did not start.");
            lock(chooserLock){if(token.IsCancellationRequested||generation!=chooserGeneration)process.Kill(true);else chooserProcess=process;}
            try
            {
                var output=process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(token).ConfigureAwait(false);
                string selected=(await output.ConfigureAwait(false)).Trim();
                if(process.ExitCode!=0)throw new IOException("File chooser failed.");
                return selected.Length==0?null:selected;
            }
            finally{lock(chooserLock){if(ReferenceEquals(chooserProcess,process))chooserProcess=null;}}
        }
        catch(OperationCanceledException){return null;}
        catch(Exception){return token.IsCancellationRequested?null:"";}
    }
    private void Load(string path)
    {
        Texture2D? decoded=null;
        try
        {
            if(!new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(path).ToLowerInvariant()))throw new ArgumentException("format");
            var info=new FileInfo(path);if(!info.Exists||info.Length>ImageBlueprintImport.MaxFileBytes)throw new ArgumentException("size");
            using var stream=File.OpenRead(path);
            if(!ImageFileProbe.TryDimensions(stream,info.Extension,out _,out _))throw new ArgumentException("dimensions");
            decoded=Texture2D.FromStream(Game1.graphics.GraphicsDevice,stream);
            if(decoded.Width>ImageBlueprintImport.MaxSide||decoded.Height>ImageBlueprintImport.MaxSide
                ||(long)decoded.Width*decoded.Height>ImageBlueprintImport.MaxPixels)throw new ArgumentException("dimensions");
            var colors=new Color[decoded.Width*decoded.Height];decoded.GetData(colors);
            var rgba=colors.Select(c=>PixelImport.FromPremultiplied(c.R,c.G,c.B,c.A)).ToArray();
            conversion.Cancel();
            texture?.Dispose();texture=decoded;decoded=null;
            pixels=rgba;prepared=null;preparedDirty=true;autoSize=true;
            filename=Path.GetFileName(path);
            kind="picture";large=texture.Width>16||texture.Height>16;
            removeBackground=false;outline=true;restore=true;showOriginal=false;dirty=true;message="";
        }
        catch(Exception){message=T("invalid","图片无法读取，或超过 20MB、4096 像素限制");}
        finally{decoded?.Dispose();}
    }
    private string Template()=>kind switch
    {
        "ornament"=>large?SimpleCrafting.LargeOrnament:SimpleCrafting.Ornament,
        "sword"=>SimpleCrafting.WeaponId(ProductUse.Sword,weaponSize),"dagger"=>SimpleCrafting.WeaponId(ProductUse.Dagger,weaponSize),"hammer"=>SimpleCrafting.WeaponId(ProductUse.Hammer,weaponSize),
        _=>large?SimpleCrafting.Picture32:SimpleCrafting.Picture16
    };
    private void Refresh()
    {
        if(!dirty||pixels is null||texture is null)return;
        dirty=false;conversionFailed=false;
        // Capture only plain data. No game state, texture or panel fields are accessed by the worker.
        var source=pixels;int width=texture.Width,height=texture.Height;
        var cached=preparedDirty?null:prepared;
        var settings=new ImageImportSettings(Template(),restore,removeBackground,tolerance,outline);
        bool chooseSize=autoSize,chosenLarge=large;
        conversion.Request(token=>
        {
            var ready=cached??ImageBlueprintImport.Prepare(source,width,height,settings.RemoveBackground,settings.BackgroundTolerance,token);
            if(chooseSize){chosenLarge=width/ready.ExactScale>16||height/ready.ExactScale>16;settings=settings with{TemplateId=chosenLarge?SimpleCrafting.Picture32:SimpleCrafting.Picture16};}
            return new Conversion(ready,ImageBlueprintImport.Create(ready,settings,token),chosenLarge);
        });
    }
    private void ToggleBackground(){removeBackground=!removeBackground;preparedDirty=dirty=true;}
    private void ChangeTolerance(int delta){tolerance=Math.Clamp(tolerance+delta,0,255);preparedDirty=dirty=true;}
    private void Button(SpriteBatch b,UiRect r,string label,Action action,bool enabled=true,bool selected=false)
    {ArtResources.ButtonText(b,r,label,enabled,selected);if(enabled)controls.Add((r,action));}
    public void Draw(SpriteBatch b)
    {
        if(!Active)return;ArtResources.Scope=FeedbackScope;controls.Clear();
        var geometry=ImageImportLayout.Calculate(area);
        ArtResources.TextLine(b,T("title","导入参考图"),new(area.X,area.Y-48,area.Width-160,40),ArtResources.Ink);
        Button(b,geometry.Back,T("back","返回我的图纸"),Close);
        if(!(pixels is not null&&geometry.Compact&&settingsPage))
        {
            Button(b,geometry.File,T("choose-file","选择本地图片"),PickWindowsFile,OperatingSystem.IsWindows()&&chooserTask is null);
            Button(b,geometry.Folder,T("folder","浏览 imports 文件夹"),()=>{conversion.Cancel();RefreshFiles();pixels=null;prepared=null;preview=null;texture?.Dispose();texture=null;dirty=true;},chooserTask is null);
        }
        if(pixels is null)
        {
            var files=cachedFiles;int rowY=area.Y+52;int rows=geometry.FileRows;
            page=Math.Clamp(page,0,Math.Max(0,(files.Length-1)/rows));
            for(int i=0;i<rows&&page*rows+i<files.Length;i++)
            {string file=files[page*rows+i];Button(b,new(area.X,rowY+i*48,area.Width,42),Path.GetFileName(file),()=>Load(file));}
            ArtResources.TextLine(b,chooserTask is not null?T("waiting","等待选择图片…"):Generating&&pixels is not null?T("generating","正在生成…"):message.Length>0?message:T("folder-hint","请将 PNG 或 JPG 图片放入 imports 文件夹"),
                geometry.FolderHint,ArtResources.Ink);
            Button(b,geometry.Previous,"←",()=>page--,page>0);
            Button(b,geometry.Next,"→",()=>page++,(page+1)*rows<files.Length);
            return;
        }
        if(geometry.Compact){DrawCompact(b,geometry);return;}
        var imageArea=geometry.Original;
        ArtResources.Panel(b,imageArea,"inset");
        if(geometry.Wide)
        {
            DrawOriginal(b,new(imageArea.X+8,imageArea.Y+8,imageArea.Width-16,imageArea.Height-44));
            ArtResources.TextLine(b,T("source","原图"),new(imageArea.X+12,imageArea.Bottom-32,imageArea.Width-24,24),ArtResources.Ink);
            var resultArea=geometry.Converted;
            ArtResources.Panel(b,resultArea,"inset");
            if(preview is not null)ArtResources.DesignPreview(b,preview.Design,new(resultArea.X+8,resultArea.Y+8,resultArea.Width-16,resultArea.Height-44),framed:false);
            ArtResources.TextLine(b,T("result","效果"),new(resultArea.X+12,resultArea.Bottom-32,resultArea.Width-24,24),ArtResources.Ink);
        }
        else
        {
            if(showOriginal)DrawOriginal(b,new(imageArea.X+8,imageArea.Y+8,imageArea.Width-16,imageArea.Height-16));
            else if(preview is not null)ArtResources.DesignPreview(b,preview.Design,new(imageArea.X+8,imageArea.Y+8,imageArea.Width-16,imageArea.Height-16),framed:false);
            Button(b,new(area.Right-102,imageArea.Y+4,96,40),showOriginal?T("result","效果"):T("source","原图"),()=>showOriginal=!showOriginal);
        }
        int targetSize=kind is "sword" or "dagger" or "hammer"?weaponSize:large?32:16;
        string status=chooserTask is not null?T("waiting","等待选择图片…"):Generating&&pixels is not null?T("generating","正在生成…"):message.Length>0?message:$"{filename} · {texture!.Width}×{texture.Height} → {targetSize}×{targetSize} · {preview?.Beads??0} {T("beads","颗豆")}";
        if(preview?.Restored==true)status+=" · "+T("restored","已无损还原");
        else if(preview?.Reduced==true)status+=" · "+T("reduced","已缩小");
        ArtResources.TextLine(b,status,geometry.Status,ArtResources.Ink,scale:0.82f);
        if(settingsPage)
        {
            Button(b,geometry.LeftOptions[0],T("restore","无损还原")+(restore?" ✓":""),()=>{restore=!restore;dirty=true;},selected:restore);
            Button(b,geometry.RightOptions[0],T("background","去纯色背景")+(removeBackground?" ✓":""),ToggleBackground,selected:removeBackground);
            Button(b,geometry.LeftOptions[1],T("tolerance","容差")+$" {tolerance} −",()=>ChangeTolerance(-8));
            Button(b,geometry.RightOptions[1],T("tolerance","容差")+$" {tolerance} +",()=>ChangeTolerance(8));
            Button(b,geometry.LeftOptions[2],T("outline","柔和描边")+(outline?" ✓":""),()=>{outline=!outline;dirty=true;},selected:outline);
            Button(b,geometry.RightOptions[2],T("options-back","返回品类"),()=>settingsPage=false);
        }
        else
        {
            Button(b,geometry.LeftOptions[0],T("category","品类")+"："+Kind(),NextKind);
            Button(b,geometry.RightOptions[0],T("size","尺寸")+"："+(kind is "sword" or "dagger" or "hammer"?$"{weaponSize}×{weaponSize}":large?"32×32":"16×16"),ChangeSize);
            Button(b,new(area.X,geometry.LeftOptions[1].Y,area.Width,42),T("options","还原、背景与描边设置"),()=>settingsPage=true);
            var note=new UiRect(area.X,geometry.LeftOptions[2].Y,area.Width,32);
            if(preview?.HasHalfAlpha==true)ArtResources.TextLine(b,T("alpha-note","半透明像素将转为实色豆"),note,ArtResources.MutedInk);
            else if(preview?.Reduced==true&&opaqueImage)ArtResources.TextLine(b,T("opaque-note","未检测到透明背景；可在设置中去除纯色背景"),note,ArtResources.MutedInk);
        }
        Button(b,geometry.Cancel,T("cancel","取消"),Close);
        Button(b,geometry.Apply,T("apply","进入画板"),()=>{if(Generating||conversionFailed)return;var d=preview!.Design.Copy(true);Close();accept(d);},chooserTask is null&&!Generating&&!conversionFailed&&preview is {Beads:>0});
    }
    private void DrawCompact(SpriteBatch b,ImageImportLayout geometry)
    {
        if(settingsPage)
        {
            Button(b,geometry.LeftOptions[0],T("category","品类")+"："+Kind(),NextKind);
            Button(b,geometry.RightOptions[0],T("size","尺寸")+"："+(kind is "sword" or "dagger" or "hammer"?$"{weaponSize}×{weaponSize}":large?"32×32":"16×16"),ChangeSize);
            Button(b,geometry.LeftOptions[1],T("restore","无损还原")+(restore?" ✓":""),()=>{restore=!restore;dirty=true;},selected:restore);
            Button(b,geometry.RightOptions[1],T("background","去纯色背景")+(removeBackground?" ✓":""),ToggleBackground,selected:removeBackground);
            Button(b,geometry.LeftOptions[2],T("tolerance","容差")+$" {tolerance} −",()=>ChangeTolerance(-8));
            Button(b,geometry.RightOptions[2],T("tolerance","容差")+$" {tolerance} +",()=>ChangeTolerance(8));
            Button(b,geometry.LeftOptions[3],T("outline","柔和描边")+(outline?" ✓":""),()=>{outline=!outline;dirty=true;},selected:outline);
            Button(b,geometry.RightOptions[3],T("options-back","查看效果"),()=>settingsPage=false);
            if(Generating||conversionFailed||preview?.HasHalfAlpha==true)ArtResources.TextLine(b,Generating?T("generating","正在生成…"):conversionFailed?message:T("alpha-note","半透明像素将转为实色豆"),
                new(area.X,geometry.LeftOptions[3].Bottom+8,area.Width,24),ArtResources.MutedInk,scale:0.82f);
        }
        else
        {
            var box=geometry.Original;
            ArtResources.Panel(b,box,"inset");
            var inside=new UiRect(box.X+6,box.Y+6,box.Width-12,box.Height-12);
            if(showOriginal)DrawOriginal(b,inside);
            else if(preview is not null)ArtResources.DesignPreview(b,preview.Design,inside,framed:false);
            ArtResources.TextLine(b,chooserTask is not null?T("waiting","等待选择图片…"):Generating&&pixels is not null?T("generating","正在生成…"):message.Length>0?message:$"{filename} · {preview?.Beads??0} {T("beads","颗豆")}"+(preview?.Reduced==true?" · "+T("reduced","已缩小"):preview?.Restored==true?" · "+T("restored","已无损还原"):""),
                geometry.Status,ArtResources.Ink,scale:0.82f);
            var toggle=new UiRect(geometry.Cancel.X,geometry.Status.Bottom,geometry.Cancel.Width,40);
            var settings=new UiRect(geometry.Apply.X,geometry.Status.Bottom,geometry.Apply.Width,40);
            Button(b,toggle,showOriginal?T("result","效果"):T("source","原图"),()=>showOriginal=!showOriginal);
            Button(b,settings,T("options","导入设置"),()=>settingsPage=true);
            if(preview?.HasHalfAlpha==true)ArtResources.TextLine(b,T("alpha-note","半透明像素将转为实色豆"),
                new(box.X+8,box.Bottom-28,box.Width-16,24),ArtResources.MutedInk,scale:0.82f);
        }
        Button(b,geometry.Cancel,T("cancel","取消"),Close);
        Button(b,geometry.Apply,T("apply","进入画板"),()=>{if(Generating||conversionFailed)return;var d=preview!.Design.Copy(true);Close();accept(d);},chooserTask is null&&!Generating&&!conversionFailed&&preview is {Beads:>0});
    }
    private void DrawOriginal(SpriteBatch b,UiRect r)
    {
        if(texture is null)return;
        float z=Math.Min(r.Width/(float)texture.Width,r.Height/(float)texture.Height);
        int w=Math.Max(1,(int)(texture.Width*z)),h=Math.Max(1,(int)(texture.Height*z));
        b.Draw(texture,new Rectangle(r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h),Color.White);
    }
    private string Kind()=>kind switch{"ornament"=>T("ornament","摆件"),"sword"=>T("sword","剑"),"dagger"=>T("dagger","匕首"),"hammer"=>T("hammer","锤"),_=>T("picture","拼豆画")};
    private void NextKind()
    {
        autoSize=false;kind=kind switch{"picture"=>"ornament","ornament"=>"sword","sword"=>"dagger","dagger"=>"hammer",_=>"picture"};
        if(kind is "sword" or "dagger" or "hammer")
        {
            int factor=restore?Math.Max(1,prepared?.ExactScale??1):1;
            int extent=Math.Max((texture?.Width??16)/factor,(texture?.Height??16)/factor);
            weaponSize=extent<=16?16:extent<=24?24:32;
        }
        dirty=true;
    }
    private void ChangeSize()
    {
        autoSize=false;
        if(kind is "sword" or "dagger" or "hammer")weaponSize=weaponSize==16?24:weaponSize==24?32:16;
        else large=!large;
        dirty=true;
    }
}
#endif
