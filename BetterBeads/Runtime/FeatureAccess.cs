namespace BetterBeads.Runtime;

internal static class FeatureAccess
{
    public static bool Has(string feature)
    {
#if BEADS_LITE
        return false;
#else
        return WorkshopService.Has(feature);
#endif
    }
}
