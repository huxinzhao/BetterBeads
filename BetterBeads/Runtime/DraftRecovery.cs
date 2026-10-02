#if BEADS_LITE
using BetterBeads.Data;
using StardewModdingAPI;
using System.Text;
using System.Text.Json;

namespace BetterBeads.Runtime;

// Local recovery is separate from the saved library and never shared with another farmer.
internal sealed class DraftRecovery
{
    private readonly string directory;
    private readonly IMonitor monitor;
    private string? path;
    private long version=-1;
    private bool readable=true,reported;
    internal DraftRecoveryEntry? Pending {get;private set;}
    internal bool ReadError=>!readable;
    internal DraftRecovery(string modDirectory,IMonitor monitor){directory=Path.Combine(modDirectory,"draft-recovery");this.monitor=monitor;}
    internal void Bind(ulong saveId,long playerId)
    {
        path=Path.Combine(directory,$"{saveId}-{playerId}.json");version=-1;Pending=null;readable=true;reported=false;
        try
        {
            if(!File.Exists(path))return;
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Recovery file is too large");
            var entry=JsonSerializer.Deserialize<DraftRecoveryEntry>(File.ReadAllText(path));
            if(entry?.Valid!=true)throw new InvalidDataException("Invalid draft recovery");
            Pending=entry;
        }
        catch(Exception ex){readable=false;Report(ex);}
    }
    internal bool Resolve(EditorDocument document,bool restore)
    {
        if(Pending is not {} entry)return false;
        if(restore&&!document.RestoreRecovery(entry))return false;
        if(!restore)
        {
            try{if(path is not null)File.Delete(path);}catch(Exception ex){Report(ex);return false;}
        }
        Pending=null;version=-1;return true;
    }
    internal void Checkpoint(EditorDocument? document)
    {
        if(path is null||document is null||!readable||Pending is not null||version==document.ChangeVersion)return;
        string? staging=null;
        try
        {
            if(document.IsDirty)
            {
                Directory.CreateDirectory(directory);staging=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllText(staging,DesignStorage.Serialize(document.RecoverySnapshot()),new UTF8Encoding(false));
                File.Move(staging,path,true);
            }
            else File.Delete(path);
            version=document.ChangeVersion;reported=false;
        }
        catch(Exception ex){Report(ex);}
        finally{if(staging is not null)try{File.Delete(staging);}catch{}}
    }
    private void Report(Exception ex)
    {if(reported)return;reported=true;monitor.Log("Draft recovery kept the previous file: "+ex.Message,LogLevel.Warn);}
}
#endif
