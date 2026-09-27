using System.Reflection;
using System.Runtime.Loader;

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
    if(args.Contains("--inspect"))ForgeRuntimeChecks.Inspect();
    else ForgeRuntimeChecks.Run();
}
catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
