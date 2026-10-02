using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

if(args.Length<2){Console.Error.WriteLine("Usage: ForgeChecks <game-directory> <mod-dll> [check-mode]");Environment.ExitCode=1;return;}
string game=Path.GetFullPath(args[0]),mod=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(_,name)=>
{
    foreach(string dir in new[]{Path.GetDirectoryName(mod)!,game,Path.Combine(game,"smapi-internal")})
    {
        string path=Path.Combine(dir,name.Name+".dll");
        if(File.Exists(path))return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }
    return null;
};
try
{
    RunChecks(args,game);
}
catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}

// Resolve game assemblies before the JIT binds checks which reference their types.
[MethodImpl(MethodImplOptions.NoInlining)]
static void RunChecks(string[] args,string game)
{
    if(args.Contains("--player-experience-only"))PlayerExperienceRuntimeChecks.Run();
    else if(args.Contains("--reliability-only"))ReliabilityRuntimeChecks.Run();
    else if(args.Contains("--weapon-speed-api"))GalaxyBalanceChecks.InspectTiming();
    else if(args.Contains("--native-shape-survey"))GalaxyBalanceChecks.Run(game,true);
    else if(args.Contains("--galaxy-balance"))GalaxyBalanceChecks.Run(game);
    else if(args.Contains("--shape-only"))ShapeRuntimeChecks.Run();
    else if(args.Contains("--inspect"))ForgeRuntimeChecks.Inspect();
    else ForgeRuntimeChecks.Run();
}
