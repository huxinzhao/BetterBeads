namespace BetterBeads.Data;

internal sealed class ShapeHitReceipts
{
    private readonly Dictionary<long,long> last=new();
    public bool TryAccept(long peer,long sequence)
    {
        if(sequence<=0||sequence<=last.GetValueOrDefault(peer))return false;
        last[peer]=sequence;return true;
    }
    public void Forget(long peer)=>last.Remove(peer);
    public void Clear()=>last.Clear();
}

/// <summary>Six independent half-second installments. New wounds never postpone older ones.</summary>
public sealed class ShapeBleeding
{
    private sealed class Wound { public double Age,Budget;public int Delivered; }
    private readonly List<Wound> wounds=new();
    private double fraction;
    public bool Active=>wounds.Count>0;
    public void Add(double budget){if(double.IsFinite(budget)&&budget>0)wounds.Add(new(){Budget=budget});}
    public int Advance(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<=0)return 0;
        foreach(var wound in wounds)
        {
            wound.Age+=seconds;int due=Math.Min(6,(int)(wound.Age/.5));
            fraction+=(due-wound.Delivered)*wound.Budget/6;wound.Delivered=due;
        }
        wounds.RemoveAll(w=>w.Delivered==6);
        int damage=(int)Math.Min(int.MaxValue,Math.Floor(fraction+1e-9));fraction-=damage;
        return damage;
    }
    public static double Susceptibility(string typeName)=>typeName switch
    {
        "Skeleton" or "Ghost" or "Mummy" or "DwarvishSentry" or "Robot" or "HotHead"=>0,
        "GreenSlime" or "BigSlime"=>.5,
        _=>1
    };
    public static double Budget(double strength,double baseline,string typeName)
        =>double.IsFinite(strength)&&double.IsFinite(baseline)?Math.Clamp(strength,0,1)*Math.Max(0,baseline)*.3*Susceptibility(typeName):0;
}
