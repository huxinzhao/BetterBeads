#if BEADS_LITE
namespace BetterBeads.Data;

public sealed class GiftGalleryRecord
{
    public ProductSnapshot? Latest { get; set; }
    public int GiftDay { get; set; } = -1;
    public ProductSnapshot? Displayed { get; set; }
    public int DisplayDay { get; set; } = -1;
    public string Giver { get; set; } = "";
}

public static class GiftGallery
{
    public const int RequiredHearts=6;
    public static bool Eligible(ProductSnapshot? snapshot)
        =>snapshot is not null&&snapshot.Design.Use==ProductUse.Picture
            &&SimpleCrafting.Wall(snapshot.Design.TemplateId)&&ProductTemplates.Matches(snapshot);

    public static void Receive(SaveProgress progress,string npc,ProductSnapshot snapshot,string giver,int day)
    {
        if(string.IsNullOrWhiteSpace(npc)||!Eligible(snapshot))throw new ArgumentException("A valid bead painting and villager are required.");
        if(!progress.GiftGalleryRecords.TryGetValue(npc,out var record))progress.GiftGalleryRecords[npc]=record=new();
        record.Latest=snapshot.Copy();record.GiftDay=day;record.Giver=giver;
    }

    public static bool Advance(GiftGalleryRecord record,int hearts,int today)
    {
        if(record.Latest is null||record.GiftDay>=today||hearts<RequiredHearts
            ||record.Displayed?.InstanceId==record.Latest.InstanceId)return false;
        record.Displayed=record.Latest.Copy();record.DisplayDay=today;return true;
    }
}
#endif
