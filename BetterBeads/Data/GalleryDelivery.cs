#if BEADS_LITE
using System.Text.Json;
namespace BetterBeads.Data;

public sealed class PendingGalleryGift
{
    public string Request {get;set;}="";
    public string Npc {get;set;}="";
    public int Day {get;set;}
    public ProductSnapshot Painting {get;set;}=new();
}

public static class GalleryDelivery
{
    public static bool Valid(PendingGalleryGift gift)=>gift is not null&&gift.Request is {Length:32}
        &&Guid.TryParseExact(gift.Request,"N",out _)&&!string.IsNullOrWhiteSpace(gift.Npc)&&gift.Npc.Length<=128
        &&gift.Day>=0&&GiftGallery.Eligible(gift.Painting);
    public static List<PendingGalleryGift> Read(string value)
    {
        var gifts=JsonSerializer.Deserialize<List<PendingGalleryGift>>(value)??throw new InvalidDataException("Missing gallery outbox");
        if(gifts.Any(g=>!Valid(g))||gifts.Select(g=>g.Request).Distinct(StringComparer.Ordinal).Count()!=gifts.Count)
            throw new InvalidDataException("Invalid gallery outbox");
        return gifts;
    }
    public static bool Receive(OnlineProgress world,long farmer,string giver,PendingGalleryGift gift)
    {
        if(!Valid(gift))throw new ArgumentException("Invalid gallery delivery");
        string id=farmer+":"+gift.Request;
        if(world.GalleryReceipts.Contains(id))return false;
        if(!world.Gallery.TryGetValue(gift.Npc,out var slot)||!slot.Candidates.TryGetValue(farmer,out var later)||later.Day<=gift.Day)
            OnlineRules.Receive(world,gift.Npc,farmer,giver,gift.Painting,gift.Day);
        world.GalleryReceipts.Add(id);return true;
    }
}
#endif
