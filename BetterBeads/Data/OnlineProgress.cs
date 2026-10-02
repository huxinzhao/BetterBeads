#if BEADS_LITE
namespace BetterBeads.Data;

/// <summary>Host-owned online records; the historical local progress stays in its original save key.</summary>
public sealed class OnlineProgress
{
    public Dictionary<long,SaveProgress> Players {get;set;}=new();
    public Dictionary<string,OnlineGallerySlot> Gallery {get;set;}=new();
    public long GiftSequence {get;set;}
    public HashSet<string> GalleryReceipts {get;set;}=new(StringComparer.Ordinal);
}
public sealed class OnlineGift
{
    public long FarmerId {get;set;}
    public long Sequence {get;set;}
    public int Day {get;set;}
    public string Giver {get;set;}="";
    public ProductSnapshot Painting {get;set;}=new();
}
public sealed class OnlineGallerySlot
{
    public Dictionary<long,OnlineGift> Candidates {get;set;}=new();
    public OnlineGift? Displayed {get;set;}
}
public static class OnlineRules
{
    public const string Protocol="1.1.0-RC4-reliability1";
    public static bool ValidPersonal(SaveProgress? p)=>p is not null&&p.SchemaVersion==1
        &&p.BlueprintRecords is not null&&p.BlueprintRecords.Count<=10000
        &&p.BlueprintRecords.All(pair=>!string.IsNullOrWhiteSpace(pair.Key)&&pair.Key.Length<=128&&pair.Value is not null&&pair.Value.Length<=2_000_000)
        &&p.FavoriteColors is {Count:20}&&p.HiddenLiteTemplates is not null
        &&p.FavoriteBlueprintIds is not null&&p.RecentBlueprintIds is not null;
    public static bool ValidWorld(OnlineProgress? world)=>world is not null&&world.GiftSequence>=0
        &&world.Players is not null&&world.Gallery is not null&&world.GalleryReceipts is not null
        &&world.GalleryReceipts.All(id=>id is {Length:>=34 and <=64})
        &&world.Gallery.All(p=>!string.IsNullOrWhiteSpace(p.Key)&&p.Value is not null&&p.Value.Candidates is not null
            &&p.Value.Candidates.All(c=>c.Value is not null&&c.Key==c.Value.FarmerId&&ValidGift(c.Value,world.GiftSequence))
            &&(p.Value.Displayed is null||ValidGift(p.Value.Displayed,world.GiftSequence)));
    private static bool ValidGift(OnlineGift gift,long sequence)=>gift.Sequence>=0&&gift.Sequence<=sequence
        &&gift.Day>=0&&gift.Giver is not null&&GiftGallery.Eligible(gift.Painting);
    // A client can edit its library, never the host's market sequence, letters, or gallery.
    public static void ApplyPersonal(SaveProgress target,SaveProgress source)
    {
        if(!ValidPersonal(source))throw new ArgumentException("Invalid personal progress");
        target.BlueprintRecords=new(source.BlueprintRecords);
        target.FavoriteColors=new(source.FavoriteColors);
        target.HiddenLiteTemplates=new(source.HiddenLiteTemplates);
        target.FavoriteBlueprintIds=new(source.FavoriteBlueprintIds);
        target.RecentBlueprintIds=new(source.RecentBlueprintIds);
        LibraryPreferences.Normalize(target);
    }
    public static void Receive(OnlineProgress world,string npc,long farmer,string giver,ProductSnapshot painting,int day)
    {
        if(!GiftGallery.Eligible(painting)||world.GiftSequence==long.MaxValue)throw new ArgumentException("Invalid painting or gift sequence");
        if(!world.Gallery.TryGetValue(npc,out var slot))world.Gallery[npc]=slot=new();
        if(slot.Candidates.Values.Any(c=>c.Painting.InstanceId==painting.InstanceId)
            ||slot.Displayed?.Painting.InstanceId==painting.InstanceId)return;
        slot.Candidates[farmer]=new(){FarmerId=farmer,Giver=giver,Painting=painting.Copy(),Day=day,Sequence=++world.GiftSequence};
    }
    public static bool Advance(OnlineGallerySlot slot,int today,Func<long,int> hearts)
    {
        var next=slot.Candidates.Values.Where(c=>c.Day<today&&c.Sequence>(slot.Displayed?.Sequence??-1)
            &&hearts(c.FarmerId)>=GiftGallery.RequiredHearts).OrderByDescending(c=>c.Sequence).FirstOrDefault();
        if(next is null)return false;
        slot.Displayed=next;return true;
    }
}
#endif
