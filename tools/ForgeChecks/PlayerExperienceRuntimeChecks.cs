using System.Reflection;
using System.Text.Json;
using BetterBeads.Data;

public class RecoveryMonitorProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>targetMethod?.ReturnType is {} t&&t!=typeof(void)&&t.IsValueType?Activator.CreateInstance(t):null;
}

internal static class PlayerExperienceRuntimeChecks
{
    internal static void Run()
    {
        int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
        var type=typeof(Blueprint).Assembly.GetType("BetterBeads.Runtime.DraftRecovery",true)!;
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;var ctor=type.GetConstructors(flags).Single();
        var monitor=typeof(DispatchProxy).GetMethod("Create")!.MakeGenericMethod(ctor.GetParameters()[1].ParameterType,typeof(RecoveryMonitorProxy)).Invoke(null,null)!;
        var root=Path.GetFullPath(Path.Combine(".tools","player-recovery-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
        object Recovery(ulong save,long player)
        {var r=ctor.Invoke(new object[]{root,monitor});type.GetMethod("Bind",flags)!.Invoke(r,new object[]{save,player});return r;}
        object? Pending(object r)=>type.GetProperty("Pending",flags)!.GetValue(r);
        void Capture(object r,EditorDocument d)=>type.GetMethod("Checkpoint",flags)!.Invoke(r,new object[]{d});
        bool Resolve(object r,EditorDocument d,bool restore)=>(bool)type.GetMethod("Resolve",flags)!.Invoke(r,new object[]{d,restore})!;
        var d=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),SimpleCrafting.Catalog(),"front");
        d.BeginStroke(3,4,BrushTool.Paint,BeadPalette.Id(0x123456ff),"decoration");d.EndStroke();
        var recovery=Recovery(123,42);Capture(recovery,d);
        string file=Path.Combine(root,"draft-recovery","123-42.json");
        Check(File.Exists(file),"dirty artwork writes a recovery file without touching the library");
        string before=File.ReadAllText(file);var timestamp=File.GetLastWriteTimeUtc(file);Capture(recovery,d);
        Check(File.ReadAllText(file)==before&&File.GetLastWriteTimeUtc(file)==timestamp,"unchanged checkpoint does not rewrite or recopy the draft");
        var reload=Recovery(123,42);
        Check(Pending(reload) is not null,"restart restores a pending local recovery");
        Check(Pending(Recovery(123,43)) is null&&Pending(Recovery(124,42)) is null,"recovery is isolated by both farmer and save identity");
        var blank=new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),SimpleCrafting.Catalog(),"front");
        Capture(reload,blank);
        Check(File.ReadAllText(file)==before,"unresolved recovery cannot be erased by a new empty draft");
        Check(!Resolve(reload,d,true)&&Pending(reload) is not null&&File.ReadAllText(file)==before,"dirty current work blocks recovery without losing either work");
        Check(Resolve(reload,blank,true)&&blank.IsDirty&&blank.Snapshot().Views["front"].Cells[67]!.Rgba==0x123456ff,"player-approved recovery restores exact pixels as an unsaved draft");
        blank.MarkSaved(1);Capture(reload,blank);
        Check(!File.Exists(file),"manual save clears obsolete local recovery");
        d.Rename("changed again");Capture(recovery,d);var deleting=Recovery(123,42);
        Check(Resolve(deleting,new EditorDocument(SimpleCrafting.Blank(SimpleCrafting.Picture16),SimpleCrafting.Catalog(),"front"),false)&&!File.Exists(file),"explicitly deleting recovery does not create a saved blueprint");
        File.WriteAllText(file,"broken recovery");var broken=Recovery(123,42);Capture(broken,d);
        Check(File.ReadAllText(file)=="broken recovery"&&(bool)type.GetProperty("ReadError",flags)!.GetValue(broken)!,"corrupt recovery remains untouched and exposes an error for the player");
        Check(Directory.GetFiles(Path.GetDirectoryName(file)!,"*.tmp").Length==0,"checkpoint does not leave staging files");
        Console.WriteLine($"PASS {passed} player experience runtime checks. Temporary files only: {root}");
    }
}
