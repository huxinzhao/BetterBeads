using BetterBeads.Data;
using StardewValley;

namespace BetterBeads.Runtime;

internal static class WorkbenchUi
{
    private static WorkbenchScale? current;
    public static bool Active=>current is not null;
    public static int MouseX=>current?.Logical(Game1.getMouseX(true))??Game1.getMouseX(true);
    public static int MouseY=>current?.Logical(Game1.getMouseY(true))??Game1.getMouseY(true);
    public static IDisposable Enter(WorkbenchScale scale)=>new Scope(scale);
    private sealed class Scope:IDisposable
    {
        private readonly WorkbenchScale? previous=current;
        public Scope(WorkbenchScale scale){current=scale;}
        public void Dispose(){current=previous;}
    }
}
