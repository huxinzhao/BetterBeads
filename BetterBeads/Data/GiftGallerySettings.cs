#if BEADS_LITE
namespace BetterBeads.Data;

public sealed class GiftGalleryHome
{
    public string Location { get; set; } = "";
    // Order determines left-to-right display slots on available native wall sections.
    public List<string> Villagers { get; set; } = new();
    // Optional explicit wall slot per villager; absent keys use Villagers order.
    public Dictionary<string,int> WallSlots { get; set; } = new();
}

public sealed class GiftGallerySettings
{
    public List<GiftGalleryHome> Homes { get; set; } = new();
    public bool IsValid=>Homes is not null&&Homes.Count>0
        &&Homes.All(h=>!string.IsNullOrWhiteSpace(h.Location)&&h.Villagers is {Count:>0}
            &&h.Villagers.All(v=>!string.IsNullOrWhiteSpace(v))
            &&h.Villagers.Distinct(StringComparer.Ordinal).Count()==h.Villagers.Count
            &&h.WallSlots is not null&&h.WallSlots.All(p=>h.Villagers.Contains(p.Key)&&p.Value>=0)
            &&h.WallSlots.Values.Distinct().Count()==h.WallSlots.Count)
        &&Homes.Select(h=>h.Location).Distinct(StringComparer.Ordinal).Count()==Homes.Count;
}
#endif
