#if BEADS_LITE
namespace BetterBeads.Data;

// Test saves use their own storage key. No story, commission or preset state is needed.
public sealed class WorkshopProgress
{
    public int Version { get; set; } = 2;
    public bool IsValid => Version == 2;
    public static WorkshopProgress Legacy() => new();
    public void UpgradeFromPrevious(int day) => Version = 2;
    public int EffectSlots(bool creative = false) => 2;
    public bool CanMake(Blueprint design, ProcessingCatalog catalog, bool creative = false)
        => DesignStorage.IsStructurallyValid(design);
}
#endif
