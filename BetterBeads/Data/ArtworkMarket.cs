#if BEADS_LITE
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace BetterBeads.Data;

public sealed class ArtworkValuation
{
    public int Version { get; set; } = 3;
    public long MaterialValue { get; set; }
    public int Percent { get; set; }
    public bool Collector { get; set; }
    public int CollectorMultiplier { get; set; }
    public int FinalPrice { get; set; }
    public ArtworkValuation Copy() => new() { Version=Version,MaterialValue=MaterialValue,Percent=Percent,Collector=Collector,CollectorMultiplier=CollectorMultiplier,FinalPrice=FinalPrice };
}

public sealed class CollectorLetter
{
    public int Day { get; set; }
    public List<CollectorWork> Works { get; set; } = new();
}

public sealed class CollectorWork
{
    public string InstanceId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Price { get; set; }
}

public static class ArtworkMarket
{
    public static long MaterialValue(ProductSnapshot snapshot,Func<string,int?> basePrice)
    {
        if(snapshot.CreatedInCreativeMode||!SimpleCrafting.Supported(snapshot.Design))return 0;
        var cost=SimpleCrafting.Cost(snapshot.Design);
        if(cost.Item.Length==0||cost.Count<=0||snapshot.ActualMaterials.Count!=1
            ||!snapshot.ActualMaterials.TryGetValue(cost.Item,out int used)||used!=cost.Count)return 0;
        int price=basePrice(cost.Item)??0;
        return price>0?(long)price*used:0;
    }

    public static bool Valid(ArtworkValuation? value)
    {
        if(value is null)return true;
        if(value.MaterialValue<0||value.Percent is <0 or >1000||value.Collector&&value.Percent<500)return false;
        int multiplier=value.Version switch
        {
            1 when value.CollectorMultiplier==0 => value.Collector?100:1,
            2 or 3 when (value.Collector
                ? value.CollectorMultiplier is >=10 and <=100
                : value.CollectorMultiplier==0)
                => value.Collector?value.CollectorMultiplier:1,
            _ => 0
        };
        return multiplier>0&&value.FinalPrice==Price(value.MaterialValue,value.Percent,multiplier);
    }

    public static int Price(long materialValue,int percent,int collectorMultiplier)
    {
        if(materialValue<=0||percent<=0)return 0;
        // Divide before multiplying for safe arithmetic even with malformed external values.
        long whole=materialValue/100, remainder=materialValue%100;
        long amount=whole>long.MaxValue/1000?long.MaxValue:whole*percent;
        amount=amount>long.MaxValue-remainder*percent/100?long.MaxValue:amount+remainder*percent/100;
        amount=amount>int.MaxValue/collectorMultiplier?int.MaxValue:amount*collectorMultiplier;
        return (int)Math.Min(int.MaxValue,amount);
    }

    public static ArtworkValuation Roll(long saveId,long sequence,ProductSnapshot snapshot,Func<string,int?> basePrice)
    {
        long value=MaterialValue(snapshot,basePrice);
        var cost=SimpleCrafting.Cost(snapshot.Design);
        string bill=$"{(int)snapshot.Design.Use}|{cost.Item}|{cost.Count}";
        int percent=DrawPercent(Draw(saveId,sequence,bill,"tier"),Draw(saveId,sequence,bill,"weight"));
        bool collector=value>0&&percent>=500&&Draw(saveId,sequence,bill,"collector")%100==0;
        int multiplier=collector?10+(int)(Draw(saveId,sequence,bill,"collector-multiplier")%91):0;
        return new(){MaterialValue=value,Percent=percent,Collector=collector,CollectorMultiplier=multiplier,
            FinalPrice=Price(value,percent,collector?multiplier:1)};
    }

    public static int DrawPercent(ulong tier,ulong weight)
    {
        int start,end;
        int band=(int)(tier%100);
        if(band<95){start=0;end=150;}
        else if(band<99){start=151;end=499;}
        else{start=500;end=1000;}
        // Rejection from weighted integer tickets keeps the 80% mode exact.
        long total=0;
        for(int p=start;p<=end;p++)total+=Weight(p,start,end);
        long ticket=(long)(weight%(ulong)total);
        for(int p=start;p<=end;p++){ticket-=Weight(p,start,end);if(ticket<0)return p;}
        return end;
    }

    private static long Weight(int p,int start,int end)
        =>start==0?p<=80?p+1:161-p:2L*(end-start)+1-(p-start);

    private static ulong Draw(long saveId,long sequence,string bill,string label)
    {
        byte[] input=Encoding.UTF8.GetBytes($"BetterBeads.ArtworkMarket.3|{saveId}|{sequence}|{bill}|{label}");
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(input));
    }
}

public static class CollectorLedger
{
    public static bool Record(SaveProgress progress,int day,ProductSnapshot snapshot)
    {
        if(snapshot.Valuation?.Collector!=true)return false;
        var letter=progress.CollectorLetters.FirstOrDefault(l=>l.Day==day);
        if(letter is null){letter=new CollectorLetter{Day=day};progress.CollectorLetters.Add(letter);}
        if(letter.Works.Any(w=>w.InstanceId==snapshot.InstanceId))return false;
        letter.Works.Add(new CollectorWork{InstanceId=snapshot.InstanceId,Name=snapshot.Design.Name,Price=snapshot.Valuation.FinalPrice});
        return true;
    }
    public static void Remove(SaveProgress progress,int day,string instanceId)
    {
        foreach(var letter in progress.CollectorLetters.Where(l=>l.Day==day).ToArray())
        {
            letter.Works.RemoveAll(w=>w.InstanceId==instanceId);
            if(letter.Works.Count==0)progress.CollectorLetters.Remove(letter);
        }
    }
    public static IEnumerable<CollectorLetter> Due(SaveProgress progress,int today)
        =>progress.CollectorLetters.Where(l=>l.Day<today&&l.Works.Count>0);
}
#endif
