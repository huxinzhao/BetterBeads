using BetterBeads.Data;

namespace BetterBeads.Runtime;

internal static class WorkbenchInput
{
    private static string detected=OperatingSystem.IsAndroid()?"touch":"mouse";
    internal static string Mode=>PlayMode.InputMode=="auto"?detected:PlayMode.InputMode;
    internal static bool Touch=>Mode=="touch";
    internal static bool Controller=>Mode=="controller";
    internal static void Pointer()=>detected=OperatingSystem.IsAndroid()?"touch":"mouse";
    internal static void Pad()=>detected="controller";
    internal static void Cycle()
    {
        var options=new[]{"auto","mouse","touch","controller"};
        int next=(Array.IndexOf(options,PlayMode.InputMode)+1)%options.Length;
        PlayMode.SetInputMode(options[next]);
    }
}
