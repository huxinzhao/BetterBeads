namespace BetterBeads.Data;

// 5% = one health per 20 actual damage. Fractional health is retained only while injured.
public static class LifeSteal
{
    public static long ActualDamage(int before,int after,int reportedDamage)
        =>reportedDamage>0?Math.Max(0L,(long)Math.Max(0,before)-Math.Max(0,after)):0;
    public static int HealRate(long actualDamage,int health,int maximum,double rate,ref double remainder)
    {
        if(health<=0 || maximum<=health){remainder=0;return 0;}
        if(actualDamage<=0 || !double.IsFinite(rate) || rate<=0 || rate>0.05)return 0;
        double total=Math.Min(maximum-health,actualDamage*rate+Math.Clamp(remainder,0,0.999999999));
        int healed=(int)Math.Floor(total+1e-10);remainder=health+healed>=maximum?0:Math.Max(0,total-healed);return healed;
    }
    public static int Heal(long actualDamage,int health,int maximum,ref int remainder)
    {
        if(health<=0 || maximum<=health){remainder=0;return 0;}
        if(actualDamage<=0)return 0;
        long missing=(long)maximum-health;
        long numerator=Math.Min(actualDamage,missing*20)+Math.Clamp(remainder,0,19);
        int healing=(int)Math.Min(missing,numerator/20);
        remainder=healing==missing?0:(int)(numerator%20);
        return healing;
    }
}
