using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace BetterBeads.Runtime;

internal static class MenuPause
{
    public static void Register() => new Harmony("xinzh.BetterBeads.MenuPause").Patch(
        AccessTools.Method(typeof(Game1),nameof(Game1.shouldTimePass)),postfix:new HarmonyMethod(typeof(MenuPause),nameof(AfterTimeCheck)));
    private static void AfterTimeCheck(ref bool __result)
    {
        // Never write shared pause flags: once the menu closes, the original result is untouched.
        #if BEADS_LITE
        if(!Context.IsMultiplayer && Game1.activeClickableMenu is (WorkbenchMenu or ProductReceivedMenu)) __result=false;
#else
        if(!Context.IsMultiplayer && Game1.activeClickableMenu is (WorkbenchMenu or WorkshopMenu)) __result=false;
#endif
    }
}
