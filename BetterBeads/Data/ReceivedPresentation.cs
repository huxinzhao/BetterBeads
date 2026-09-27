namespace BetterBeads.Data;

/// <summary>Transient display state only; no inventory, progress or save mutations.</summary>
internal sealed class ReceivedPresentation
{
    private double elapsed;
    private bool released;
    public bool CanContinue=>released&&elapsed>=0.45;
    public double Reveal=>1-Math.Pow(1-Math.Clamp(elapsed/0.28,0,1),3);
    public void Update(double seconds,bool inputReleased)
    {elapsed+=Math.Max(0,seconds);released|=inputReleased;}
    public static string Message(ProductSnapshot product,string? saleLine=null)
    {
        // Names must remain text, not native dialogue page/control delimiters.
        string name=product.Design.Name.Replace('^','＾').Replace('#','＃').Replace('\r',' ').Replace('\n',' ');
        string message=ContentText.Format("simple.received-native",$"你制作了「{name}」！");
        if(SimpleCrafting.IsWeapon(product.Design.Use))message+="\n"+ContentText.Format("simple.received-damage",$"伤害：{product.FinalStats.GetValueOrDefault("minDamage")}～{product.FinalStats.GetValueOrDefault("maxDamage")}");
#if BEADS_LITE
        if(saleLine is not null)message+="\n"+saleLine;
        else if(product.CreatedInCreativeMode)message+="\n"+ContentText.Get("market.creative-price","创造模式作品 · 售价：1金");
        else if(product.Valuation is {} valuation)
        {
            message+="\n"+ContentText.Format("market.final-price",$"收藏估价：{valuation.FinalPrice}金");
            if(valuation.Collector)message+="\n"+ContentText.Get("market.collector-note","神秘艺术收藏家赏识了这件作品！");
        }
#endif
        return message+"\n"+ContentText.Get("simple.received-saved","已放入背包。");
    }
    public static UiRect Preview(int width,int height,int dialogueTop,int headX,int headY)
    {
        int topLimit=Math.Clamp(dialogueTop-16,48,Math.Max(48,height-16));
        int size=Math.Max(24,Math.Min(128,Math.Min(width-40,topLimit-24)));
        return new(Math.Clamp(headX-size/2,16,Math.Max(16,width-size-16)),
            Math.Clamp(headY-size,16,Math.Max(16,topLimit-size)),size,size);
    }
}
