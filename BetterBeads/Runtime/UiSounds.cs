using StardewValley;

namespace BetterBeads.Runtime;

internal static class UiSounds
{
    private static double nextBead;
    private static readonly HashSet<string> failed=new();
    internal static void Play(string cue)
    {
        if(failed.Contains(cue))return;
        try{Game1.playSound(cue);}
        catch{failed.Add(cue);} // Audio failure must never interrupt drawing or a completed transaction.
    }
    internal static void BeadPlaced()
    {
        if(!PlayMode.Feedback.BeadSounds)return;
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        if(now<nextBead)return;
        nextBead=now+0.075;Play("stoneStep");
    }
    internal static void Hover(bool enter){if(PlayMode.Feedback.HoverSounds)Play(enter?"shiny4":"shwip");}
    internal static void Click(){if(PlayMode.Feedback.ClickSounds)Play("smallSelect");}
    internal static void Success(string cue="reward"){if(PlayMode.Feedback.SuccessSounds)Play(cue);}
}
