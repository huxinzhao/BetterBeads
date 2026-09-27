namespace BetterBeads.Data;

public sealed record WorkshopPages(int Page,int Count,int DescriptionStart,int DescriptionCount,int ChoiceStart)
{
    public static WorkshopPages Calculate(int page,int lines,int choices,int capacity)
    {
        int textCapacity=lines>5?4:5;
        int textPages=Math.Max(1,(lines+textCapacity-1)/textCapacity),choicePages=Math.Max(1,(choices+capacity-1)/capacity);
        int count=Math.Max(textPages,choicePages);page=Math.Clamp(page,0,count-1);
        return new(page,count,Math.Min(page,textPages-1)*textCapacity,textCapacity,Math.Min(page,choicePages-1)*capacity);
    }
}
